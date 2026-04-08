using System.Security.Cryptography;
using System.Text;

namespace MuaythaiApp.Api.Security;

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public static bool Verify(string password, string expectedHash)
    {
        return string.Equals(Hash(password), expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}
