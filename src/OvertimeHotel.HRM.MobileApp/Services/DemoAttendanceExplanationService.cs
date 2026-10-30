using OvertimeHotel.HRM.Core.DTOs;

namespace OvertimeHotel.HRM.MobileApp.Services;

public class DemoAttendanceExplanationService : IAttendanceExplanationService
{
    private readonly IAuthService _authService;
    private readonly List<AttendanceExplanationItemDto> _mockItems = [];
    private int _nextId = 100;

    public bool IsDemoMode => true;

    public DemoAttendanceExplanationService(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var now = DateTimeOffset.UtcNow;
        _mockItems.Add(new AttendanceExplanationItemDto(
            MaGiaiTrinh: 1,
            MaPhanCa: 101,
            MaNhanVien: 10,
            HoTenNhanVien: "Đỗ Thành Đạt",
            TenPhongBan: "Ẩm Thực & Nhà Hàng",
            NgayLamViec: DateOnly.FromDateTime(DateTime.Today.AddDays(-2)),
            TenCa: "Ca Chiều (14:00 - 22:00)",
            GioDeXuatVao: now.AddDays(-2).Date.AddHours(14),
            GioDeXuatRa: now.AddDays(-2).Date.AddHours(22).AddMinutes(15),
            GioVaoThucTe: null,
            GioRaThucTe: now.AddDays(-2).Date.AddHours(22).AddMinutes(10),
            LyDo: "Quên quẹt thẻ lúc vào ca do hỗ trợ tiệc cưới gấp tại sảnh A.",
            MinhChung: "Đã báo giám sát ca lúc 14:05 qua tin nhắn.",
            TrangThai: "CHO_QUAN_LY",
            TrangThaiLabel: "Chờ Quản lý xác minh",
            NgayGui: now.AddDays(-1),
            MaQuanLyXacMinh: null,
            HoTenQuanLy: null,
            NgayQuanLyXacMinh: null,
            YKienQuanLy: null,
            MaHrXacNhan: null,
            HoTenHr: null,
            NgayHrXacNhan: null,
            YKienHr: null,
            CanWithdraw: true
        ));

        _mockItems.Add(new AttendanceExplanationItemDto(
            MaGiaiTrinh: 2,
            MaPhanCa: 102,
            MaNhanVien: 10,
            HoTenNhanVien: "Đỗ Thành Đạt",
            TenPhongBan: "Ẩm Thực & Nhà Hàng",
            NgayLamViec: DateOnly.FromDateTime(DateTime.Today.AddDays(-5)),
            TenCa: "Ca Sáng (06:00 - 14:00)",
            GioDeXuatVao: now.AddDays(-5).Date.AddHours(6),
            GioDeXuatRa: now.AddDays(-5).Date.AddHours(14).AddMinutes(30),
            GioVaoThucTe: now.AddDays(-5).Date.AddHours(6).AddMinutes(2),
            GioRaThucTe: null,
            LyDo: "Máy chấm công tầng 1 bảo trì lúc 14:00 không nhận thẻ ra.",
            MinhChung: "Có xác nhận của bảo vệ trực cổng nhân viên.",
            TrangThai: "DA_DUYET",
            TrangThaiLabel: "Đã duyệt",
            NgayGui: now.AddDays(-4),
            MaQuanLyXacMinh: 4,
            HoTenQuanLy: "Lê Văn Lương",
            NgayQuanLyXacMinh: now.AddDays(-4).AddHours(2),
            YKienQuanLy: "Xác nhận đúng sự việc, máy quét tầng 1 lỗi cổng giao tiếp.",
            MaHrXacNhan: 2,
            HoTenHr: "Phạm Thị Mai",
            NgayHrXacNhan: now.AddDays(-3),
            YKienHr: "Đã cập nhật giờ ra công thực tế.",
            CanWithdraw: false
        ));
    }

    public Task<List<EligibleShiftDto>> GetEligibleShiftsAsync(CancellationToken cancellationToken = default)
    {
        var shifts = new List<EligibleShiftDto>
        {
            new(
                MaPhanCa: 103,
                NgayLamViec: DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
                MaCa: 1,
                TenCa: "Ca Sáng (06:00 - 14:00)",
                GioBatDau: new TimeOnly(6, 0),
                GioKetThuc: new TimeOnly(14, 0),
                GioVaoThucTe: null,
                GioRaThucTe: DateTimeOffset.UtcNow.AddDays(-1).Date.AddHours(14),
                TrangThaiChamCong: "THIEU_CHECKIN"
            ),
            new(
                MaPhanCa: 104,
                NgayLamViec: DateOnly.FromDateTime(DateTime.Today.AddDays(-3)),
                MaCa: 2,
                TenCa: "Ca Chiều (14:00 - 22:00)",
                GioBatDau: new TimeOnly(14, 0),
                GioKetThuc: new TimeOnly(22, 0),
                GioVaoThucTe: DateTimeOffset.UtcNow.AddDays(-3).Date.AddHours(14).AddMinutes(5),
                GioRaThucTe: null,
                TrangThaiChamCong: "THIEU_CHECKOUT"
            )
        };
        return Task.FromResult(shifts);
    }

    public Task<List<AttendanceExplanationItemDto>> GetMyExplanationsAsync(CancellationToken cancellationToken = default)
    {
        lock (_mockItems)
        {
            return Task.FromResult(_mockItems.OrderByDescending(x => x.NgayGui).ToList());
        }
    }

    public Task<AttendanceExplanationItemDto?> GetExplanationDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        lock (_mockItems)
        {
            var item = _mockItems.FirstOrDefault(x => x.MaGiaiTrinh == id);
            return Task.FromResult(item);
        }
    }

    public Task<AttendanceExplanationOperationResult> SubmitExplanationAsync(CreateAttendanceExplanationRequest request)
    {
        lock (_mockItems)
        {
            var user = _authService.CurrentUser;
            var newId = ++_nextId;
            var item = new AttendanceExplanationItemDto(
                MaGiaiTrinh: newId,
                MaPhanCa: request.MaPhanCa,
                MaNhanVien: user?.MaNhanVien ?? 10,
                HoTenNhanVien: user?.HoTen ?? "Đỗ Thành Đạt",
                TenPhongBan: "Ẩm Thực & Nhà Hàng",
                NgayLamViec: DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
                TenCa: $"Ca #{request.MaPhanCa}",
                GioDeXuatVao: request.GioDeXuatVao,
                GioDeXuatRa: request.GioDeXuatRa,
                GioVaoThucTe: null,
                GioRaThucTe: null,
                LyDo: request.LyDo,
                MinhChung: request.MinhChung,
                TrangThai: "CHO_QUAN_LY",
                TrangThaiLabel: "Chờ Quản lý xác minh",
                NgayGui: DateTimeOffset.UtcNow,
                MaQuanLyXacMinh: null,
                HoTenQuanLy: null,
                NgayQuanLyXacMinh: null,
                YKienQuanLy: null,
                MaHrXacNhan: null,
                HoTenHr: null,
                NgayHrXacNhan: null,
                YKienHr: null,
                CanWithdraw: true
            );

            _mockItems.Insert(0, item);
            return Task.FromResult(new AttendanceExplanationOperationResult(true, "CREATED", "Gửi giải trình chấm công thành công.", newId));
        }
    }

    public Task<AttendanceExplanationOperationResult> WithdrawExplanationAsync(int id)
    {
        lock (_mockItems)
        {
            var idx = _mockItems.FindIndex(x => x.MaGiaiTrinh == id);
            if (idx < 0) return Task.FromResult(new AttendanceExplanationOperationResult(false, "NOT_FOUND", "Không tìm thấy giải trình."));

            var old = _mockItems[idx];
            if (old.TrangThai != "CHO_QUAN_LY")
            {
                if (old.TrangThai == "DA_HUY")
                    return Task.FromResult(new AttendanceExplanationOperationResult(false, "ALREADY_WITHDRAWN", "Giải trình đã rút trước đó."));
                return Task.FromResult(new AttendanceExplanationOperationResult(false, "CANNOT_WITHDRAW", "Giải trình đã được xử lý, không thể rút."));
            }

            _mockItems[idx] = old with { TrangThai = "DA_HUY", TrangThaiLabel = "Đã rút", CanWithdraw = false };
            return Task.FromResult(new AttendanceExplanationOperationResult(true, "WITHDRAWN", "Đã rút giải trình thành công."));
        }
    }
}
