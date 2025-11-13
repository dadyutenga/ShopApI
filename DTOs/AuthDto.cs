using ShopApI.Enums;

namespace ShopApI.DTOs;

public record RegisterRequest(
    string Email,
    string Password,
    UserRole Role = UserRole.Customer
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
    bool IsActive,
    bool IsEmailVerified,
    DateTime CreatedAt,
    CustomerProfileDto? Customer
);

public record CustomerProfileDto(
    string? PhoneNumber,
    bool IsPhoneVerified,
    bool IsEmailVerified,
    bool TwoFactorEnabled
);

public record UpdateProfileRequest(
    string? Email = null,
    string? CurrentPassword = null,
    string? NewPassword = null
);

public record BootstrapCompleteRequest(
    string? Email,
    string? Password,
    string? SetupToken
);

public record BootstrapStatusResponse(
    bool IsBootstrapAllowed,
    string Reason
);

public record RequestOtpRequest(string Email);
public record ResendOtpRequest(string Email);
public record VerifyOtpRequest(string Email, string Otp);

public record OtpEnvelope(Guid UserId, DateTime ExpiresAt);
public record OtpIssuanceResult(string Otp, OtpEnvelope Envelope);

public record AdminRegisterManagerRequest(string Email, string Password);
public record AdminRegisterSupportRequest(string Email, string Password);
public record AdminRegisterCustomerRequest(string Email, string? PhoneNumber, bool TwoFactorEnabled = false);

public record UserQueryParameters(string? Email = null, UserRole? Role = null, bool? IsActive = null);

public record UpdateUserRoleRequest(UserRole Role);
public record UpdateUserStatusRequest(bool IsActive);
public record SoftDeleteUserRequest(string? Reason);

public record EmailVerificationRequest(string Token);
public record OAuthPendingResponse(string Email, string VerificationLink);
