using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Core.Policies;

public static class PayrollBudgetPolicy
{
    public static bool CanTransition(string state, string action, int creator, int actor) => actor > 0 && ((state, action) switch
    {
        ("DRAFT" or "TU_CHOI", "submit") => true,
        ("CHO_DUYET", "approve") => creator != actor,
        ("CHO_DUYET", "reject") => creator != actor,
        ("DA_DUYET", "lock") => true,
        _ => false
    });

    public sealed record Summary(decimal Total, int MissingSlots, int ExpectedSlots)
    {
        public bool Complete => ExpectedSlots > 0 && MissingSlots == 0;
    }

    // Monthly department budgets are additive; do not mix annual or whole-hotel rows into them.
    public static Summary Summarize(IEnumerable<NganSachLuong> rows, int year, int[] months, int[] departments)
    {
        var approved = rows.Where(r => r.Nam == year && r.Thang.HasValue && r.MaPhongBan.HasValue
            && r.TrangThai is "DA_DUYET" or "KHOA")
            .ToDictionary(r => (r.Thang!.Value, r.MaPhongBan!.Value));
        decimal total = 0; var missing = 0;
        foreach (var month in months.Distinct())
            foreach (var department in departments.Distinct())
                if (approved.TryGetValue((month, department), out var row)) total += row.SoTienNganSach;
                else missing++;
        return new(total, missing, months.Distinct().Count() * departments.Distinct().Count());
    }
}
