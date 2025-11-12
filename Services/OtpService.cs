using ShopApI.DTOs;
using ShopApI.Helpers;
using ShopApI.Models;
using System.Security.Cryptography;
using System.Text;

namespace ShopApI.Services;

public class OtpService : IOtpService
{
    private const int OTP_LENGTH = 6;
    private const int MAX_ATTEMPTS = 3;
    private const int RATE_LIMIT_MAX = 5;
    private static readonly TimeSpan OTP_EXPIRY = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RATE_LIMIT_WINDOW = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RESEND_GUARD = TimeSpan.FromSeconds(30);

    private readonly ICacheService _cacheService;
    private readonly ILogger<OtpService> _logger;

    public OtpService(ICacheService cacheService, ILogger<OtpService> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<OtpIssuanceResult> GenerateOtpAsync(User user, string ipAddress)
    {
        await EnforceRateLimitAsync(ipAddress);

        var otp = GenerateSecureOtp();
        var hash = HashOtp(user.Id, otp);
        var envelope = new OtpEnvelope(user.Id, DateTime.UtcNow.Add(OTP_EXPIRY));

        await _cacheService.SetAsync(BuildOtpKey(user.Id), new CachedOtp(hash, envelope.ExpiresAt, user.Email), OTP_EXPIRY);
        await _cacheService.SetAsync(BuildAttemptKey(user.Id), 0, OTP_EXPIRY);
        await _cacheService.SetAsync(BuildResendKey(user.Id), DateTime.UtcNow, RESEND_GUARD);

        _logger.LogInformation("OTP generated for {Email}", MaskingHelper.MaskEmail(user.Email));

        return new OtpIssuanceResult(otp, envelope);
    }

    public async Task<OtpEnvelope?> ResendOtpAsync(User user)
    {
        var resendGuardKey = BuildResendKey(user.Id);
        if (await _cacheService.ExistsAsync(resendGuardKey))
        {
            _logger.LogWarning("Resend throttled for {Email}", MaskingHelper.MaskEmail(user.Email));
            return null;
        }

        var entry = await _cacheService.GetAsync<CachedOtp>(BuildOtpKey(user.Id));
        if (entry == null)
            return null;

        entry = entry with { ExpiresAt = DateTime.UtcNow.Add(OTP_EXPIRY) };
        await _cacheService.SetAsync(BuildOtpKey(user.Id), entry, OTP_EXPIRY);
        await _cacheService.SetAsync(BuildResendKey(user.Id), DateTime.UtcNow, RESEND_GUARD);

        _logger.LogInformation("OTP resent for {Email}", MaskingHelper.MaskEmail(user.Email));

        return new OtpEnvelope(user.Id, entry.ExpiresAt);
    }

    public async Task<bool> ValidateOtpAsync(Guid userId, string otp)
    {
        var entry = await _cacheService.GetAsync<CachedOtp>(BuildOtpKey(userId));
        if (entry == null)
            return false;

        var attempts = await _cacheService.GetAsync<int>(BuildAttemptKey(userId)) ?? 0;
        var hashed = HashOtp(userId, otp);
        var isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(entry.Hash),
            Encoding.UTF8.GetBytes(hashed));

        if (isValid)
        {
            await ClearOtpStateAsync(userId);
            return true;
        }

        attempts++;
        await _cacheService.SetAsync(BuildAttemptKey(userId), attempts, OTP_EXPIRY);

        if (attempts >= MAX_ATTEMPTS)
        {
            await ClearOtpStateAsync(userId);
            _logger.LogWarning("OTP attempts exceeded for user {UserId}", userId);
        }

        return false;
    }

    public async Task ClearOtpStateAsync(Guid userId)
    {
        await _cacheService.RemoveAsync(BuildOtpKey(userId));
        await _cacheService.RemoveAsync(BuildAttemptKey(userId));
        await _cacheService.RemoveAsync(BuildResendKey(userId));
    }

    private static string GenerateSecureOtp()
    {
        var number = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, OTP_LENGTH));
        return number.ToString($"D{OTP_LENGTH}");
    }

    private static string HashOtp(Guid userId, string otp)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes($"{userId}:{otp}");
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    private static string BuildOtpKey(Guid userId) => $"otp:{userId}";
    private static string BuildAttemptKey(Guid userId) => $"otp:attempts:{userId}";
    private static string BuildResendKey(Guid userId) => $"otp:resend:{userId}";
    private static string BuildRateKey(string ip) => $"rate:otp:{ip}";

    private async Task EnforceRateLimitAsync(string ipAddress)
    {
        var key = BuildRateKey(ipAddress);
        var attempts = await _cacheService.IncrementAsync(key, RATE_LIMIT_WINDOW);
        if (attempts > RATE_LIMIT_MAX)
        {
            throw new InvalidOperationException("OTP rate limit exceeded");
        }
    }

    private record CachedOtp(string Hash, DateTime ExpiresAt, string Destination);
}
