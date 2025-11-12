namespace ShopApI.Services;

public interface IOtpService
{
    Task<string> GenerateOtpAsync(string email);
    Task<bool> ValidateOtpAsync(string email, string otp);
    Task InvalidateOtpAsync(string email);
}
