using ShopApI.Enums;

namespace ShopApI.DTOs;

public record RegisterRequest(
    string Email,
    string Password,
    UserRole Role = UserRole.Customer,
    string? PhoneNumber = null
);

public record VerifyOtpRequest(
    string Email,
    string Otp
);

public record OtpRequest(
    string Email
);

public record OtpVerifyRequest(
    string Email,
    string Otp
);

public record BootstrapStatusResponse(
    bool IsLocked,
    bool BootstrapEnabled,
    bool HasAdminUsers
);

public record BootstrapCompleteRequest(
    string? Email,
    string? Password,
    string? SetupToken
);

public record LoginRequest(
    string Email,
    string Password
);

public record RefreshTokenRequest(
    string RefreshToken
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

public record RegisterResponse(
    string Message,
    string Email
);

public record UserDto(
    Guid Id,
    string Email,
    string Role,
    string? Provider,
    DateTime CreatedAt,
    bool IsActive,
    bool IsEmailVerified,
    bool IsPhoneVerified,
    bool TwoFactorEnabled
);

public record UpdateProfileRequest(
    string? Email = null,
    string? CurrentPassword = null,
    string? NewPassword = null,
    string? PhoneNumber = null
);

public record RegisterManagerRequest(string Email, string TemporaryPassword);
public record RegisterSupportRequest(string Email, string TemporaryPassword);
public record RegisterCustomerRequest(string Email, string? PhoneNumber);

public record UpdateUserRoleRequest(UserRole Role);
public record UpdateUserStatusRequest(bool IsActive);

public record OAuthCallbackResult(
    bool RequiresVerification,
    string? VerificationLink,
    AuthResponse? AuthResponse
);
