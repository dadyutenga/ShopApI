using Isopoh.Cryptography.Argon2;
using System.Security.Cryptography;
using System.Text;

namespace ShopApI.Helpers;

public static class PasswordHasher
{
    private const int SALT_LENGTH = 16;

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SALT_LENGTH);
        var config = BuildConfig(password, salt);
        var hash = Argon2.Hash(config);
        return $"{Convert.ToBase64String(salt)}.{hash.Base64String}";
    }

    public static bool VerifyPassword(string password, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 2)
            return false;

        var salt = Convert.FromBase64String(parts[0]);
        var config = BuildConfig(password, salt);
        return Argon2.Verify(config, parts[1]);
    }

    private static Argon2Config BuildConfig(string password, byte[] salt)
        => new()
        {
            Type = Argon2Type.Argon2id,
            Version = Argon2Version.Nineteen,
            TimeCost = 4,
            MemoryCost = 1 << 16,
            Lanes = 4,
            Threads = Environment.ProcessorCount,
            Salt = salt,
            Password = Encoding.UTF8.GetBytes(password)
        };
}
