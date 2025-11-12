using ShopApI.Enums;

namespace ShopApI.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = null!;
    public string? PasswordHash { get; set; }
    public string? Provider { get; set; }
    public string? ProviderId { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public string? AuditTrail { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }

    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
