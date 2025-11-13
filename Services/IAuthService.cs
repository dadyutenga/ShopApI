using ShopApI.DTOs;

namespace ShopApI.Services;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task<UserDto?> GetUserByIdAsync(Guid userId);
    Task<bool> UpdateUserAsync(Guid userId, UpdateProfileRequest request);
    Task DeactivateUserAsync(Guid userId);
    Task<bool> IsAccountLockedAsync(string email);
    Task IncrementFailedLoginAsync(string email);
    Task ResetFailedLoginAsync(string email);
    Task<OtpEnvelope> RequestCustomerOtpAsync(RequestOtpRequest request, string ipAddress);
    Task<OtpEnvelope> ResendCustomerOtpAsync(ResendOtpRequest request);
    Task<AuthResponse> VerifyCustomerOtpAsync(VerifyOtpRequest request);
    Task<bool> VerifyEmailFromTokenAsync(string token);
}
