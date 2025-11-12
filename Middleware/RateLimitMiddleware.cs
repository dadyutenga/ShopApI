using ShopApI.Services;
using System.Net;

namespace ShopApI.Middleware;

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICacheService cacheService)
    {
        // Only rate limit auth endpoints
        if (!context.Request.Path.StartsWithSegments("/api/auth"))
        {
            await _next(context);
            return;
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = $"rate:auth:{ipAddress}";

        var requestCount = await cacheService.IncrementAsync(key, TimeSpan.FromMinutes(1));

        if (requestCount > 10) // 10 requests per minute
        {
            _logger.LogWarning("Rate limit exceeded for IP: {IpAddress}", ipAddress);
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Too many requests. Please try again later.",
                retryAfter = 60
            });
            return;
        }

        await _next(context);
    }
}
