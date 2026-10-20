namespace OvertimeHotel.HRM.Core.Policies;

/// <summary>Loại xung đột phát hiện được khi xếp một ca mới cho nhân viên.</summary>
public enum ShiftConflictKind
{
    /// <summary>Không có xung đột — được phép xếp ca.</summary>
    None = 0,

    /// <summary>Ca mới chồng giờ lên một ca đã xếp trong cùng ngày (hoặc kéo sang ngày kề).</summary>
    Overlap = 1,

    /// <summary>Khoảng nghỉ giữa ca trước và ca sau ngắn hơn mức tối thiểu luật định.</summary>
    InsufficientRest = 2
}

/// <summary>
/// Kết quả đối chiếu một ca dự kiến xếp với các ca đã có của cùng nhân viên.
/// </summary>
/// <param name="Kind">Loại xung đột.</param>
/// <param name="KetThucCaTruoc">Giờ kết thúc của ca đứng trước (chỉ có khi xung đột là nghỉ không đủ).</param>
/// <param name="BatDauCaSau">Giờ bắt đầu của ca đứng sau (chỉ có khi xung đột là nghỉ không đủ).</param>
/// <param name="GioNghi">Số giờ nghỉ thực tế giữa hai ca.</param>
/// <param name="ThongDiep">Câu thông báo tiếng Việt sẵn dùng cho giao diện.</param>
public record ShiftConflict(
    ShiftConflictKind Kind,
    DateTimeOffset? KetThucCaTruoc,
    DateTimeOffset? BatDauCaSau,
    double GioNghi,
    string? ThongDiep)
{
    public bool HasConflict => Kind != ShiftConflictKind.None;

    public static readonly ShiftConflict None = new(ShiftConflictKind.None, null, null, 0, null);
}

/// <summary>
/// Luật xếp ca dùng chung cho Web Admin, Mobile và tầng kiểm tra dữ liệu.
///
/// <para>
/// Hai ràng buộc ở đây đến thẳng từ PRD mục 3.2 và 3.3. Chúng được viết thành hàm thuần
/// (không truy vấn CSDL) để mọi nơi gọi cùng một đoạn logic: <c>PhanCaController</c> khi
/// phân ca tay, xếp ca nhanh cả tuần, kéo thả trên lưới ma trận, và sau này là luồng đổi
/// ca 3.4. Trước đây mỗi action tự viết một phép kiểm tra riêng nên chỉ chặn được ca
/// trùng đúng mã ca, còn ca chồng giờ và ca sát giờ thì lọt.
/// </para>
///
/// <para>
/// <b>Vì sao dùng <see cref="DateTimeOffset"/> chứ không phải <c>DateOnly</c>:</b> ca đêm
/// bắt đầu ngày D và kết thúc ngày D+1. Nếu so theo ngày làm việc thì ca đêm hôm trước và
/// ca sáng hôm sau trông như hai ngày khác nhau, trong khi thực tế nhân viên chỉ được nghỉ
/// vài tiếng. So trên trục thời gian tuyệt đối mới bắt đúng.
/// </para>
/// </summary>
public static class ShiftSchedulingPolicy
{
    /// <summary>
    /// Số giờ nghỉ tối thiểu giữa hai ca liên tiếp — Điều 110 Bộ luật Lao động 2019
    /// (làm việc theo ca liên tục được nghỉ ít nhất 12 giờ; ca đêm được nghỉ ít nhất 8 giờ
    /// trước khi chuyển sang ca mới). Đề tài chọn mức chặt hơn là 8 giờ cho mọi ca để bảo
    /// đảm sức khỏe nhân sự khách sạn vận hành 24/7.
    /// </summary>
    public const double MIN_REST_HOURS = 8d;

    /// <summary>Trích dẫn pháp lý hiển thị kèm thông báo chặn.</summary>
    public const string REST_RULE_CITATION = "Điều 110 Bộ luật Lao động 2019";

    /// <summary>Độ lệch cho phép khi so mốc thời gian, tránh sai số dấu phẩy động.</summary>
    private static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(1);

    /// <summary>Khoảng thời gian của một ca đã có, dùng làm đầu vào cho <see cref="ValidateAssignment"/>.</summary>
    public readonly record struct ShiftWindow(DateTimeOffset BatDau, DateTimeOffset KetThuc);

    /// <summary>Hai ca chồng giờ nhau hay không. Ca chạm mép (kết thúc đúng lúc ca kia bắt đầu) không tính là chồng.</summary>
    public static bool Overlaps(DateTimeOffset batDauA, DateTimeOffset ketThucA, DateTimeOffset batDauB, DateTimeOffset ketThucB)
        => batDauA + Tolerance < ketThucB && ketThucA > batDauB + Tolerance;

    /// <summary>
    /// Đối chiếu một ca dự kiến xếp với toàn bộ ca hiện có của nhân viên.
    /// </summary>
    /// <param name="existingShifts">
    /// Các ca đang có của nhân viên. Khi sửa/đổi ca, người gọi phải loại bỏ chính ca sắp bị
    /// thay thế khỏi danh sách này, nếu không ca mới sẽ "chồng" lên chính nó.
    /// </param>
    /// <param name="newStart">Giờ bắt đầu dự kiến của ca mới.</param>
    /// <param name="newEnd">Giờ kết thúc dự kiến của ca mới.</param>
    public static ShiftConflict ValidateAssignment(
        IEnumerable<ShiftWindow> existingShifts,
        DateTimeOffset newStart,
        DateTimeOffset newEnd)
    {
        if (newEnd <= newStart)
        {
            return new ShiftConflict(
                ShiftConflictKind.Overlap, null, null, 0,
                "Giờ kết thúc ca phải sau giờ bắt đầu.");
        }

        ShiftConflict? restConflict = null;

        foreach (var shift in existingShifts)
        {
            // 1. Chồng giờ — chặn ngay, không cần xét tiếp.
            if (Overlaps(newStart, newEnd, shift.BatDau, shift.KetThuc))
            {
                return new ShiftConflict(
                    ShiftConflictKind.Overlap,
                    shift.BatDau,
                    shift.KetThuc,
                    0,
                    $"Nhân viên đã có ca {Format(shift.BatDau)} – {Format(shift.KetThuc)} trùng giờ với ca đang xếp ({Format(newStart)} – {Format(newEnd)}). " +
                    "Mỗi nhân viên chỉ được xếp một ca tại một thời điểm.");
            }

            // 2. Khoảng nghỉ giữa hai ca — lấy vi phạm nặng nhất (nghỉ ít nhất) để báo.
            double gapHours;
            DateTimeOffset truoc, sau;

            if (shift.KetThuc <= newStart)
            {
                truoc = shift.KetThuc;
                sau = newStart;
                gapHours = (newStart - shift.KetThuc).TotalHours;
            }
            else
            {
                truoc = newEnd;
                sau = shift.BatDau;
                gapHours = (shift.BatDau - newEnd).TotalHours;
            }

            if (gapHours < MIN_REST_HOURS)
            {
                if (restConflict == null || gapHours < restConflict.GioNghi)
                {
                    restConflict = new ShiftConflict(
                        ShiftConflictKind.InsufficientRest,
                        truoc,
                        sau,
                        Math.Round(gapHours, 2),
                        $"Khoảng nghỉ giữa ca kết thúc {Format(truoc)} và ca bắt đầu {Format(sau)} chỉ có " +
                        $"{FormatHours(gapHours)} giờ, thấp hơn mức tối thiểu {MIN_REST_HOURS:0} giờ theo {REST_RULE_CITATION}. " +
                        "Vui lòng dời ca để nhân viên có đủ thời gian nghỉ.");
                }
            }
        }

        return restConflict ?? ShiftConflict.None;
    }

    /// <summary>
    /// Định dạng mốc thời gian theo giờ Việt Nam để thông báo đọc được ngay trên giao diện.
    /// </summary>
    public static string Format(DateTimeOffset value)
        => value.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm dd/MM/yyyy");

    private static string FormatHours(double hours)
        => hours <= 0 ? "0" : hours.ToString("0.#");
}
