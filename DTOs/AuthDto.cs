using ShopApI.Enums;

namespace ShopApI.DTOs;

public record RegisterRequest(
    string Email,
    string Password,
    UserRole Role = UserRole.Customer
);

public record VerifyOtpRequest(
    string Email,
    string Otp
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
    DateTime CreatedAt
);

public record UpdateProfileRequest(
    string? Email = null,
    string? CurrentPassword = null,
    string? NewPassword = null
);
