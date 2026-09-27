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
                LeaveApprovalPolicy.CanView(user.TenVaiTro, user.MaPhongBan, r.MaPhongBan, r.MaLoaiDon)).ToList();

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
                phanHoi: phanHoi
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
