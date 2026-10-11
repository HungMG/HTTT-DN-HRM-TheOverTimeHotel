namespace OvertimeHotel.HRM.Core.Policies;

/// <summary>
/// Báo rằng bảng công của CHÍNH người đang đăng nhập không tồn tại và cũng không
/// thể nạp được — tách khỏi lỗi "không đủ quyền xem".
///
/// <para>
/// Tài khoản quản trị (Admin) và bộ phận CNTT (IT) được cấp quyền đăng nhập nhưng
/// không có hồ sơ lao động trong <c>nhan_vien</c>: họ quản trị hệ thống chứ không
/// trực. Trước đây trang Công &amp; nghỉ phép của họ hiện lỗi chung chung
/// "không có quyền" hoặc treo vòng xoay. Riêng loại lỗi này cần một kiểu riêng để
/// tầng UI biết mà đổi tiêu đề thông báo và ẩn nút "Thử lại" — thử lại sẽ luôn
/// ra cùng kết quả vì nguyên nhân không nằm ở kết nối.
/// </para>
///
/// <para>
/// Không dùng cách bắt lỗi bằng so khớp nội dung chuỗi thông báo: sửa câu chữ
/// ở bất kỳ đâu là hỏng. Kiểu lỗi là hợp đồng, chuỗi là diễn giải.
/// </para>
/// </summary>
public sealed class AttendanceSummaryUnavailableException : InvalidOperationException
{
    public AttendanceSummaryUnavailableException()
        : base("Tài khoản Quản trị viên không áp dụng lịch phân ca và chấm công.")
    {
    }

    public AttendanceSummaryUnavailableException(string message) : base(message)
    {
    }
}
