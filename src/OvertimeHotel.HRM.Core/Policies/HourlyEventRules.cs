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
            var candidates = list.Where(h => h.TrangThai is "HIEU_LUC" or "HET_HAN" or "DA_KET_THUC"
                && h.NgayBatDau <= date && (!h.NgayKetThuc.HasValue || h.NgayKetThuc >= date))
                .OrderByDescending(h => h.NgayBatDau).ToList();
            if (candidates.Count == 0 || candidates[0].LuongCoBan <= 0
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
