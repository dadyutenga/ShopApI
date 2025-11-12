using Microsoft.EntityFrameworkCore;
using ShopApI.Data;
using ShopApI.DTOs;
using ShopApI.Events;
using ShopApI.Models;
using System.Security.Claims;

namespace ShopApI.Services;

public class OAuthService : IOAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICacheService _cacheService;
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(
        ApplicationDbContext context,
        IJwtService jwtService,
        IEventPublisher eventPublisher,
        ICacheService cacheService,
        ILogger<OAuthService> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _eventPublisher = eventPublisher;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<AuthResponse> HandleOAuthCallbackAsync(string provider, ClaimsPrincipal principal)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email);
        var providerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(providerId))
        {
            throw new InvalidOperationException("Invalid OAuth response");
        }

        // Check if user exists
        var user = await _context.Users.FirstOrDefaultAsync(u => 
            u.Email == email || (u.Provider == provider && u.ProviderId == providerId));

        if (user == null)
        {
            // Create new user
            user = new User
            {
                Email = email,
                Provider = provider,
                ProviderId = providerId,
                Role = "Customer",
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await _eventPublisher.PublishAsync(new UserRegisteredEvent
            {
                UserId = user.Id,
                Email = user.Email,
                Provider = provider
            });
        }
        else if (user.Provider != provider || user.ProviderId != providerId)
        {
            // Link OAuth account
            user.Provider = provider;
            user.ProviderId = providerId;
            await _context.SaveChangesAsync();
        }

        // Cache user session
        await _cacheService.SetAsync($"session:{user.Id}", new
        {
            user.Id,
            user.Email,
            user.Role
        }, TimeSpan.FromMinutes(15));

        // Generate JWT tokens
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();
        await _jwtService.SaveRefreshTokenAsync(user.Id, refreshToken);

        return new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(15),
            new UserDto(user.Id, user.Email, user.Role, user.Provider, user.CreatedAt)
        );
    }
}
