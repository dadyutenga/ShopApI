using ShopApI.Services;

namespace ShopApI.IntegrationTests.TestDoubles;

public class FakeEventPublisher : IEventPublisher
{
    public List<object> Events { get; } = new();

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        Events.Add(message);
        return Task.CompletedTask;
    }
}
