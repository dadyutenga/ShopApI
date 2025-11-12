using System.Security.Cryptography;

namespace ShopApI.Services;

public class OtpService : IOtpService
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<OtpService> _logger;
    private const int OTP_LENGTH = 6;
    private static readonly TimeSpan OTP_EXPIRY = TimeSpan.FromMinutes(5);

    public OtpService(ICacheService cacheService, ILogger<OtpService> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<string> GenerateOtpAsync(string email)
    {
        var otp = GenerateSecureOtp();
        var key = $"otp:{email}";
        
        await _cacheService.SetAsync(key, otp, OTP_EXPIRY);
        
        _logger.LogInformation("OTP generated for {Email}", email);
        
        return otp;
    }

    public async Task<bool> ValidateOtpAsync(string email, string otp)
    {
        var key = $"otp:{email}";
        var storedOtp = await _cacheService.GetAsync<string>(key);
        
        if (storedOtp == null)
        {
            _logger.LogWarning("OTP not found or expired for {Email}", email);
            return false;
        }

        var isValid = storedOtp == otp;
        
        if (isValid)
        {
            await _cacheService.RemoveAsync(key);
            _logger.LogInformation("OTP validated successfully for {Email}", email);
        }
        else
        {
            _logger.LogWarning("Invalid OTP attempt for {Email}", email);
        }

        return isValid;
    }

    public async Task InvalidateOtpAsync(string email)
    {
        var key = $"otp:{email}";
        await _cacheService.RemoveAsync(key);
    }

    private static string GenerateSecureOtp()
    {
        var number = RandomNumberGenerator.GetInt32(0, 1000000);
        return number.ToString("D6");
    }
}
