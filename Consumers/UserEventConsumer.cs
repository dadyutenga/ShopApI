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

public class UserUpdatedConsumer : IConsumer<UserUpdatedEvent>
{
    private readonly ILogger<UserUpdatedConsumer> _logger;

    public UserUpdatedConsumer(ILogger<UserUpdatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<UserUpdatedEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Processing UserUpdated event: UserId={UserId}, Email={Email}",
            evt.UserId, evt.Email);

        // Add your business logic here:
        // - Update related services
        // - Invalidate caches
        // - Notify connected services

        return Task.CompletedTask;
    }
}

public class UserDeactivatedConsumer : IConsumer<UserDeactivatedEvent>
{
    private readonly ILogger<UserDeactivatedConsumer> _logger;

    public UserDeactivatedConsumer(ILogger<UserDeactivatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<UserDeactivatedEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Processing UserDeactivated event: UserId={UserId}, Email={Email}",
            evt.UserId, evt.Email);

        // Add your business logic here:
        // - Cancel subscriptions
        // - Archive user data
        // - Send goodbye email
        // - Clean up resources

        return Task.CompletedTask;
    }
}
