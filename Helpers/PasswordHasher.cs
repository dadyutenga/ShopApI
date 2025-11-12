using System.Security.Cryptography;

namespace ShopApI.Helpers;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 150_000;

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA512);
        var hash = pbkdf2.GetBytes(KeySize);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}.{Iterations}";
    }

    public static bool Verify(string password, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length < 3)
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[0]);
        var storedHash = Convert.FromBase64String(parts[1]);
        var iterations = int.TryParse(parts[2], out var parsed) ? parsed : Iterations;

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA512);
        var computed = pbkdf2.GetBytes(KeySize);
        return CryptographicOperations.FixedTimeEquals(storedHash, computed);
    }
}
