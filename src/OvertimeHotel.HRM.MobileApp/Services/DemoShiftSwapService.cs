using OvertimeHotel.HRM.Core.DTOs;

namespace OvertimeHotel.HRM.MobileApp.Services;

public class DemoShiftSwapService : IShiftSwapService
{
    private readonly IAuthService _authService;
    private readonly List<ShiftSwapItemDto> _mockItems = [];
    private int _nextId = 200;

    public bool IsDemoMode => true;

    public DemoShiftSwapService(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var now = DateTimeOffset.UtcNow;
        var myId = _authService.CurrentUser?.MaNhanVien ?? 10;

        // 1. Đơn đồng nghiệp gửi đến CẦN MÌNH XÁC NHẬN
        _mockItems.Add(new ShiftSwapItemDto
        {
            MaYeuCau = 1,
            MaPhanCaGui = 101,
            TenCaGui = "Ca Chiều (14:00 - 22:00)",
            NgayCaGui = DateTime.Today.AddDays(2).ToString("dd/MM/yyyy"),
            GioCaGui = "14:00 - 22:00",
            MaNhanVienGui = 12,
            TenNguoiGui = "Trần Thị Bích",
            MaPhanCaNhan = 102,
            TenCaNhan = "Ca Sáng (06:00 - 14:00)",
            NgayCaNhan = DateTime.Today.AddDays(2).ToString("dd/MM/yyyy"),
            GioCaNhan = "06:00 - 14:00",
            MaNhanVienNhan = myId,
            TenNguoiNhan = "Đỗ Thành Đạt",
            LyDo = "Nhà có việc đột xuất buổi sáng, nhờ Đạt đổi giúp ca sáng sang ca chiều.",
            TrangThai = "CHO_NGUOI_NHAN",
            NgayGui = now.AddHours(-3),
            IsSender = false,
            IsReceiver = true
        });

        // 2. Đơn mình gửi CHỜ ĐỒNG NGHIỆP XÁC NHẬN
        _mockItems.Add(new ShiftSwapItemDto
        {
            MaYeuCau = 2,
            MaPhanCaGui = 103,
            TenCaGui = "Ca Sáng (06:00 - 14:00)",
            NgayCaGui = DateTime.Today.AddDays(4).ToString("dd/MM/yyyy"),
            GioCaGui = "06:00 - 14:00",
            MaNhanVienGui = myId,
            TenNguoiGui = "Đỗ Thành Đạt",
            MaPhanCaNhan = 104,
            TenCaNhan = "Ca Chiều (14:00 - 22:00)",
            NgayCaNhan = DateTime.Today.AddDays(4).ToString("dd/MM/yyyy"),
            GioCaNhan = "14:00 - 22:00",
            MaNhanVienNhan = 15,
            TenNguoiNhan = "Lê Hoàng Nam",
            LyDo = "Đi khám bệnh theo lịch hẹn vào buổi sáng.",
            TrangThai = "CHO_NGUOI_NHAN",
            NgayGui = now.AddHours(-8),
            IsSender = true,
            IsReceiver = false
        });

        // 3. Đơn đã được đồng nghiệp đồng ý, CHỜ QUẢN LÝ DUYỆT
        _mockItems.Add(new ShiftSwapItemDto
        {
            MaYeuCau = 3,
            MaPhanCaGui = 105,
            TenCaGui = "Ca Đêm (22:00 - 06:00)",
            NgayCaGui = DateTime.Today.AddDays(5).ToString("dd/MM/yyyy"),
            GioCaGui = "22:00 - 06:00",
            MaNhanVienGui = myId,
            TenNguoiGui = "Đỗ Thành Đạt",
            MaPhanCaNhan = 106,
            TenCaNhan = "Ca Sáng (06:00 - 14:00)",
            NgayCaNhan = DateTime.Today.AddDays(6).ToString("dd/MM/yyyy"),
            GioCaNhan = "06:00 - 14:00",
            MaNhanVienNhan = 18,
            TenNguoiNhan = "Phạm Minh Tuấn",
            LyDo = "Hỗ trợ đổi ca theo thỏa thuận trước.",
            TrangThai = "CHO_DUYET",
            PhanHoiNguoiNhan = "Đồng ý trực thay ca đêm.",
            NgayNguoiNhanPhanHoi = now.AddHours(-1),
            NgayGui = now.AddDays(-1),
            IsSender = true,
            IsReceiver = false
        });

        // 4. Đơn ĐÃ DUYỆT thành công
        _mockItems.Add(new ShiftSwapItemDto
        {
            MaYeuCau = 4,
            MaPhanCaGui = 107,
            TenCaGui = "Ca Chiều (14:00 - 22:00)",
            NgayCaGui = DateTime.Today.AddDays(-3).ToString("dd/MM/yyyy"),
            GioCaGui = "14:00 - 22:00",
            MaNhanVienGui = myId,
            TenNguoiGui = "Đỗ Thành Đạt",
            MaPhanCaNhan = 108,
            TenCaNhan = "Ca Sáng (06:00 - 14:00)",
            NgayCaNhan = DateTime.Today.AddDays(-3).ToString("dd/MM/yyyy"),
            GioCaNhan = "06:00 - 14:00",
            MaNhanVienNhan = 12,
            TenNguoiNhan = "Trần Thị Bích",
            LyDo = "Đổi ca trực hỗ trợ sự kiện.",
            TrangThai = "DA_DUYET",
            PhanHoiNguoiNhan = "Nhất trí đổi ca.",
            NgayNguoiNhanPhanHoi = now.AddDays(-4),
            MaNguoiDuyet = 2,
            TenNguoiDuyet = "Nguyễn Văn Quản Lý",
            NgayDuyet = now.AddDays(-3),
            PhanHoiDuyet = "Đã kiểm tra khoảng nghỉ, hợp lệ.",
            NgayGui = now.AddDays(-4),
            IsSender = true,
            IsReceiver = false
        });
    }

    public Task<List<EligibleMyShiftDto>> GetEligibleMyShiftsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var list = new List<EligibleMyShiftDto>
        {
            new()
            {
                MaPhanCa = 201,
                NgayLamViec = DateOnly.FromDateTime(today.AddDays(1)),
                TenCa = "Ca Sáng",
                GioBatDau = "06:00",
                GioKetThuc = "14:00",
                BatDauDuKien = today.AddDays(1).AddHours(6),
                KetThucDuKien = today.AddDays(1).AddHours(14)
            },
            new()
            {
                MaPhanCa = 202,
                NgayLamViec = DateOnly.FromDateTime(today.AddDays(3)),
                TenCa = "Ca Chiều",
                GioBatDau = "14:00",
                GioKetThuc = "22:00",
                BatDauDuKien = today.AddDays(3).AddHours(14),
                KetThucDuKien = today.AddDays(3).AddHours(22)
            }
        };
        return Task.FromResult(list);
    }

    public Task<List<EligiblePartnerShiftDto>> GetEligiblePartnerShiftsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var list = new List<EligiblePartnerShiftDto>
        {
            new()
            {
                MaPhanCa = 301,
                MaNhanVien = 12,
                TenNhanVien = "Trần Thị Bích",
                NgayLamViec = DateOnly.FromDateTime(today.AddDays(1)),
                TenCa = "Ca Chiều",
                GioBatDau = "14:00",
                GioKetThuc = "22:00",
                BatDauDuKien = today.AddDays(1).AddHours(14),
                KetThucDuKien = today.AddDays(1).AddHours(22)
            },
            new()
            {
                MaPhanCa = 302,
                MaNhanVien = 15,
                TenNhanVien = "Lê Hoàng Nam",
                NgayLamViec = DateOnly.FromDateTime(today.AddDays(3)),
                TenCa = "Ca Sáng",
                GioBatDau = "06:00",
                GioKetThuc = "14:00",
                BatDauDuKien = today.AddDays(3).AddHours(6),
                KetThucDuKien = today.AddDays(3).AddHours(14)
            }
        };
        return Task.FromResult(list);
    }

    public Task<List<ShiftSwapItemDto>> GetMyShiftSwapsAsync(CancellationToken cancellationToken = default)
    {
        var myId = _authService.CurrentUser?.MaNhanVien ?? 10;
        foreach (var item in _mockItems)
        {
            item.IsSender = item.MaNhanVienGui == myId;
            item.IsReceiver = item.MaNhanVienNhan == myId;
        }

        var sorted = _mockItems
            .OrderBy(x => (x.IsReceiver && x.TrangThai == "CHO_NGUOI_NHAN") ? 0 :
                          (x.TrangThai == "CHO_DUYET" ? 1 :
                          (x.TrangThai == "CHO_NGUOI_NHAN" ? 2 : 3)))
            .ThenByDescending(x => x.NgayGui)
            .ToList();

        return Task.FromResult(sorted);
    }

    public Task<ShiftSwapItemDto?> GetShiftSwapDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = _mockItems.FirstOrDefault(x => x.MaYeuCau == id);
        return Task.FromResult(item);
    }

    public Task<ShiftSwapOperationResult> CreateShiftSwapAsync(CreateShiftSwapRequest request)
    {
        var myId = _authService.CurrentUser?.MaNhanVien ?? 10;
        var myName = _authService.CurrentUser?.HoTen ?? "Đỗ Thành Đạt";

        var newItem = new ShiftSwapItemDto
        {
            MaYeuCau = _nextId++,
            MaPhanCaGui = request.MaPhanCaGui,
            TenCaGui = "Ca đã chọn",
            NgayCaGui = DateTime.Today.AddDays(1).ToString("dd/MM/yyyy"),
            GioCaGui = "06:00 - 14:00",
            MaNhanVienGui = myId,
            TenNguoiGui = myName,
            MaPhanCaNhan = request.MaPhanCaNhan,
            TenCaNhan = "Ca nhận thay",
            NgayCaNhan = DateTime.Today.AddDays(1).ToString("dd/MM/yyyy"),
            GioCaNhan = "14:00 - 22:00",
            MaNhanVienNhan = 12,
            TenNguoiNhan = "Trần Thị Bích",
            LyDo = request.LyDo.Trim(),
            TrangThai = "CHO_NGUOI_NHAN",
            NgayGui = DateTimeOffset.UtcNow,
            IsSender = true,
            IsReceiver = false
        };

        _mockItems.Insert(0, newItem);

        return Task.FromResult(new ShiftSwapOperationResult
        {
            Success = true,
            Message = "Đã gửi yêu cầu đổi ca. Chờ đồng nghiệp xác nhận.",
            MaYeuCau = newItem.MaYeuCau
        });
    }

    public Task<ShiftSwapOperationResult> RespondShiftSwapAsync(int id, RespondShiftSwapRequest request)
    {
        var item = _mockItems.FirstOrDefault(x => x.MaYeuCau == id);
        if (item == null)
        {
            return Task.FromResult(new ShiftSwapOperationResult { Success = false, Message = "Không tìm thấy yêu cầu đổi ca." });
        }

        if (item.TrangThai != "CHO_NGUOI_NHAN")
        {
            return Task.FromResult(new ShiftSwapOperationResult { Success = false, Message = "Yêu cầu đã được xử lý hoặc bị rút trước đó." });
        }

        var now = DateTimeOffset.UtcNow;
        if (request.DongY)
        {
            item.TrangThai = "CHO_DUYET";
            item.PhanHoiNguoiNhan = request.LyDo;
            item.NgayNguoiNhanPhanHoi = now;
            return Task.FromResult(new ShiftSwapOperationResult
            {
                Success = true,
                Message = "Bạn đã đồng ý đổi ca. Yêu cầu chuyển lên Quản lý duyệt.",
                MaYeuCau = id
            });
        }
        else
        {
            item.TrangThai = "TU_CHOI";
            item.BenTuChoi = "NGUOI_NHAN";
            item.PhanHoiNguoiNhan = request.LyDo;
            item.NgayNguoiNhanPhanHoi = now;
            return Task.FromResult(new ShiftSwapOperationResult
            {
                Success = true,
                Message = "Bạn đã từ chối yêu cầu đổi ca.",
                MaYeuCau = id
            });
        }
    }

    public Task<ShiftSwapOperationResult> WithdrawShiftSwapAsync(int id)
    {
        var item = _mockItems.FirstOrDefault(x => x.MaYeuCau == id);
        if (item == null)
        {
            return Task.FromResult(new ShiftSwapOperationResult { Success = false, Message = "Không tìm thấy yêu cầu đổi ca." });
        }

        if (item.TrangThai != "CHO_NGUOI_NHAN" && item.TrangThai != "CHO_DUYET")
        {
            return Task.FromResult(new ShiftSwapOperationResult { Success = false, Message = "Yêu cầu đã được xử lý, không thể rút." });
        }

        item.TrangThai = "DA_HUY";
        return Task.FromResult(new ShiftSwapOperationResult
        {
            Success = true,
            Message = "Rút yêu cầu đổi ca thành công.",
            MaYeuCau = id
        });
    }
}
