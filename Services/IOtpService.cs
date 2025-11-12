namespace ShopApI.Services;

public interface IOtpService
{
    Task<OtpDescriptor> GenerateAsync(Guid userId, string email, string purpose);
    Task<OtpValidationResult> ValidateAsync(Guid userId, string otp);
    Task<bool> RefreshAsync(Guid userId);
    Task<bool> CanResendAsync(Guid userId);
}

public record OtpDescriptor(string Code, DateTime ExpiresAt);

public enum OtpValidationResult
{
    Valid,
    Invalid,
    Expired,
    AttemptsExceeded
}
