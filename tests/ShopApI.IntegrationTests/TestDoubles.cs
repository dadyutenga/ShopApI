using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShopApI.Data;
using ShopApI.DTOs;
using ShopApI.Enums;
using ShopApI.Models;
using ShopApI.Services;
using System.Collections.Generic;
using System.Security.Claims;

namespace ShopApI.IntegrationTests;

internal static class TestHelpers
{
    public static ApplicationDbContext CreateContext(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new ApplicationDbContext(options);
    }
}

internal class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, (object Value, DateTime? ExpiresAt)> _store = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
            {
                _store.Remove(key);
                return Task.FromResult<T?>(default);
            }
            return Task.FromResult((T?)entry.Value);
        }
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        _store[key] = (value!, expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : null);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.ContainsKey(key));

    public async Task<long> IncrementAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var current = await GetAsync<long>(key) ?? 0;
        current++;
        await SetAsync(key, current, expiry);
        return current;
    }
}

internal class FakeJwtService : IJwtService
{
    private readonly Dictionary<string, Guid> _refresh = new();

    public Task<string> GenerateAccessTokenAsync(User user) => Task.FromResult("access-token");
    public string GenerateRefreshToken() => "refresh-token";
    public ClaimsPrincipal? ValidateToken(string token) => null;
    public Task SaveRefreshTokenAsync(Guid userId, string token)
    {
        _refresh[token] = userId;
        return Task.CompletedTask;
    }
    public Task<string?> ValidateRefreshTokenAsync(string token)
        => Task.FromResult(_refresh.TryGetValue(token, out var id) ? id.ToString() : null);
    public Task RevokeRefreshTokenAsync(string token)
    {
        _refresh.Remove(token);
        return Task.CompletedTask;
    }
    public Task BlacklistTokenAsync(string jti, TimeSpan expiry) => Task.CompletedTask;
    public Task<bool> IsTokenBlacklistedAsync(string jti) => Task.FromResult(false);
}

internal class FakeEventPublisher : IEventPublisher
{
    public List<object> Events { get; } = new();

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        Events.Add(message!);
        return Task.CompletedTask;
    }
}

internal class SpyOtpService : IOtpService
{
    private readonly OtpService _inner;
    public string? LastOtp { get; private set; }

    public SpyOtpService(ICacheService cache)
    {
        _inner = new OtpService(cache, NullLogger<OtpService>.Instance);
    }

    public async Task<OtpIssuanceResult> GenerateOtpAsync(User user, string ipAddress)
    {
        var result = await _inner.GenerateOtpAsync(user, ipAddress);
        LastOtp = result.Otp;
        return result;
    }

    public Task<OtpEnvelope?> ResendOtpAsync(User user) => _inner.ResendOtpAsync(user);
    public Task<bool> ValidateOtpAsync(Guid userId, string otp) => _inner.ValidateOtpAsync(userId, otp);
    public Task ClearOtpStateAsync(Guid userId) => _inner.ClearOtpStateAsync(userId);
}
