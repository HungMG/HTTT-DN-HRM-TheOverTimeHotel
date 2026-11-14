using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace OvertimeHotel.HRM.MobileApp.Services;

public static class ProfileContactValidation
{
    public static string? Validate(string? phone, string? email, string? address)
    {
        phone = phone?.Trim() ?? ""; email = email?.Trim() ?? ""; address = address?.Trim() ?? "";
        if (!Regex.IsMatch(phone, @"\A(?:0|\+84)[35789][0-9]{8}\z", RegexOptions.CultureInvariant))
            return "Số điện thoại phải có dạng 0912345678 hoặc +84912345678, không chứa khoảng trắng.";
        if (email.Length == 0 || email.Length > 100 || !new EmailAddressAttribute().IsValid(email))
            return "Vui lòng nhập email cá nhân hợp lệ, tối đa 100 ký tự. Ví dụ: ten@gmail.com.";
        if (address.Length == 0) return "Vui lòng nhập địa chỉ.";
        if (address.Length > 255) return "Địa chỉ không được quá 255 ký tự.";
        return null;
    }
}
