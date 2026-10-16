namespace OvertimeHotel.HRM.Core.Policies;

public static class PersonalIncomeTaxCalculator
{
    /// <summary>
    /// Bậc thuế lũy tiến từng phần theo tháng (Luật Thuế TNCN Việt Nam).
    /// </summary>
    private static readonly (decimal MaxAmount, decimal Rate)[] TaxBrackets =
    [
        (5000000m, 0.05m),   // Bậc 1: <= 5tr: 5%
        (10000000m, 0.10m),  // Bậc 2: > 5tr - 10tr: 10%
        (18000000m, 0.15m),  // Bậc 3: > 10tr - 18tr: 15%
        (32000000m, 0.20m),  // Bậc 4: > 18tr - 32tr: 20%
        (52000000m, 0.25m),  // Bậc 5: > 32tr - 52tr: 25%
        (80000000m, 0.30m),  // Bậc 6: > 52tr - 80tr: 30%
        (decimal.MaxValue, 0.35m) // Bậc 7: > 80tr: 35%
    ];

    /// <summary>
    /// Tính tiền thuế TNCN theo biểu thuế lũy tiến từng phần.
    /// </summary>
    /// <param name="thuNhapTinhThue">Thu nhập tính thuế sau khi đã trừ giảm trừ gia cảnh và bảo hiểm.</param>
    /// <returns>Tiền thuế TNCN làm tròn AwayFromZero.</returns>
    public static decimal CalculateTax(decimal thuNhapTinhThue)
    {
        if (thuNhapTinhThue <= 0m)
        {
            return 0m;
        }

        var remaining = thuNhapTinhThue;
        var previousLimit = 0m;
        var totalTax = 0m;

        foreach (var (bracketLimit, rate) in TaxBrackets)
        {
            var span = bracketLimit - previousLimit;
            if (remaining <= span)
            {
                totalTax += remaining * rate;
                break;
            }

            totalTax += span * rate;
            remaining -= span;
            previousLimit = bracketLimit;
        }

        return Math.Round(totalTax, MidpointRounding.AwayFromZero);
    }
}
