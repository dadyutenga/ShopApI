namespace ShopApI.DTOs;

public record RegisterRequest(
    string Email,
    string Password,
    string? FullName = null
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
