using ShopApI.Models;
using System.Security.Claims;

namespace ShopApI.Services;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
    Task<RefreshToken> SaveRefreshTokenAsync(Guid userId, string token);
    Task<RefreshToken?> ValidateRefreshTokenAsync(string token);
    Task RevokeRefreshTokenAsync(string token);
}
