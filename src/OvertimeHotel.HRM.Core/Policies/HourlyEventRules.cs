using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Core.Policies;

public record EventPaySlice(DateTimeOffset Start, DateTimeOffset End, decimal MonthlySalary, int ContractId,
    decimal Rate = SalaryConstants.EVENT_HOURLY_RATE)
{
    public decimal Minutes => (decimal)(End - Start).Ticks / TimeSpan.TicksPerMinute;

    // Tiền sự kiện = Lương tháng / 26 / 8 × Số giờ tham gia sự kiện × hệ số (lưu kèm căn cứ để không hồi tố)
    public decimal HourlyRate => MonthlySalary / SalaryConstants.STANDARD_WORKING_DAYS
        / SalaryConstants.STANDARD_WORKING_HOURS_PER_DAY * Rate;

    public decimal UnroundedAmount => MonthlySalary * Rate * Minutes
        / (SalaryConstants.STANDARD_WORKING_DAYS * SalaryConstants.STANDARD_WORKING_HOURS_PER_DAY * 60m);
}

public static class HourlyEventRules
{
    public static string? AutomaticRating(PhanCongDuAn assignment)
    {
        var planned = assignment.KetThuc - assignment.BatDau;
        if (planned <= TimeSpan.Zero || assignment.CongThucTes.Count == 0 ||
            assignment.CongThucTes.Any(w => w.BatDau < assignment.BatDau || w.KetThuc > assignment.KetThuc || w.KetThuc <= w.BatDau))
            return null;
        var ticks = assignment.CongThucTes.Sum(w => (decimal)(w.KetThuc - w.BatDau).Ticks);
        return ticks * 3 >= (decimal)planned.Ticks * 2 ? "XUAT_SAC" : "HOAN_THANH";
    }

    public static bool CanRecordWork(DuAn item, PhanCongDuAn assignment, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now)
    {
        var shift = assignment.PhanCa;
        return assignment.MaDuAn == item.MaDuAn && assignment.TrangThaiThamGia is not ("HUY_PHAN_CONG" or "DA_HUY")
            && shift != null && shift.DaCongBo && shift.TrangThai != "DA_HUY"
            && assignment.MaPhanCa == shift.MaPhanCa && shift.MaNhanVien == assignment.MaNhanVien
            && start < end && end <= now && start >= assignment.BatDau && end <= assignment.KetThuc
            && start >= shift.BatDauDuKien && end <= shift.KetThucDuKien
            && start >= item.NgayBatDau && end <= item.NgayKetThuc
            && (!item.KetThucThucTe.HasValue || end <= item.KetThucThucTe);
    }

    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    public static DateOnly WorkMonth(DateTimeOffset instant)
    { var local = HotelEventRules.Local(instant); return new(local.Year, local.Month, 1); }

    public static (DateTimeOffset Start, DateTimeOffset End)? Intersect(DateTimeOffset start, DateTimeOffset end,
        DateTimeOffset otherStart, DateTimeOffset otherEnd)
    {
        var from = start > otherStart ? start : otherStart;
        var to = end < otherEnd ? end : otherEnd;
        return from < to ? (from, to) : null;
    }

    /// <summary>
    /// Tính khoảng thời gian làm việc thực tế cho sự kiện từ phân công, ca nguồn và bản ghi chấm công.
    /// Giới hạn chặt chẽ theo giờ hết ca nguồn (không phát sinh OT ngoài ca) và đối soát giờ quẹt thẻ thực tế.
    ///
    /// <para>
    /// Xử lý "quên quẹt ra" (đã có <paramref name="checkIn"/> nhưng
    /// <paramref name="checkOut"/> null): tự chốt theo giờ kết thúc ca nguồn
    /// (<paramref name="shiftEnd"/>) thay vì trả null (tính 0 tiền). Đầu ra kèm
    /// <c>TuChotTheoCa</c> để nơi hiển thị có thể cảnh báo cho người dùng.
    /// </para>
    /// </summary>
    public static (DateTimeOffset Start, DateTimeOffset End, bool TuChotTheoCa)? ResolveActualWindow(
        DateTimeOffset assignmentStart, DateTimeOffset assignmentEnd,
        DateTimeOffset shiftStart, DateTimeOffset shiftEnd,
        DateTimeOffset? checkIn, DateTimeOffset? checkOut)
    {
        if (!checkIn.HasValue)
            return null;

        var effectiveCheckOut = checkOut ?? shiftEnd;
        if (effectiveCheckOut <= checkIn.Value)
            return null;

        var shiftWindow = Intersect(assignmentStart, assignmentEnd, shiftStart, shiftEnd);
        if (!shiftWindow.HasValue)
            return null;

        var window = Intersect(shiftWindow.Value.Start, shiftWindow.Value.End, checkIn.Value, effectiveCheckOut);
        if (!window.HasValue)
            return null;

        return (window.Value.Start, window.Value.End, !checkOut.HasValue);
    }

    public static (DateTimeOffset Start, DateTimeOffset End)? AttendanceWindow(DuAn item, PhanCongDuAn assignment, DateTimeOffset now)
    {
        var shift = assignment.PhanCa;
        if (shift?.ChamCong == null) return null;
        var window = ResolveActualWindow(assignment.BatDau, assignment.KetThuc, shift.BatDauDuKien, shift.KetThucDuKien,
            shift.ChamCong.GioCheckIn, shift.ChamCong.GioCheckOut);
        if (!window.HasValue) return null;
        var end = item.KetThucThucTe ?? item.NgayKetThuc;
        var clipped = Intersect(window.Value.Start, window.Value.End, item.NgayBatDau, end);
        return clipped.HasValue && CanRecordWork(item, assignment, clipped.Value.Start, clipped.Value.End, now)
            ? (clipped.Value.Start, clipped.Value.End)
            : null;
    }

    public static decimal Amount(IEnumerable<EventPaySlice> work)
    {
        var slices = work.OrderBy(s => s.Start).ToList();
        decimal total = 0;
        DateTimeOffset? previousEnd = null;
        foreach (var slice in slices)
        {
            if (slice.Start >= slice.End || slice.MonthlySalary <= 0 || !HotelEventRules.IsMoney(slice.MonthlySalary)
                || previousEnd > slice.Start) throw new ArgumentException("Công sự kiện hoặc căn cứ lương không hợp lệ/trùng giờ.");
            total += slice.UnroundedAmount;
            previousEnd = slice.End;
        }
        var rounded = decimal.Round(total, 0, MidpointRounding.AwayFromZero);
        if (!HotelEventRules.IsMoney(rounded)) throw new ArgumentException("Tiền sự kiện vượt giới hạn lưu trữ.");
        return rounded;
    }

    public static IReadOnlyList<EventPaySlice> Price(DateTimeOffset start, DateTimeOffset end, IEnumerable<HopDong> contracts,
        decimal? rate = null)
    {
        if (start >= end) throw new ArgumentException("Khoảng công phải có thời lượng lớn hơn 0.");
        var hourlyRate = rate is > 0 ? rate.Value : SalaryConstants.EVENT_HOURLY_RATE;
        var list = contracts.ToList();
        var result = new List<EventPaySlice>();
        for (var from = start; from < end;)
        {
            var date = DateOnly.FromDateTime(HotelEventRules.Local(from));
            var candidates = list.Where(h => h.TrangThai is "HIEU_LUC" or "HET_HAN" or "DA_KET_THUC" or "DA_CHAM_DUT" or "CHAM_DUT"
                && h.NgayBatDau <= date && (!h.NgayKetThuc.HasValue || h.NgayKetThuc >= date))
                .OrderByDescending(h => h.NgayBatDau).ToList();
            if (candidates.Count == 0 || candidates[0].LuongCoBan <= 0
                || (candidates[0].TrangThai is "DA_CHAM_DUT" or "CHAM_DUT" && !candidates[0].NgayKetThuc.HasValue)
                || candidates.Count(h => h.NgayBatDau == candidates[0].NgayBatDau) != 1)
                throw new ArgumentException($"Thiếu hoặc trùng căn cứ lương ngày {date:dd/MM/yyyy}.");
            var nextDay = HotelEventRules.ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue));
            var to = nextDay < end ? nextDay : end;
            result.Add(new(from, to, candidates[0].LuongCoBan, candidates[0].MaHopDong, hourlyRate));
            from = to;
        }
        return result;
    }
}
