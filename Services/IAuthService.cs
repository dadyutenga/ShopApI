using ShopApI.DTOs;

namespace ShopApI.Services;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request);
    Task<bool> VerifyEmailAsync(VerifyOtpRequest request);
    Task<bool> VerifyEmailLinkAsync(string token);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task<UserDto?> GetUserByIdAsync(Guid userId);
    Task<bool> UpdateUserAsync(Guid userId, UpdateProfileRequest request);
    Task DeactivateUserAsync(Guid userId);
    Task<bool> IsAccountLockedAsync(string email);
    Task IncrementFailedLoginAsync(string email);
    Task ResetFailedLoginAsync(string email);
    Task RequestCustomerOtpAsync(OtpRequest request, string ipAddress);
    Task ResendCustomerOtpAsync(OtpRequest request);
    Task<AuthResponse> VerifyCustomerOtpAsync(OtpVerifyRequest request, string ipAddress);
}
