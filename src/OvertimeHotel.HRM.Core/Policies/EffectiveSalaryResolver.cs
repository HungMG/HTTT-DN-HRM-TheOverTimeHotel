using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Core.Policies;

/// <summary>Salary and organization effective on the work date; probation salary is already stored at 85%.</summary>
public sealed record EffectiveSalary(HopDong Contract, PhuLucLuong? Amendment, decimal MonthlySalary,
    int? PositionId, int? DepartmentId)
{
    public bool IsProbation => Contract.HinhThucHopDong.Contains("THU_VIEC", StringComparison.OrdinalIgnoreCase)
        || Contract.LoaiHopDong.Contains("thử việc", StringComparison.OrdinalIgnoreCase)
        || Contract.LoaiHopDong.Contains("THU_VIEC", StringComparison.OrdinalIgnoreCase);
}

public sealed record SalarySegment(DateOnly From, DateOnly To, EffectiveSalary Salary)
{
    /// <summary>
    /// Định mức quỹ lương tham chiếu quy đổi theo ngày công chuẩn (26 ngày/tháng).
    /// Công thức: Định mức tháng = Lương chuẩn theo hợp đồng × (Ngày công tính định mức / 26).
    /// </summary>
    public decimal CalendarProratedAmount
    {
        get
        {
            decimal total = 0m;
            for (var number = From.DayNumber; number <= To.DayNumber;)
            {
                var date = DateOnly.FromDayNumber(number);
                var monthStart = new DateOnly(date.Year, date.Month, 1);
                var monthEnd = new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
                var end = Math.Min(monthEnd.DayNumber, To.DayNumber);

                // Trọn cả tháng: hưởng trọn 100% định mức tháng (26/26 công)
                if (number == monthStart.DayNumber && end == monthEnd.DayNumber)
                {
                    total += Salary.MonthlySalary;
                }
                else
                {
                    // Phân đoạn lẻ trong tháng: tính theo số ngày công tiêu chuẩn (trừ Chủ nhật) / 26 ngày công chuẩn
                    var workDays = 0;
                    for (var d = number; d <= end; d++)
                    {
                        if (DateOnly.FromDayNumber(d).DayOfWeek != DayOfWeek.Sunday)
                        {
                            workDays++;
                        }
                    }

                    total += Salary.MonthlySalary * workDays / SalaryConstants.STANDARD_WORKING_DAYS;
                }

                number = end + 1;
            }
            return Math.Round(total, 2, MidpointRounding.AwayFromZero);
        }
    }
}

public static class EffectiveSalaryResolver
{
    public static EffectiveSalary? Resolve(IEnumerable<HopDong> contracts, DateOnly date)
    {
        var matches = contracts.Where(h => Covers(h, date)).ToList();
        if (matches.Count > 1)
            throw new InvalidOperationException($"Trùng hợp đồng hiệu lực ngày {date:dd/MM/yyyy}; cần HR đối soát trước khi tính lương.");
        if (matches.Count == 0) return null;
        var contract = matches[0];
        var amendments = contract.PhuLucLuongs.Where(a => a.NgayHieuLuc <= date)
            .OrderBy(a => a.NgayHieuLuc).ToList();
        if (amendments.Any(a => a.NgayHieuLuc < contract.NgayBatDau || a.NgayHieuLuc.Day != 1)
            || amendments.GroupBy(a => a.NgayHieuLuc).Any(g => g.Count() > 1))
            throw new InvalidOperationException($"Phụ lục hợp đồng #{contract.MaHopDong} có ngày hiệu lực không hợp lệ hoặc trùng nhau.");
        var amendment = amendments.LastOrDefault();
        var salary = amendment?.LuongCoBanMoi ?? contract.LuongCoBan;
        if (salary <= 0)
            throw new InvalidOperationException($"Thiếu mức lương hợp lệ ngày {date:dd/MM/yyyy} (hợp đồng #{contract.MaHopDong}).");
        // A subsequent salary-only appendix must retain the previous organizational move.
        var positionId = amendments.LastOrDefault(a => a.MaChucVuMoi.HasValue)?.MaChucVuMoi ?? contract.MaChucVuKyKet;
        var departmentId = amendments.LastOrDefault(a => a.MaPhongBanMoi.HasValue)?.MaPhongBanMoi ?? contract.MaPhongBanKyKet;
        return new(contract, amendment, salary, positionId, departmentId);
    }

    public static IReadOnlyList<SalarySegment> BuildSegments(IEnumerable<HopDong> contracts, DateOnly from, DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber > 365)
            throw new ArgumentException("Khoảng tính lương phải từ 1 đến 366 ngày.");
        var list = contracts.ToList();
        var result = new List<SalarySegment>();
        for (var number = from.DayNumber; number <= to.DayNumber; number++)
        {
            var date = DateOnly.FromDayNumber(number);
            var salary = Resolve(list, date);
            if (salary == null) continue;
            if (result.LastOrDefault() is { } previous && previous.To.DayNumber + 1 == number && previous.Salary == salary)
                result[^1] = previous with { To = date };
            else
                result.Add(new(date, date, salary));
        }
        return result;
    }

    private static bool Covers(HopDong contract, DateOnly date) =>
        contract.TrangThai is "HIEU_LUC" or "HET_HAN" or "DA_KET_THUC" or "DA_CHAM_DUT" or "CHAM_DUT" or "DA_KY"
        && contract.NgayBatDau <= date && (!contract.NgayKetThuc.HasValue || date <= contract.NgayKetThuc)
        // Terminated legacy records without an end cannot prove historical coverage.
        && !(contract.TrangThai is "DA_CHAM_DUT" or "CHAM_DUT" && !contract.NgayKetThuc.HasValue);
}
