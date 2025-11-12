using MassTransit;
using ShopApI.Events;

namespace ShopApI.Consumers;

public class UserRegisteredConsumer : IConsumer<UserRegisteredEvent>
{
    private readonly ILogger<UserRegisteredConsumer> _logger;

    public UserRegisteredConsumer(ILogger<UserRegisteredConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<UserRegisteredEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Processing UserRegistered event: UserId={UserId}, Email={Email}, Provider={Provider}",
            evt.UserId, evt.Email, evt.Provider);

        // Add your business logic here:
        // - Send welcome email
        // - Create user profile
        // - Initialize user preferences
        // - Send to analytics

        return Task.CompletedTask;
    }
}

public class UserRoleAssignedConsumer : IConsumer<UserRoleAssignedEvent>
{
    private readonly ILogger<UserRoleAssignedConsumer> _logger;

    public UserRoleAssignedConsumer(ILogger<UserRoleAssignedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<UserRoleAssignedEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Processing UserRoleAssigned event: UserId={UserId}, Email={Email}, Role={Role}",
            evt.UserId, evt.Email, evt.Role);

        // Add your business logic here:
        // - Update related services
        // - Invalidate caches
        // - Notify connected services

        return Task.CompletedTask;
    }
}

public class UserStatusChangedConsumer : IConsumer<UserStatusChangedEvent>
{
    private readonly ILogger<UserStatusChangedConsumer> _logger;

    public UserStatusChangedConsumer(ILogger<UserStatusChangedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<UserStatusChangedEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Processing UserStatusChanged event: UserId={UserId}, Email={Email}, Active={Active}",
            evt.UserId, evt.Email, evt.IsActive);

        // Add your business logic here:
        // - Cancel subscriptions
        // - Archive user data
        // - Send goodbye email
        // - Clean up resources

        return Task.CompletedTask;
    }
}

public class OtpGeneratedConsumer : IConsumer<OtpGeneratedEvent>
{
    private readonly ILogger<OtpGeneratedConsumer> _logger;

    public OtpGeneratedConsumer(ILogger<OtpGeneratedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<OtpGeneratedEvent> context)
    {
        _logger.LogInformation("OTP generated for {Email}", context.Message.Email);
        return Task.CompletedTask;
    }
}

public class OtpVerifiedConsumer : IConsumer<OtpVerifiedEvent>
{
    private readonly ILogger<OtpVerifiedConsumer> _logger;

    public OtpVerifiedConsumer(ILogger<OtpVerifiedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<OtpVerifiedEvent> context)
    {
        _logger.LogInformation("OTP verified for {Email}", context.Message.Email);
        return Task.CompletedTask;
    }
}

public class EmailVerificationEventConsumer : IConsumer<EmailVerificationEvent>
{
    private readonly ILogger<EmailVerificationEventConsumer> _logger;

    public EmailVerificationEventConsumer(ILogger<EmailVerificationEventConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<EmailVerificationEvent> context)
    {
        _logger.LogInformation("Email verification event {Type} for {Email} completed={Completed}",
            context.Message.Type, context.Message.Email, context.Message.Completed);
        return Task.CompletedTask;
    }
}
