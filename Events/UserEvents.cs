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

public record UserUpdatedEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Event { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public record UserDeactivatedEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
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

public record OtpGeneratedEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
}

public record OtpVerifiedEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
}

public record EmailVerificationSentEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
}

public record EmailVerificationCompletedEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
}
