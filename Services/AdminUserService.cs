using Microsoft.EntityFrameworkCore;
using ShopApI.Data;
using ShopApI.DTOs;
using ShopApI.Enums;
using ShopApI.Events;
using ShopApI.Helpers;
using ShopApI.Models;
using System.Text.Json;

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

    public async Task<UserDto> RegisterManagerAsync(Guid actorId, AdminRegisterManagerRequest request)
        => await CreateUserAsync(actorId, request.Email, request.Password, UserRole.Manager);

    public async Task<UserDto> RegisterSupportAsync(Guid actorId, AdminRegisterSupportRequest request)
        => await CreateUserAsync(actorId, request.Email, request.Password, UserRole.Support);

    public async Task<UserDto> RegisterCustomerAsync(Guid actorId, AdminRegisterCustomerRequest request)
    {
        var user = new User
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = PasswordHasher.HashPassword(Guid.NewGuid().ToString("N")),
            Role = UserRole.Customer,
            IsActive = true,
            Provider = "admin",
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CustomerProfile = new CustomerProfile
            {
                PhoneNumber = request.PhoneNumber,
                TwoFactorEnabled = request.TwoFactorEnabled,
                IsEmailVerified = false,
                IsPhoneVerified = false
            }
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await WriteAuditAsync(actorId, user.Id, "user.registered", new { role = user.Role.ToString() });
        await PublishRegistrationEventsAsync(user, "admin");
        return Map(user);
    }

    public async Task<IEnumerable<UserDto>> GetUsersAsync(UserQueryParameters parameters)
    {
        var query = _context.Users.Include(u => u.CustomerProfile).AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.Email))
            query = query.Where(u => u.Email.Contains(parameters.Email));

        if (parameters.Role.HasValue)
            query = query.Where(u => u.Role == parameters.Role.Value);

        if (parameters.IsActive.HasValue)
            query = query.Where(u => u.IsActive == parameters.IsActive.Value);

        var users = await query.OrderBy(u => u.Email).ToListAsync();
        return users.Select(Map);
    }

    public async Task<UserDto?> GetUserAsync(Guid userId)
    {
        var user = await _context.Users.Include(u => u.CustomerProfile).FirstOrDefaultAsync(u => u.Id == userId);
        return user == null ? null : Map(user);
    }

    public async Task<UserDto> UpdateRoleAsync(Guid actorId, Guid userId, UpdateUserRoleRequest request)
    {
        var user = await _context.Users.Include(u => u.CustomerProfile).FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found");

        if (user.Role == UserRole.Admin && request.Role != UserRole.Admin)
        {
            var adminCount = await _context.Users.CountAsync(u => u.Role == UserRole.Admin && u.Id != userId);
            if (adminCount == 0)
                throw new InvalidOperationException("At least one admin must remain");
        }

        user.Role = request.Role;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await WriteAuditAsync(actorId, user.Id, "user.role.assigned", new { role = user.Role.ToString() });
        await _eventPublisher.PublishAsync(new UserRoleAssignedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role.ToString(),
            CorrelationId = Guid.NewGuid().ToString()
        });

        return Map(user);
    }

    public async Task<UserDto> UpdateStatusAsync(Guid actorId, Guid userId, UpdateUserStatusRequest request)
    {
        var user = await _context.Users.Include(u => u.CustomerProfile).FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found");

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var @event = request.IsActive ? "user.activated" : "user.deactivated";
        await WriteAuditAsync(actorId, user.Id, @event, new { status = request.IsActive });
        await _eventPublisher.PublishAsync(new UserDeactivatedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Reason = @event,
            CorrelationId = Guid.NewGuid().ToString()
        });

        return Map(user);
    }

    public async Task SoftDeleteUserAsync(Guid actorId, Guid userId, SoftDeleteUserRequest request)
    {
        var user = await _context.Users.Include(u => u.CustomerProfile).FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found");

        user.IsActive = false;
        user.DeletedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await WriteAuditAsync(actorId, user.Id, "user.soft_deleted", new { request.Reason });
        await _eventPublisher.PublishAsync(new UserDeactivatedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Reason = "user.soft_deleted",
            CorrelationId = Guid.NewGuid().ToString()
        });
    }

    private async Task<UserDto> CreateUserAsync(Guid actorId, string email, string password, UserRole role)
    {
        if (await _context.Users.AnyAsync(u => u.Email == email))
            throw new InvalidOperationException("Email already exists");

        var user = new User
        {
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = PasswordHasher.HashPassword(password),
            Role = role,
            IsActive = true,
            Provider = "admin",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await WriteAuditAsync(actorId, user.Id, "user.registered", new { role = role.ToString() });
        await PublishRegistrationEventsAsync(user, "admin");
        _logger.LogInformation("{Role} user created: {Email}", role, MaskingHelper.MaskEmail(user.Email));
        return Map(user);
    }

    private async Task PublishRegistrationEventsAsync(User user, string provider)
    {
        await _eventPublisher.PublishAsync(new UserRegisteredEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role.ToString(),
            Provider = provider,
            CorrelationId = Guid.NewGuid().ToString()
        });
    }

    private async Task WriteAuditAsync(Guid actorId, Guid targetUserId, string eventType, object metadata)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            TargetUserId = targetUserId,
            EventType = eventType,
            Metadata = JsonSerializer.Serialize(metadata)
        });
        await _context.SaveChangesAsync();
    }

    private static UserDto Map(User user)
        => new(
            user.Id,
            user.Email,
            user.Role.ToString(),
            user.IsActive,
            user.IsEmailVerified,
            user.CreatedAt,
            user.CustomerProfile == null
                ? null
                : new CustomerProfileDto(
                    user.CustomerProfile.PhoneNumber,
                    user.CustomerProfile.IsPhoneVerified,
                    user.CustomerProfile.IsEmailVerified,
                    user.CustomerProfile.TwoFactorEnabled));
}
