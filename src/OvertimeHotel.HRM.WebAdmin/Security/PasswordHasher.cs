using System.Security.Cryptography;
using System.Text;

namespace OvertimeHotel.HRM.WebAdmin.Security;

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(password)))
            .ToLowerInvariant();
    }
}
