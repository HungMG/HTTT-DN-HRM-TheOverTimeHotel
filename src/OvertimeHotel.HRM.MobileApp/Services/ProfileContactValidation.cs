using System.Net.Mail;

namespace OvertimeHotel.HRM.MobileApp.Services;

public static class ProfileContactValidation
{
    public static string? Validate(string? phone, string? email, string? address)
    {
        phone = phone?.Trim() ?? ""; email = email?.Trim() ?? ""; address = address?.Trim() ?? "";
        if (phone.Length is < 1 or > 20 || phone.Any(c => !char.IsAsciiDigit(c) && c is not ('+' or ' ' or '-' or '(' or ')')) || !phone.Any(char.IsAsciiDigit))
            return "Số điện thoại không hợp lệ (tối đa 20 ký tự).";
        if (email.Length > 100 || (email.Length > 0 && (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)))
            return "Email cá nhân không hợp lệ (tối đa 100 ký tự).";
        if (address.Length > 255) return "Địa chỉ không được quá 255 ký tự.";
        return null;
    }
}
