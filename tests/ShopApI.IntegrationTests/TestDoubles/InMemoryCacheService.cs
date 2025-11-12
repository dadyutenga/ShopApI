using ShopApI.Services;
using System.Collections.Concurrent;

namespace ShopApI.IntegrationTests.TestDoubles;

public class InMemoryCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, (object Value, DateTime? ExpireAt)> _store = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.ExpireAt.HasValue && entry.ExpireAt.Value < DateTime.UtcNow)
            {
                _store.TryRemove(key, out _);
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
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var exists = _store.TryGetValue(key, out var entry) && (!entry.ExpireAt.HasValue || entry.ExpireAt.Value > DateTime.UtcNow);
        return Task.FromResult(exists);
    }

    public Task<long> IncrementAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var current = 0L;
        if (_store.TryGetValue(key, out var entry) && entry.Value is long l)
        {
            current = l;
        }

        current++;
        _store[key] = (current, expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : null);
        return Task.FromResult(current);
    }
}
