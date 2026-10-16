using System.Text;

namespace OvertimeHotel.HRM.Core.Utilities;

public static class VietnameseNumberToWords
{
    private static readonly string[] Digits =
        ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];

    private static readonly string[] Units =
        ["", "nghìn", "triệu", "tỷ", "nghìn tỷ", "triệu tỷ"];

    public static string ConvertToWords(decimal amount)
    {
        var value = (long)Math.Round(amount, MidpointRounding.AwayFromZero);
        if (value == 0) return "Không đồng";
        if (value < 0) return $"Âm {ConvertToWords(-value).ToLowerInvariant()}";

        var sNumber = value.ToString();
        var groups = new List<string>();

        for (var i = sNumber.Length; i > 0; i -= 3)
        {
            var start = Math.Max(0, i - 3);
            var len = i - start;
            groups.Add(sNumber.Substring(start, len));
        }

        var result = new StringBuilder();
        for (var i = groups.Count - 1; i >= 0; i--)
        {
            var groupVal = int.Parse(groups[i]);
            if (groupVal == 0) continue;

            var groupWords = ReadThreeDigits(groupVal, i < groups.Count - 1);
            result.Append(groupWords).Append(' ').Append(Units[i]).Append(' ');
        }

        var text = result.ToString().Trim();
        // Chuẩn hóa khoảng trắng kép
        while (text.Contains("  "))
        {
            text = text.Replace("  ", " ");
        }

        if (string.IsNullOrEmpty(text)) return "Không đồng";

        // Viết hoa chữ cái đầu và thêm "đồng"
        return char.ToUpper(text[0]) + text[1..] + " đồng";
    }

    private static string ReadThreeDigits(int number, bool readZeroHundred)
    {
        var hundreds = number / 100;
        var tens = (number % 100) / 10;
        var units = number % 10;
        var sb = new StringBuilder();

        if (hundreds > 0 || readZeroHundred)
        {
            sb.Append(Digits[hundreds]).Append(" trăm ");
        }

        if (tens > 1)
        {
            sb.Append(Digits[tens]).Append(" mươi ");
            if (units == 1) sb.Append("mốt ");
            else if (units == 5) sb.Append("lăm ");
            else if (units > 0) sb.Append(Digits[units]).Append(' ');
        }
        else if (tens == 1)
        {
            sb.Append("mười ");
            if (units == 5) sb.Append("lăm ");
            else if (units > 0) sb.Append(Digits[units]).Append(' ');
        }
        else if (tens == 0 && units > 0)
        {
            if (hundreds > 0 || readZeroHundred) sb.Append("lẻ ");
            sb.Append(Digits[units]).Append(' ');
        }

        return sb.ToString().Trim();
    }
}
