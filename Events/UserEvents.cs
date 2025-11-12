namespace ShopApI.Events;

public record UserRegisteredEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public record UserRoleAssignedEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public record UserStatusChangedEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public record UserDeletedEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public record OtpGeneratedEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public record OtpVerifiedEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public record EmailVerificationEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool Completed { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
