namespace ShopApI.Models;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public string? Actor { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsImmutable { get; set; }

    public User? User { get; set; }
}
