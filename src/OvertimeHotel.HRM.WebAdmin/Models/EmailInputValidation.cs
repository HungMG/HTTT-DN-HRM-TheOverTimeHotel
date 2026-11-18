using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace OvertimeHotel.HRM.WebAdmin.Models;

public static class EmailInputValidation
{
    // Shared by HTML pattern, client validation and server validation. No fixed TLD list.
    public const string Pattern = @"[^\s@]+@[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?)+";
    public const string Message = "Email phải có dạng ten@tenmien.duoi, ví dụ ten@gmail.com hoặc ten@congty.vn; không chứa khoảng trắng.";

    public static bool IsValid(string? value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 100 && new EmailAddressAttribute().IsValid(value)
        && Regex.IsMatch(value, @"\A(?:" + Pattern + @")\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
