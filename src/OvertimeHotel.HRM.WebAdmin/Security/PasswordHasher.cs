using System.Security.Cryptography;
using System.Text;

namespace OvertimeHotel.HRM.WebAdmin.Security;

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return string.Empty;
        }

        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(password)))
            .ToLowerInvariant();
    }
}
