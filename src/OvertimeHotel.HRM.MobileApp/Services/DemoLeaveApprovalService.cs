using Microsoft.Extensions.Logging;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.MobileApp.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Dịch vụ duyệt đơn từ cho CHẾ ĐỘ DEMO.
/// Hoạt động 100% trên bộ nhớ In-Memory, mô phỏng đầy đủ quy tắc RBAC và xử lý duyệt/từ chối.
/// </summary>
public class DemoLeaveApprovalService : ILeaveApprovalService
{
    private readonly IAuthService _authService;
    private readonly ILogger<DemoLeaveApprovalService> _logger;
    private readonly List<LeaveRequestItem> _seedRequests;

    public bool IsDemoMode => true;

    // Share the same demo requests with the attendance summary so approvals and
    // leave submission immediately affect the monthly view.
    internal List<OvertimeHotel.HRM.Core.Models.DonTu> GetAttendanceSnapshot()
    {
        lock (_seedRequests)
        {
            return _seedRequests.Select(r => new OvertimeHotel.HRM.Core.Models.DonTu
            {
                MaDon = r.MaDon,
                MaNhanVien = r.MaNhanVien,
                MaLoaiDon = r.MaLoaiDon,
                TuNgay = r.TuNgay,
                DenNgay = r.DenNgay,
                TrangThai = r.TrangThai,
                NgayDuyet = r.NgayDuyet,
                LoaiDon = new OvertimeHotel.HRM.Core.Models.LoaiDon
                {
                    TenLoaiDon = r.TenLoaiDon,
                    CoHuongLuong = r.CoHuongLuong
                },
                NhanVien = new OvertimeHotel.HRM.Core.Models.NhanVien
                {
                    MaNhanVien = r.MaNhanVien,
                    Ho = r.TenNhanVien,
                    MaPhongBan = r.MaPhongBan
                }
            }).ToList();
        }
    }

    public DemoLeaveApprovalService(IAuthService authService, ILogger<DemoLeaveApprovalService> logger)
    {
        _authService = authService;
        _logger = logger;
        _seedRequests = InitializeSeedData();
    }

    public Task<LeaveRequestListResponse> GetLeaveRequestsAsync(string view = "pending", string type = "all")
    {
        var user = _authService.CurrentUser;
        if (user == null || !user.IsLoggedIn)
        {
            return Task.FromResult(new LeaveRequestListResponse(new List<LeaveRequestListItemDto>(), 0, "demo"));
        }

        lock (_seedRequests)
        {
            // 1. Lọc theo thẩm quyền RBAC
            var authorized = _seedRequests.Where(r =>
                LeaveApprovalPolicy.CanView(user.TenVaiTro, user.MaPhongBan, r.MaPhongBan, r.MaLoaiDon, user.MaNhanVien, r.MaNhanVien)).ToList();

            var pendingCount = authorized.Count(r => r.TrangThai == "CHO_DUYET");

            // 2. Lọc theo view
            var isPending = string.Equals(view, "pending", StringComparison.OrdinalIgnoreCase);
            var filtered = isPending
                ? authorized.Where(r => r.TrangThai == "CHO_DUYET").OrderByDescending(r => r.NgayGui).ToList()
                : authorized.Where(r => r.TrangThai != "CHO_DUYET").OrderByDescending(r => r.NgayDuyet ?? r.NgayGui).ToList();

            // 3. Lọc theo type
            if (!string.Equals(type, "all", StringComparison.OrdinalIgnoreCase) && int.TryParse(type, out var maLoai))
            {
                filtered = filtered.Where(r => r.MaLoaiDon == maLoai).ToList();
            }

            var dtos = filtered.Select(r => new LeaveRequestListItemDto(
                MaDon: r.MaDon,
                MaNhanVien: r.MaNhanVien,
                HoTenNhanVien: r.TenNhanVien,
                ChucVu: r.TenChucVu,
                MaPhongBan: r.MaPhongBan,
                TenPhongBan: r.TenPhongBan,
                MaLoaiDon: r.MaLoaiDon,
                TenLoaiDon: r.TenLoaiDon,
                CoHuongLuong: r.CoHuongLuong,
                TuNgay: r.TuNgay,
                DenNgay: r.DenNgay,
                LyDo: r.LyDo,
                NgayGui: r.NgayGui,
                TrangThai: r.TrangThai,
                PhanHoiDuyet: r.PhanHoiDuyet,
                NgayDuyet: r.NgayDuyet,
                HoTenNguoiDuyet: r.NguoiDuyetTen
            )).ToList();

            return Task.FromResult(new LeaveRequestListResponse(dtos, pendingCount, "demo"));
        }
    }

    public Task<LeaveReviewResult> ReviewLeaveRequestAsync(int maDon, bool approved, string? phanHoi)
    {
        var user = _authService.CurrentUser;
        if (user == null || !user.IsLoggedIn)
        {
            return Task.FromResult(new LeaveReviewResult(false, "UNAUTHENTICATED", "Chưa đăng nhập."));
        }

        lock (_seedRequests)
        {
            var item = _seedRequests.FirstOrDefault(r => r.MaDon == maDon);
            if (item == null)
            {
                return Task.FromResult(new LeaveReviewResult(false, "NOT_FOUND", "Không tìm thấy đơn từ."));
            }

            var (isValid, errorCode, errorMessage) = LeaveApprovalPolicy.ValidateReviewAction(
                role: user.TenVaiTro,
                reviewerDeptId: user.MaPhongBan,
                requestDeptId: item.MaPhongBan,
                maLoaiDon: item.MaLoaiDon,
                currentStatus: item.TrangThai,
                approved: approved,
                phanHoi: phanHoi,
                reviewerEmployeeId: user.MaNhanVien,
                requestEmployeeId: item.MaNhanVien
            );

            if (!isValid)
            {
                return Task.FromResult(new LeaveReviewResult(false, errorCode ?? "VALIDATION_ERROR", errorMessage!));
            }

            item.TrangThai = approved ? "DA_DUYET" : "TU_CHOI";
            item.NgayDuyet = DateTimeOffset.UtcNow;
            item.MaNguoiDuyet = user.MaNhanVien;
            item.NguoiDuyetTen = user.HoTen;
            item.PhanHoiDuyet = string.IsNullOrWhiteSpace(phanHoi) ? null : phanHoi.Trim();

            _logger.LogInformation("[DEMO] Đã {Action} đơn #{MaDon} trong bộ nhớ Demo.", approved ? "duyệt" : "từ chối", maDon);

            return Task.FromResult(new LeaveReviewResult(
                Success: true,
                Code: "REVIEWED",
                Message: approved ? "[DEMO] Phê duyệt đơn thành công." : "[DEMO] Đã từ chối đơn thành công."
            ));
        }
    }

    public async Task<List<LeaveRequestItem>> GetPendingRequestsAsync(string role, int? maPhongBan)
    {
        var res = await GetLeaveRequestsAsync("pending", "all");
        return MapToItems(res.Items);
    }

    public async Task<List<LeaveRequestItem>> GetReviewedRequestsAsync(string role, int? maPhongBan)
    {
        var res = await GetLeaveRequestsAsync("reviewed", "all");
        return MapToItems(res.Items);
    }

    public async Task<int> GetPendingCountAsync(string role, int? maPhongBan)
    {
        var res = await GetLeaveRequestsAsync("pending", "all");
        return res.PendingCount;
    }

    public async Task<(bool Success, string Message)> ApproveAsync(int maDon)
    {
        var res = await ReviewLeaveRequestAsync(maDon, true, null);
        return (res.Success, res.Message);
    }

    public async Task<(bool Success, string Message)> RejectAsync(int maDon, string lyDoTuChoi)
    {
        var res = await ReviewLeaveRequestAsync(maDon, false, lyDoTuChoi);
        return (res.Success, res.Message);
    }

    // ═══════════════════════════════════════════════════════════════
    //  PHÍA NHÂN VIÊN NỘP ĐƠN (TSK-18) — chế độ Demo
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Danh mục loại đơn ghi cứng, khớp 5 dòng seed LOAI_DON của CSDL.
    /// </summary>
    /// <remarks>
    /// Demo chạy không có mạng nên không đọc được bảng LOAI_DON. Nội dung phải
    /// giữ nguyên với seed — nếu lệch thì form Demo duyệt được loại mà bản thật
    /// không có, và người demo tưởng app chạy sai.
    /// </remarks>
    private static readonly List<LeaveTypeOption> DemoTypes =
    [
        new(1, "Nghỉ phép năm", true, "Nghỉ phép thường niên hưởng nguyên 100% lương theo chế độ"),
        new(2, "Nghỉ ốm đau / BHXH", true, "Nghỉ theo chỉ định y tế, có giấy tờ BHXH chi trả"),
        new(3, "Nghỉ thai sản", true, "Chế độ nghỉ thai sản dành cho lao động nữ (6 tháng)"),
        new(4, "Nghỉ việc riêng không lương", false, "Nghỉ giải quyết việc gia đình, khấu trừ lương ngày công"),
        new(5, "Đơn xin thôi việc", false, "Đơn nguyện vọng xin chấm dứt hợp đồng lao động trước hạn")
    ];

    private const int MA_LOAI_PHEP_NAM = 1;

    /// <summary>Mã đơn demo cấp tiếp theo, đủ lớn để không đụng seed 100-105.</summary>
    private int _nextMaDon = 200;

    public Task<List<LeaveTypeOption>> GetSubmittableTypesAsync() =>
        Task.FromResult<List<LeaveTypeOption>>([.. DemoTypes.OrderBy(t => t.MaLoaiDon)]);

    public Task<List<LeaveRequestItem>> GetMyRequestsAsync(bool chiChoDuyet = false)
    {
        var user = _authService.CurrentUser;
        if (user is null || !user.IsLoggedIn)
        {
            return Task.FromResult(new List<LeaveRequestItem>());
        }

        lock (_seedRequests)
        {
            var mine = _seedRequests
                .Where(r => r.MaNhanVien == user.MaNhanVien)
                .Where(r => !chiChoDuyet || r.TrangThai == "CHO_DUYET")
                .OrderByDescending(r => r.NgayGui)
                .ToList();

            // Trả về bản sao: nếu trả chính danh sách, ViewModel sửa item trong
            // CollectionView cũng sửa luôn dữ liệu gốc của service.
            return Task.FromResult<List<LeaveRequestItem>>([.. mine.Select(Clone)]);
        }
    }

    public Task<int> GetUsedAnnualLeaveDaysAsync()
    {
        var user = _authService.CurrentUser;
        if (user is null || !user.IsLoggedIn)
        {
            return Task.FromResult(0);
        }

        lock (_seedRequests)
        {
            var year = DateTime.Today.Year;

            // Khớp luật production: chỉ tính đơn phép năm ĐÃ DUYỆT trong năm.
            var used = _seedRequests
                .Where(r => r.MaNhanVien == user.MaNhanVien
                            && r.MaLoaiDon == MA_LOAI_PHEP_NAM
                            && r.TrangThai == "DA_DUYET"
                            && r.DenNgay.Year == year)
                .Sum(r => Math.Max(1, r.DenNgay.DayNumber - r.TuNgay.DayNumber + 1));

            return Task.FromResult(used);
        }
    }

    public async Task<LeaveSubmitResult> SubmitRequestAsync(
        int maLoaiDon, DateOnly tuNgay, DateOnly denNgay, string lyDo)
    {
        var user = _authService.CurrentUser;
        if (user is null || !user.IsLoggedIn)
        {
            return new LeaveSubmitResult(false, "UNAUTHENTICATED", "Chưa đăng nhập.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        var (isValid, errorCode, errorMessage) = LeaveApprovalPolicy.ValidateCreate(
            maLoaiDon, tuNgay, denNgay, lyDo, today);

        if (!isValid)
        {
            return new LeaveSubmitResult(false, errorCode ?? "VALIDATION_ERROR", errorMessage!);
        }

        var pending = await GetMyRequestsAsync(chiChoDuyet: true);
        if (LeaveApprovalPolicy.OverlapsAny(tuNgay, denNgay, pending.Select(r => (r.TuNgay, r.DenNgay))))
        {
            return new LeaveSubmitResult(
                false, "OVERLAP",
                "Bạn đã có đơn đang chờ duyệt trùng với khoảng ngày này. " +
                "Vui lòng kiểm tra lịch sử đơn trước khi gửi tiếp.");
        }

        if (maLoaiDon == MA_LOAI_PHEP_NAM)
        {
            var used = await GetUsedAnnualLeaveDaysAsync();
            var (quotaOk, quotaCode, quotaMsg) = LeaveApprovalPolicy.ValidateAnnualLeaveBalance(
                denNgay.DayNumber - tuNgay.DayNumber + 1, used);

            if (!quotaOk)
            {
                return new LeaveSubmitResult(false, quotaCode ?? "EXCEEDS_QUOTA", quotaMsg!);
            }
        }

        var loai = DemoTypes.FirstOrDefault(t => t.MaLoaiDon == maLoaiDon);

        lock (_seedRequests)
        {
            var item = new LeaveRequestItem
            {
                MaDon = _nextMaDon++,
                MaNhanVien = user.MaNhanVien,
                TenNhanVien = user.HoTen,
                MaPhongBan = user.MaPhongBan,
                TenPhongBan = user.TenPhongBan,
                TenChucVu = user.TenChucVu,
                MaLoaiDon = maLoaiDon,
                TenLoaiDon = loai?.TenLoaiDon ?? $"Loại #{maLoaiDon}",
                CoHuongLuong = loai?.CoHuongLuong ?? false,
                TuNgay = tuNgay,
                DenNgay = denNgay,
                LyDo = lyDo.Trim(),
                NgayGui = DateTimeOffset.UtcNow,
                TrangThai = "CHO_DUYET"
            };

            _seedRequests.Add(item);

            _logger.LogInformation("[DEMO] Nhân viên #{MaNhanVien} nộp đơn mới #{MaDon}.", user.MaNhanVien, item.MaDon);

            return new LeaveSubmitResult(
                true, "SUBMITTED",
                $"[DEMO] Đã tạo đơn #{item.MaDon}. Dữ liệu chỉ tồn tại trong phiên Demo, không ghi xuống Supabase.",
                item.MaDon);
        }
    }

    /// <summary>Chép sang item mới để lời gọi của service không sửa được dữ liệu gốc.</summary>
    private static LeaveRequestItem Clone(LeaveRequestItem src) => new()
    {
        MaDon = src.MaDon,
        MaNhanVien = src.MaNhanVien,
        TenNhanVien = src.TenNhanVien,
        MaPhongBan = src.MaPhongBan,
        TenPhongBan = src.TenPhongBan,
        TenChucVu = src.TenChucVu,
        MaLoaiDon = src.MaLoaiDon,
        TenLoaiDon = src.TenLoaiDon,
        CoHuongLuong = src.CoHuongLuong,
        TuNgay = src.TuNgay,
        DenNgay = src.DenNgay,
        LyDo = src.LyDo,
        NgayGui = src.NgayGui,
        TrangThai = src.TrangThai,
        MaNguoiDuyet = src.MaNguoiDuyet,
        NguoiDuyetTen = src.NguoiDuyetTen,
        NgayDuyet = src.NgayDuyet,
        PhanHoiDuyet = src.PhanHoiDuyet
    };

    private static List<LeaveRequestItem> MapToItems(List<LeaveRequestListItemDto> dtos)
    {
        return dtos.Select(d => new LeaveRequestItem
        {
            MaDon = d.MaDon,
            MaNhanVien = d.MaNhanVien,
            TenNhanVien = d.HoTenNhanVien,
            TenChucVu = d.ChucVu ?? "",
            MaPhongBan = d.MaPhongBan,
            TenPhongBan = d.TenPhongBan ?? "",
            MaLoaiDon = d.MaLoaiDon,
            TenLoaiDon = d.TenLoaiDon,
            CoHuongLuong = d.CoHuongLuong,
            TuNgay = d.TuNgay,
            DenNgay = d.DenNgay,
            LyDo = d.LyDo,
            NgayGui = d.NgayGui,
            TrangThai = d.TrangThai,
            PhanHoiDuyet = d.PhanHoiDuyet,
            NgayDuyet = d.NgayDuyet,
            NguoiDuyetTen = d.HoTenNguoiDuyet
        }).ToList();
    }

    private static List<LeaveRequestItem> InitializeSeedData()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return new List<LeaveRequestItem>
        {
            new()
            {
                MaDon = 101,
                MaNhanVien = 5,
                TenNhanVien = "Phạm Thị Mai",
                MaPhongBan = 4, // Bộ Phận Tiền Sảnh (FO)
                TenPhongBan = "Bộ Phận Tiền Sảnh (FO)",
                TenChucVu = "Nhân Viên Lễ Tân",
                MaLoaiDon = 1, // Nghỉ phép năm
                TenLoaiDon = "Nghỉ phép năm",
                CoHuongLuong = true,
                TuNgay = today.AddDays(2),
                DenNgay = today.AddDays(4),
                LyDo = "Về quê giải quyết việc gia đình có hiếu hỷ.",
                NgayGui = DateTimeOffset.UtcNow.AddHours(-3),
                TrangThai = "CHO_DUYET"
            },
            new()
            {
                MaDon = 102,
                MaNhanVien = 6,
                TenNhanVien = "Vũ Đình Trọng",
                MaPhongBan = 4, // FO
                TenPhongBan = "Bộ Phận Tiền Sảnh (FO)",
                TenChucVu = "Nhân Viên Bellman",
                MaLoaiDon = 2, // Nghỉ ốm
                TenLoaiDon = "Nghỉ ốm / Khám bệnh (BHXH)",
                CoHuongLuong = true,
                TuNgay = today,
                DenNgay = today.AddDays(1),
                LyDo = "Bị sốt phát ban, bác sĩ chỉ định nghỉ ngơi tĩnh dưỡng 2 ngày.",
                NgayGui = DateTimeOffset.UtcNow.AddHours(-1),
                TrangThai = "CHO_DUYET"
            },
            new()
            {
                MaDon = 103,
                MaNhanVien = 12,
                TenNhanVien = "Lý Kim Ngân",
                MaPhongBan = 3, // Phòng Kế Toán
                TenPhongBan = "Phòng Kế Toán - Tài Chính",
                TenChucVu = "Kế Toán Viên",
                MaLoaiDon = 3, // Nghỉ thai sản
                TenLoaiDon = "Nghỉ chế độ thai sản",
                CoHuongLuong = true,
                TuNgay = today.AddDays(15),
                DenNgay = today.AddDays(195),
                LyDo = "Nghỉ sinh con theo chế độ thai sản quy định của nhà nước (6 tháng).",
                NgayGui = DateTimeOffset.UtcNow.AddDays(-1),
                TrangThai = "CHO_DUYET"
            },
            new()
            {
                MaDon = 104,
                MaNhanVien = 8,
                TenNhanVien = "Bùi Văn Nam",
                MaPhongBan = 5, // F&B
                TenPhongBan = "Bộ Phận Ẩm Thực (F&B)",
                TenChucVu = "Nhân Viên Phục Vụ",
                MaLoaiDon = 4, // Nghỉ không lương
                TenLoaiDon = "Nghỉ việc riêng không lương",
                CoHuongLuong = false,
                TuNgay = today.AddDays(5),
                DenNgay = today.AddDays(7),
                LyDo = "Có việc cá nhân cần giải quyết tại quê nhà.",
                NgayGui = DateTimeOffset.UtcNow.AddHours(-5),
                TrangThai = "CHO_DUYET"
            },
            new()
            {
                MaDon = 105,
                MaNhanVien = 15,
                TenNhanVien = "Đặng Hoàng Long",
                MaPhongBan = 6, // Buồng Phòng
                TenPhongBan = "Bộ Phận Buồng Phòng (Housekeeping)",
                TenChucVu = "Nhân Viên Buồng Phòng",
                MaLoaiDon = 5, // Thôi việc
                TenLoaiDon = "Đơn xin thôi việc / Chấm dứt HĐLĐ",
                CoHuongLuong = false,
                TuNgay = today.AddDays(30),
                DenNgay = today.AddDays(30),
                LyDo = "Xin chuyển công tác về gần gia đình tại quê nhà.",
                NgayGui = DateTimeOffset.UtcNow.AddDays(-2),
                TrangThai = "CHO_DUYET"
            },
            new()
            {
                MaDon = 100,
                MaNhanVien = 7,
                TenNhanVien = "Ngô Bảo Châu",
                MaPhongBan = 4,
                TenPhongBan = "Bộ Phận Tiền Sảnh (FO)",
                TenChucVu = "Nhân Viên Lễ Tân",
                MaLoaiDon = 1,
                TenLoaiDon = "Nghỉ phép năm",
                CoHuongLuong = true,
                TuNgay = today.AddDays(-10),
                DenNgay = today.AddDays(-8),
                LyDo = "Nghỉ phép thường niên.",
                NgayGui = DateTimeOffset.UtcNow.AddDays(-15),
                TrangThai = "DA_DUYET",
                MaNguoiDuyet = 4,
                NguoiDuyetTen = "Lê Văn Lương",
                NgayDuyet = DateTimeOffset.UtcNow.AddDays(-14),
                PhanHoiDuyet = "Đồng ý, chúc em kỳ nghỉ vui vẻ."
            }
        };
    }
}
