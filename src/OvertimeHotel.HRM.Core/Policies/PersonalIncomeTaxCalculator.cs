namespace OvertimeHotel.HRM.Core.Policies;

public static class PersonalIncomeTaxCalculator
{
    /// <summary>
    /// Biểu thuế lũy tiến từng phần 5 bậc mới (áp dụng từ năm 2026).
    /// Bậc 1: Đến 10 triệu: 5% (trừ 0)
    /// Bậc 2: Trên 10 đến 30 triệu: 10% (trừ 0,5 triệu)
    /// Bậc 3: Trên 30 đến 60 triệu: 20% (trừ 3,5 triệu)
    /// Bậc 4: Trên 60 đến 100 triệu: 30% (trừ 9,5 triệu)
    /// Bậc 5: Trên 100 triệu: 35% (trừ 14,5 triệu)
    /// </summary>
    private static readonly (decimal MaxAmount, decimal Rate)[] TaxBrackets =
    [
        (10000000m, 0.05m),        // Bậc 1: Đến 10 triệu: 5%
        (30000000m, 0.10m),        // Bậc 2: Trên 10 đến 30 triệu: 10%
        (60000000m, 0.20m),        // Bậc 3: Trên 30 đến 60 triệu: 20%
        (100000000m, 0.30m),       // Bậc 4: Trên 60 đến 100 triệu: 30%
        (decimal.MaxValue, 0.35m)  // Bậc 5: Trên 100 triệu: 35%
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
