namespace OvertimeHotel.HRM.Core.Policies;

/// <summary>
/// Luật đổi ca giữa hai nhân viên (PRD 3.4).
///
/// <para>
/// Đổi ca là phép hoán đổi hai bản ghi <c>phan_ca</c>: A trực ca đã nhường của B,
/// B trực ca đã nhường của A. Vì hai ca giữ nguyên khung giờ, chỉ đổi người, nên
/// ràng buộc cần kiểm tra không nằm trên chính cặp ca đó mà nằm trên <b>lịch còn
/// lại của mỗi người</b>: sau khi đổi, A có chồng giờ ca nào khác không, có bị
/// nghỉ dưới 8 tiếng so với ca liền kề không — và ngược lại với B.
/// </para>
///
/// <para>
/// Bỏ bước này thì luồng duyệt chỉ cần một cú bấm là tạo ra lịch vi phạm Điều 110
/// BLLĐ, trong khi màn xếp ca tay vẫn chặn đúng — hai cửa vào cùng một bảng dữ
/// liệu mà một cửa không có luật.
/// </para>
/// </summary>
public static class ShiftSwapPolicy
{
    /// <summary>Kết quả đối soát một yêu cầu đổi ca trước khi duyệt.</summary>
    /// <param name="IsValid">Được phép đổi ca hay không.</param>
    /// <param name="ThongDiep">Câu giải thích tiếng Việt khi bị chặn; null khi hợp lệ.</param>
    public readonly record struct SwapValidation(bool IsValid, string? ThongDiep)
    {
        public static readonly SwapValidation Valid = new(true, null);

        public static SwapValidation Invalid(string thongDiep) => new(false, thongDiep);
    }

    /// <summary>
    /// Đối soát yêu cầu đổi ca.
    /// </summary>
    /// <param name="caNguoiGuiNhanVe">
    /// Khung giờ của ca mà <b>người gửi</b> sẽ trực sau khi đổi (tức ca của người nhận).
    /// </param>
    /// <param name="caNguoiNhanNhanVe">
    /// Khung giờ của ca mà <b>người nhận</b> sẽ trực sau khi đổi (tức ca của người gửi).
    /// </param>
    /// <param name="caKhacCuaNguoiGui">
    /// Các ca còn lại của người gửi, <b>đã loại</b> ca đang đem nhường.
    /// </param>
    /// <param name="caKhacCuaNguoiNhan">
    /// Các ca còn lại của người nhận, <b>đã loại</b> ca đang đem nhường.
    /// </param>
    /// <param name="tenNguoiGui">Tên hiển thị của người gửi, dùng ghép câu thông báo.</param>
    /// <param name="tenNguoiNhan">Tên hiển thị của người nhận, dùng ghép câu thông báo.</param>
    public static SwapValidation Validate(
        ShiftSchedulingPolicy.ShiftWindow caNguoiGuiNhanVe,
        ShiftSchedulingPolicy.ShiftWindow caNguoiNhanNhanVe,
        IEnumerable<ShiftSchedulingPolicy.ShiftWindow> caKhacCuaNguoiGui,
        IEnumerable<ShiftSchedulingPolicy.ShiftWindow> caKhacCuaNguoiNhan,
        string tenNguoiGui,
        string tenNguoiNhan)
    {
        // Người gửi nhận ca của người nhận.
        var loiNguoiGui = ShiftSchedulingPolicy.ValidateAssignment(
            caKhacCuaNguoiGui, caNguoiGuiNhanVe.BatDau, caNguoiGuiNhanVe.KetThuc);

        if (loiNguoiGui.HasConflict)
        {
            return SwapValidation.Invalid(
                $"Không thể đổi ca: sau khi đổi, {tenNguoiGui} sẽ vi phạm quy định xếp ca. {loiNguoiGui.ThongDiep}");
        }

        // Người nhận nhận ca của người gửi.
        var loiNguoiNhan = ShiftSchedulingPolicy.ValidateAssignment(
            caKhacCuaNguoiNhan, caNguoiNhanNhanVe.BatDau, caNguoiNhanNhanVe.KetThuc);

        if (loiNguoiNhan.HasConflict)
        {
            return SwapValidation.Invalid(
                $"Không thể đổi ca: sau khi đổi, {tenNguoiNhan} sẽ vi phạm quy định xếp ca. {loiNguoiNhan.ThongDiep}");
        }

        return SwapValidation.Valid;
    }
}
