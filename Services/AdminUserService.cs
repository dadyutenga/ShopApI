using Microsoft.EntityFrameworkCore;
using ShopApI.Data;
using ShopApI.DTOs;
using ShopApI.Enums;
using ShopApI.Events;
using ShopApI.Helpers;
using ShopApI.Models;

namespace ShopApI.Services;

public class AdminUserService : IAdminUserService
{
    private readonly ApplicationDbContext _context;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(ApplicationDbContext context, IEventPublisher eventPublisher, ILogger<AdminUserService> logger)
    {
        _context = context;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public Task<UserDto> RegisterManagerAsync(RegisterManagerRequest request, string actorEmail)
        => RegisterStaffAsync(request.Email, request.TemporaryPassword, UserRole.Manager, actorEmail);

    public Task<UserDto> RegisterSupportAsync(RegisterSupportRequest request, string actorEmail)
        => RegisterStaffAsync(request.Email, request.TemporaryPassword, UserRole.Support, actorEmail);

    public async Task<UserDto> RegisterCustomerAsync(RegisterCustomerRequest request, string actorEmail)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email && !u.IsDeleted))
        {
            throw new InvalidOperationException("User already exists");
        }

        var user = new User
        {
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = PasswordHasher.HashPassword(Guid.NewGuid().ToString("N")),
            Role = UserRole.Customer,
            IsActive = true,
            PhoneNumber = request.PhoneNumber,
            Provider = "admin",
            IsEmailVerified = false
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        await WriteAuditAsync(user.Id, "admin_register_customer", new { actorEmail, request.Email });

        await _eventPublisher.PublishAsync(new UserRegisteredEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role.ToString(),
            Provider = user.Provider ?? "admin",
            CorrelationId = Guid.NewGuid().ToString()
        });

        return Map(user);
    }

    public async Task<IEnumerable<UserDto>> GetUsersAsync(string? role, string? status, string? email)
    {
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrEmpty(role) && Enum.TryParse<UserRole>(role, true, out var parsedRole))
        {
            query = query.Where(u => u.Role == parsedRole);
        }

        if (!string.IsNullOrEmpty(status))
        {
            if (bool.TryParse(status, out var isActive))
            {
                query = query.Where(u => u.IsActive == isActive && !u.IsDeleted);
            }
        }

        if (!string.IsNullOrEmpty(email))
        {
            query = query.Where(u => u.Email.Contains(email));
        }

        var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
        return users.Select(Map);
    }

    public async Task<UserDto?> GetUserAsync(Guid id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
        return user == null ? null : Map(user);
    }

    public async Task<UserDto> UpdateRoleAsync(Guid id, UpdateUserRoleRequest request, string actorEmail)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted)
            ?? throw new KeyNotFoundException("User not found");

        user.Role = request.Role;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await WriteAuditAsync(user.Id, "admin_role_assigned", new { actorEmail, request.Role });
        await _eventPublisher.PublishAsync(new UserRoleAssignedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role.ToString(),
            CorrelationId = Guid.NewGuid().ToString()
        });

        return Map(user);
    }

    public async Task<UserDto> UpdateStatusAsync(Guid id, UpdateUserStatusRequest request, string actorEmail)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted)
            ?? throw new KeyNotFoundException("User not found");

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await WriteAuditAsync(user.Id, "admin_status_changed", new { actorEmail, request.IsActive });
        await _eventPublisher.PublishAsync(new UserStatusChangedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            IsActive = user.IsActive,
            CorrelationId = Guid.NewGuid().ToString()
        });

        return Map(user);
    }

    public async Task SoftDeleteAsync(Guid id, string actorEmail)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted)
            ?? throw new KeyNotFoundException("User not found");

        user.IsDeleted = true;
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await WriteAuditAsync(user.Id, "admin_soft_delete", new { actorEmail });
        await _eventPublisher.PublishAsync(new UserDeletedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            CorrelationId = Guid.NewGuid().ToString()
        });
    }

    private async Task<UserDto> RegisterStaffAsync(string email, string password, UserRole role, string actorEmail)
    {
        if (await _context.Users.AnyAsync(u => u.Email == email && !u.IsDeleted))
        {
            throw new InvalidOperationException("User already exists");
        }

        var user = new User
        {
            Email = email.ToLowerInvariant(),
            PasswordHash = PasswordHasher.HashPassword(password),
            Role = role,
            IsActive = true,
            IsEmailVerified = true,
            Provider = "admin"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await WriteAuditAsync(user.Id, "admin_register_staff", new { actorEmail, email, role });
        await _eventPublisher.PublishAsync(new UserRegisteredEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role.ToString(),
            Provider = user.Provider ?? "admin",
            CorrelationId = Guid.NewGuid().ToString()
        });

        _logger.LogInformation("{Role} registered with credentials emailed", role);
        return Map(user);
    }

    private static UserDto Map(User user) => new(
        user.Id,
        user.Email,
        user.Role.ToString(),
        user.Provider,
        user.CreatedAt,
        user.IsActive,
        user.IsEmailVerified,
        user.IsPhoneVerified,
        user.TwoFactorEnabled);

    private async Task WriteAuditAsync(Guid userId, string action, object metadata)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            Metadata = System.Text.Json.JsonSerializer.Serialize(metadata)
        });
        await _context.SaveChangesAsync();
    }
}
