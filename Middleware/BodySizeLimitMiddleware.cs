namespace ShopApI.Middleware;

public class BodySizeLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BodySizeLimitMiddleware> _logger;
    private const long MAX_BODY_SIZE = 1048576;

    public BodySizeLimitMiddleware(RequestDelegate next, ILogger<BodySizeLimitMiddleware> _logger)
    {
        _next = next;
        this._logger = _logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var contentLength = context.Request.ContentLength;

        if (contentLength.HasValue && contentLength.Value > MAX_BODY_SIZE)
        {
            _logger.LogWarning("Request body size {Size} exceeds limit of {Limit}", contentLength.Value, MAX_BODY_SIZE);
            context.Response.StatusCode = 413;
            await context.Response.WriteAsJsonAsync(new { message = "Request body too large. Maximum size is 1MB." });
            return;
        }

        await _next(context);
    }
}
