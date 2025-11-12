using ShopApI.Models;
using System.Security.Claims;

namespace ShopApI.Services;

public interface IJwtService
{
    Task<string> GenerateAccessTokenAsync(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
    Task SaveRefreshTokenAsync(Guid userId, string token);
    Task<string?> ValidateRefreshTokenAsync(string token);
    Task RevokeRefreshTokenAsync(string token);
    Task BlacklistTokenAsync(string jti, TimeSpan expiry);
    Task<bool> IsTokenBlacklistedAsync(string jti);
}
