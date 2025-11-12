using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ShopApI.Helpers;

public static class BootstrapTokenHelper
{
    public static string SignToken(BootstrapTokenPayload payload, string secret)
    {
        var json = JsonSerializer.Serialize(payload);
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var signature = ComputeSignature(data, secret);
        return $"{data}.{signature}";
    }

    public static BootstrapTokenPayload? TryParse(string token, string secret)
    {
        var parts = token.Split('.');
        if (parts.Length != 2)
            return null;

        var expected = ComputeSignature(parts[0], secret);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(parts[1]), Convert.FromBase64String(expected)))
            return null;

        var payload = JsonSerializer.Deserialize<BootstrapTokenPayload>(Encoding.UTF8.GetString(Convert.FromBase64String(parts[0])));
        if (payload == null || payload.ExpiresAt <= DateTimeOffset.UtcNow)
            return null;

        return payload;
    }

    private static string ComputeSignature(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(bytes);
    }
}

public record BootstrapTokenPayload(string Email, string Password, DateTimeOffset ExpiresAt);
