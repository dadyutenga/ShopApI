using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopApI.Data;
using ShopApI.DTOs;
using ShopApI.Events;
using ShopApI.Helpers;
using ShopApI.Models;
using ShopApI.Enums;
using ShopApI.Options;
using System.Security.Claims;
using System;

namespace ShopApI.Services;

public class OAuthService : IOAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<OAuthService> _logger;
    private readonly string _emailVerificationSigningKey;

    public OAuthService(
        ApplicationDbContext context,
        IEventPublisher eventPublisher,
        ILogger<OAuthService> logger,
        IOptions<EmailVerificationOptions> emailVerificationOptions)
    {
        _context = context;
        _eventPublisher = eventPublisher;
        _logger = logger;
        _emailVerificationSigningKey = string.IsNullOrWhiteSpace(emailVerificationOptions.Value.SigningKey)
            ? throw new InvalidOperationException("EMAIL_VERIFICATION_SIGNING_KEY is not configured.")
            : emailVerificationOptions.Value.SigningKey;
    }

    public async Task<OAuthPendingResponse> HandleOAuthCallbackAsync(string provider, ClaimsPrincipal principal, string verificationBaseUrl)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email);
        var providerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(providerId))
        {
            throw new InvalidOperationException("Invalid OAuth response");
        }

        var user = await _context.Users.Include(u => u.CustomerProfile).FirstOrDefaultAsync(u =>
            u.Email == email || (u.Provider == provider && u.ProviderId == providerId));

        if (user == null)
        {
            user = new User
            {
                Email = email,
                Provider = provider,
                ProviderId = providerId,
                Role = UserRole.Customer,
                IsActive = true,
                IsEmailVerified = false,
                CustomerProfile = new CustomerProfile
                {
                    IsEmailVerified = false,
                    IsPhoneVerified = false,
                    TwoFactorEnabled = false
                }
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
            user.IsEmailVerified = false;
            if (user.CustomerProfile == null)
            {
                user.CustomerProfile = new CustomerProfile();
            }
            await _context.SaveChangesAsync();
        }

        var token = EmailVerificationTokenHelper.Sign(
            new EmailVerificationPayload(user.Id, provider, DateTimeOffset.UtcNow.AddHours(1)),
            _emailVerificationSigningKey);

        var link = $"{verificationBaseUrl}?token={Uri.EscapeDataString(token)}";

        await _eventPublisher.PublishAsync(new EmailVerificationSentEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Provider = provider,
            CorrelationId = Guid.NewGuid().ToString()
        });

        _logger.LogInformation("OAuth verification required for {Email}", MaskingHelper.MaskEmail(user.Email));
        return new OAuthPendingResponse(user.Email, link);
    }
}
