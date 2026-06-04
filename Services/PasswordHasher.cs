using System.Security.Cryptography;
using System.Text;

namespace ChatSupport.Services;

/// <summary>
/// Minimal salted SHA-256 hasher. Good enough for an MVP demo; for production
/// swap in ASP.NET Core Identity / PBKDF2 / bcrypt.
/// </summary>
public static class PasswordHasher
{
    public static string Hash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("chat-support-salt::" + password));
        return Convert.ToHexString(bytes);
    }

    public static bool Verify(string password, string hash) =>
        string.Equals(Hash(password), hash, StringComparison.OrdinalIgnoreCase);
}
