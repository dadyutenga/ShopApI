using ShopApI.Models;
using ShopApI.Services;
using System.Security.Claims;

namespace ShopApI.IntegrationTests.TestDoubles;

public class FakeJwtService : IJwtService
{
    public Task<string> GenerateAccessTokenAsync(User user)
        => Task.FromResult($"token-{user.Id}");

    public string GenerateRefreshToken() => Guid.NewGuid().ToString();

    public ClaimsPrincipal? ValidateToken(string token) => null;

    public Task SaveRefreshTokenAsync(Guid userId, string token) => Task.CompletedTask;

    public Task<string?> ValidateRefreshTokenAsync(string token) => Task.FromResult<string?>(null);

    public Task RevokeRefreshTokenAsync(string token) => Task.CompletedTask;

    public Task BlacklistTokenAsync(string jti, TimeSpan expiry) => Task.CompletedTask;

    public Task<bool> IsTokenBlacklistedAsync(string jti) => Task.FromResult(false);
}
