using System.Text;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Core.Policies;

/// <summary>Matches salary grades without changing the employee's recorded qualification.</summary>
public static class SalaryBandLookup
{
    public static string? EducationGrade(string? education)
    {
        var value = education?.Trim().Normalize(NormalizationForm.FormC);
        if (string.IsNullOrEmpty(value)) return null;
        // Only explicit degree prefixes qualify. Do not infer a degree from certificates,
        // job titles or unsupported higher qualifications.
        foreach (var grade in new[] { "Trung cấp", "Cao đẳng", "Đại học" })
            if (string.Equals(value, grade, StringComparison.OrdinalIgnoreCase)
                || value.StartsWith(grade + " ", StringComparison.OrdinalIgnoreCase)) return grade;
        return null;
    }

    public static KhungLuongCapBac? Find(IEnumerable<KhungLuongCapBac> bands, string? group, string? education, int tenureMonths)
    {
        if (group == null || tenureMonths < 0) return null;
        var grade = EducationGrade(education);
        return bands.SingleOrDefault(x => x.NhomChucVu == group &&
            (group != "NHAN_VIEN" || (grade != null && x.TrinhDo == grade)) &&
            (x.ThamNienTuThang == null || x.ThamNienTuThang <= tenureMonths) &&
            (x.ThamNienDenThang == null || tenureMonths < x.ThamNienDenThang));
    }
}
