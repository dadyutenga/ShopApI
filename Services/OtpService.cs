using System.Security.Cryptography;
using System.Text;

namespace ShopApI.Services;

public class OtpService : IOtpService
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<OtpService> _logger;
    private const int OtpLength = 6;
    private const int MaxAttempts = 3;
    private static readonly TimeSpan OtpExpiry = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ResendGuard = TimeSpan.FromSeconds(30);

    public OtpService(ICacheService cacheService, ILogger<OtpService> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<OtpDescriptor> GenerateAsync(Guid userId, string email, string purpose)
    {
        var code = GenerateSecureOtp();
        var payload = new CachedOtp(Hash(code), purpose, DateTime.UtcNow);
        await _cacheService.SetAsync(GetOtpKey(userId), payload, OtpExpiry);
        await _cacheService.SetAsync(GetAttemptKey(userId), 0, OtpExpiry);
        await _cacheService.SetAsync(GetMetaKey(userId), new OtpMeta(payload.IssuedAt), OtpExpiry);

        _logger.LogInformation("OTP generated for {Email} purpose {Purpose}", email, purpose);
        return new OtpDescriptor(code, payload.IssuedAt.Add(OtpExpiry));
    }

    public async Task<OtpValidationResult> ValidateAsync(Guid userId, string otp)
    {
        var payload = await _cacheService.GetAsync<CachedOtp>(GetOtpKey(userId));
        if (payload == null)
        {
            return OtpValidationResult.Expired;
        }

        var attempts = await _cacheService.GetAsync<int>(GetAttemptKey(userId));
        if (attempts >= MaxAttempts)
        {
            return OtpValidationResult.AttemptsExceeded;
        }

        var incomingHash = Hash(otp);
        if (CryptographicOperations.FixedTimeEquals(Convert.FromHexString(payload.Hash), Convert.FromHexString(incomingHash)))
        {
            await _cacheService.RemoveAsync(GetOtpKey(userId));
            await _cacheService.RemoveAsync(GetAttemptKey(userId));
            await _cacheService.RemoveAsync(GetMetaKey(userId));
            return OtpValidationResult.Valid;
        }

        attempts++;
        await _cacheService.SetAsync(GetAttemptKey(userId), attempts, OtpExpiry);
        return attempts >= MaxAttempts ? OtpValidationResult.AttemptsExceeded : OtpValidationResult.Invalid;
    }

    public async Task<bool> RefreshAsync(Guid userId)
    {
        var payload = await _cacheService.GetAsync<CachedOtp>(GetOtpKey(userId));
        if (payload == null)
        {
            return false;
        }

        payload = payload with { IssuedAt = DateTime.UtcNow };
        await _cacheService.SetAsync(GetOtpKey(userId), payload, OtpExpiry);
        await _cacheService.SetAsync(GetMetaKey(userId), new OtpMeta(payload.IssuedAt), OtpExpiry);
        return true;
    }

    public async Task<bool> CanResendAsync(Guid userId)
    {
        var meta = await _cacheService.GetAsync<OtpMeta>(GetMetaKey(userId));
        if (meta == null)
        {
            return true;
        }

        return DateTime.UtcNow - meta.LastSent >= ResendGuard;
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static string GenerateSecureOtp()
    {
        var number = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, OtpLength));
        return number.ToString($"D{OtpLength}");
    }

    private static string GetOtpKey(Guid userId) => $"otp:{userId}";
    private static string GetMetaKey(Guid userId) => $"otp:{userId}:meta";
    private static string GetAttemptKey(Guid userId) => $"otp:{userId}:attempts";

    private record CachedOtp(string Hash, string Purpose, DateTime IssuedAt);
    private record OtpMeta(DateTime LastSent);
}
