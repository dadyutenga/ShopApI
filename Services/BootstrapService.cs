using Microsoft.EntityFrameworkCore;
using ShopApI.Data;
using ShopApI.DTOs;
using ShopApI.Enums;
using ShopApI.Events;
using ShopApI.Helpers;
using ShopApI.Models;

namespace ShopApI.Services;

public class BootstrapService : IBootstrapService
{
    private const string BOOTSTRAP_LOCK_KEY = "bootstrap.locked";

    private readonly ApplicationDbContext _context;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<BootstrapService> _logger;

    public BootstrapService(ApplicationDbContext context, IEventPublisher eventPublisher, ILogger<BootstrapService> logger)
    {
        _context = context;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<BootstrapStatusResponse> GetStatusAsync()
    {
        var hasAdmins = await _context.Users.AnyAsync(u => u.Role == UserRole.Admin);
        var locked = await IsLockedAsync();

        if (hasAdmins || locked)
        {
            return new BootstrapStatusResponse(false, hasAdmins ? "Admin user exists" : "Bootstrap locked");
        }

        return new BootstrapStatusResponse(true, "Bootstrap allowed");
    }

    public async Task<UserDto> CompleteBootstrapAsync(BootstrapCompleteRequest request)
    {
        var status = await GetStatusAsync();
        if (!status.IsBootstrapAllowed)
        {
            throw new InvalidOperationException(status.Reason);
        }

        var (email, password) = ResolveCredentials(request);
        var user = new User
        {
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = PasswordHasher.HashPassword(password),
            Role = UserRole.Admin,
            Provider = "bootstrap",
            IsActive = true,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.AuditLogs.Add(new AuditLog
        {
            ActorId = user.Id,
            TargetUserId = user.Id,
            EventType = "bootstrap_admin_created",
            Metadata = "{}"
        });

        await _context.SaveChangesAsync();
        await PersistLockAsync();

        var correlationId = Guid.NewGuid().ToString();
        await _eventPublisher.PublishAsync(new UserRegisteredEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role.ToString(),
            Provider = "bootstrap",
            CorrelationId = correlationId
        });

        _logger.LogInformation("Bootstrap admin created for {Email}", MaskingHelper.MaskEmail(user.Email));
        return new UserDto(user.Id, user.Email, user.Role.ToString(), user.IsActive, user.IsEmailVerified, user.CreatedAt, null);
    }

    private (string Email, string Password) ResolveCredentials(BootstrapCompleteRequest request)
    {
        var envEmail = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_EMAIL");
        var envPassword = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD");

        if (!string.IsNullOrWhiteSpace(envEmail) && !string.IsNullOrWhiteSpace(envPassword))
        {
            return (envEmail, envPassword);
        }

        if (!string.IsNullOrWhiteSpace(request.SetupToken))
        {
            var signingKey = Environment.GetEnvironmentVariable("BOOTSTRAP_SETUP_SIGNING_KEY") ?? throw new InvalidOperationException("BOOTSTRAP_SETUP_SIGNING_KEY missing");
            var payload = BootstrapTokenHelper.TryParse(request.SetupToken, signingKey) ?? throw new UnauthorizedAccessException("Invalid setup token");
            return (payload.Email, payload.Password);
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !string.IsNullOrWhiteSpace(request.Password))
        {
            return (request.Email, request.Password);
        }

        throw new InvalidOperationException("Bootstrap credentials are not configured");
    }

    private async Task PersistLockAsync()
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == BOOTSTRAP_LOCK_KEY);
        if (setting == null)
        {
            _context.SystemSettings.Add(new SystemSetting
            {
                Key = BOOTSTRAP_LOCK_KEY,
                Value = "true",
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            setting.Value = "true";
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    private async Task<bool> IsLockedAsync()
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == BOOTSTRAP_LOCK_KEY);
        return setting?.Value == "true";
    }
}
