using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ShopApI.Data;
using ShopApI.DTOs;
using ShopApI.Enums;
using ShopApI.Helpers;
using ShopApI.Models;

namespace ShopApI.Services;

public class BootstrapService : IBootstrapService
{
    private const string BootstrapLockKey = "bootstrap_locked";
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BootstrapService> _logger;

    public BootstrapService(ApplicationDbContext context, ILogger<BootstrapService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BootstrapStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var hasAdmin = await _context.Users.AnyAsync(u => u.Role == UserRole.Admin && !u.IsDeleted, cancellationToken);
        var locked = await IsLockedAsync(cancellationToken);
        var enabled = !locked && !hasAdmin;
        return new BootstrapStatusResponse(locked, enabled, hasAdmin);
    }

    public async Task CompleteAsync(BootstrapCompleteRequest request, CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);
        if (!status.BootstrapEnabled)
        {
            throw new InvalidOperationException("Bootstrap is disabled");
        }

        var (email, password) = ResolveCredentials(request);
        var admin = new User
        {
            Email = email.ToLowerInvariant(),
            PasswordHash = PasswordHasher.HashPassword(password),
            Role = UserRole.Admin,
            IsActive = true,
            IsEmailVerified = true,
            Provider = "bootstrap"
        };

        _context.Users.Add(admin);
        await _context.SaveChangesAsync(cancellationToken);

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = admin.Id,
            Action = "bootstrap_admin_created",
            Metadata = System.Text.Json.JsonSerializer.Serialize(new { admin.Email }),
            IsImmutable = true
        });
        await _context.SaveChangesAsync(cancellationToken);

        await WriteLockAsync(cancellationToken);
        _logger.LogInformation("Bootstrap admin created for {Email}", admin.Email);
    }

    private async Task<bool> IsLockedAsync(CancellationToken cancellationToken)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == BootstrapLockKey, cancellationToken);
        return setting != null && bool.TryParse(setting.Value, out var locked) && locked;
    }

    private async Task WriteLockAsync(CancellationToken cancellationToken)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == BootstrapLockKey, cancellationToken);
        if (setting == null)
        {
            setting = new SystemSetting { Key = BootstrapLockKey, Value = bool.TrueString };
            _context.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = bool.TrueString;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private (string Email, string Password) ResolveCredentials(BootstrapCompleteRequest request)
    {
        if (!string.IsNullOrEmpty(request.SetupToken))
        {
            return ValidateSetupToken(request.SetupToken);
        }

        var email = request.Email ?? Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_EMAIL");
        var password = request.Password ?? Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Bootstrap credentials missing");
        }

        return (email, password);
    }

    private (string Email, string Password) ValidateSetupToken(string token)
    {
        var secret = Environment.GetEnvironmentVariable("BOOTSTRAP_TOKEN_SECRET") ?? "bootstrap-secret";
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey = key,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5)
        }, out _);

        var email = principal.FindFirstValue("email");
        var password = principal.FindFirstValue("password");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Setup token missing credentials");
        }

        return (email, password);
    }
}
