namespace OvertimeHotel.HRM.Core.DTOs;

public record LeaveRequestCreateDto(
    int MaNhanVien,
    int MaLoaiDon,
    DateOnly TuNgay,
    DateOnly DenNgay,
    string LyDo
);

/// <summary>
/// Kết quả nộp đơn từ phía nhân viên (TSK-18).
/// </summary>
/// <remarks>
/// <b>Vì sao không trả về bool trần.</b> Người dùng cần biết đơn có được tạo
/// không, và nếu không thì vì sao — mã lỗi <c>Code</c> để ViewModel chuyển thành
/// thông điệp phù hợp (lỗi hiển thị ngay trên form, còn lỗi hệ thống mới mở popup).
/// </remarks>
public record LeaveSubmitResult(
    bool Success,
    string Code,
    string Message,
    int MaDon = 0
);

/// <summary>
/// Một loại đơn người dùng được chọn khi nộp, kèm mô tả luật đi kèm đúng loại đó.
/// </summary>
/// <remarks>
/// Danh mục 5 loại nằm trong bảng <c>LOAI_DON</c> của CSDL chứ không ghi cứng ở
/// app: nếu CSDL thêm loại mới thì form tự hiện, không phải sửa lại code.
/// </remarks>
public record LeaveTypeOption(
    int MaLoaiDon,
    string TenLoaiDon,
    bool CoHuongLuong,
    string MoTa
);

/// <summary>
/// DTO gửi từ client để duyệt hoặc từ chối đơn.
/// Lưu ý bảo mật: Không truyền MaNguoiDuyet từ client; Server tự trích xuất từ JWT token.
/// </summary>
public record LeaveReviewRequest(
    bool Approved,
    string? PhanHoi
);

/// <summary>
/// Kết quả phản hồi xử lý duyệt đơn từ Server.
/// </summary>
public record LeaveReviewResult(
    bool Success,
    string Code,
    string Message
);

/// <summary>
/// DTO chi tiết một đơn từ hiển thị trong danh sách duyệt của Mobile App.
/// </summary>
public record LeaveRequestListItemDto(
    int MaDon,
    int MaNhanVien,
    string HoTenNhanVien,
    string? ChucVu,
    int? MaPhongBan,
    string? TenPhongBan,
    int MaLoaiDon,
    string TenLoaiDon,
    bool CoHuongLuong,
    DateOnly TuNgay,
    DateOnly DenNgay,
    string LyDo,
    DateTimeOffset NgayGui,
    string TrangThai,
    string? PhanHoiDuyet,
    DateTimeOffset? NgayDuyet,
    string? HoTenNguoiDuyet
);

/// <summary>
/// Response gộp danh sách đơn và số lượng chờ duyệt trong 1 request.
/// </summary>
public record LeaveRequestListResponse(
    List<LeaveRequestListItemDto> Items,
    int PendingCount,
    string Source,
    bool HasMore = false
);

/// <summary>
/// Legacy DTO được giữ lại cho tương thích ngược nội bộ.
/// </summary>
public record LeaveRequestReviewDto(
    int MaDon,
    int MaNguoiDuyet,
    bool Approved,
    string? PhanHoi
);
