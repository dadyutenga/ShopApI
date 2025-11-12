using Microsoft.EntityFrameworkCore;
using ShopApI.Data;
using ShopApI.DTOs;
using ShopApI.Events;
using ShopApI.Models;
using ShopApI.Enums;
using System.Security.Claims;

namespace ShopApI.Services;

public class OAuthService : IOAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICacheService _cacheService;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(
        ApplicationDbContext context,
        IJwtService jwtService,
        IEventPublisher eventPublisher,
        ICacheService cacheService,
        IEmailVerificationService emailVerificationService,
        ILogger<OAuthService> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _eventPublisher = eventPublisher;
        _cacheService = cacheService;
        _emailVerificationService = emailVerificationService;
        _logger = logger;
    }

    public async Task<OAuthCallbackResult> HandleOAuthCallbackAsync(string provider, ClaimsPrincipal principal)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email);
        var providerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(providerId))
        {
            throw new InvalidOperationException("Invalid OAuth response");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.Email == email || (u.Provider == provider && u.ProviderId == providerId));

        if (user == null)
        {
            user = new User
            {
                Email = email.ToLowerInvariant(),
                Provider = provider,
                ProviderId = providerId,
                Role = UserRole.Customer,
                IsActive = true,
                IsEmailVerified = false
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await _eventPublisher.PublishAsync(new UserRegisteredEvent
            {
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role.ToString(),
                Provider = provider,
                CorrelationId = Guid.NewGuid().ToString()
            });
        }
        else if (user.Provider != provider || user.ProviderId != providerId)
        {
            user.Provider = provider;
            user.ProviderId = providerId;
            await _context.SaveChangesAsync();
        }

        if (!user.IsEmailVerified)
        {
            var token = _emailVerificationService.IssueToken(user, TimeSpan.FromHours(2));
            var verificationLink = $"/api/auth/verify-email?token={token}";

            await _eventPublisher.PublishAsync(new EmailVerificationEvent
            {
                UserId = user.Id,
                Email = user.Email,
                Type = "oauth_link",
                Completed = false,
                CorrelationId = Guid.NewGuid().ToString()
            });

            return new OAuthCallbackResult(true, verificationLink, null);
        }

        await _cacheService.SetAsync($"session:{user.Id}", new
        {
            user.Id,
            user.Email,
            Role = user.Role.ToString()
        }, TimeSpan.FromMinutes(10));

        var accessToken = await _jwtService.GenerateAccessTokenAsync(user);
        var refreshToken = _jwtService.GenerateRefreshToken();
        await _jwtService.SaveRefreshTokenAsync(user.Id, refreshToken);

        return new OAuthCallbackResult(false, null, new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(10),
            new UserDto(user.Id, user.Email, user.Role.ToString(), user.Provider, user.CreatedAt, user.IsActive, user.IsEmailVerified, user.IsPhoneVerified, user.TwoFactorEnabled)));
    }
}
