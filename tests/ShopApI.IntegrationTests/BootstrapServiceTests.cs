using Microsoft.Extensions.Logging.Abstractions;
using ShopApI.DTOs;
using ShopApI.Services;
using Xunit;

namespace ShopApI.IntegrationTests;

public class BootstrapServiceTests
{
    [Fact]
    public async Task BootstrapLocksAfterFirstUse()
    {
        using var context = TestHelpers.CreateContext(nameof(BootstrapLocksAfterFirstUse));
        var publisher = new FakeEventPublisher();
        var service = new BootstrapService(context, publisher, NullLogger<BootstrapService>.Instance);

        var status = await service.GetStatusAsync();
        Assert.True(status.IsBootstrapAllowed);

        var admin = await service.CompleteBootstrapAsync(new BootstrapCompleteRequest("admin@example.com", "ChangeMe123!", null));
        Assert.Equal("admin@example.com", admin.Email);

        var lockedStatus = await service.GetStatusAsync();
        Assert.False(lockedStatus.IsBootstrapAllowed);
        Assert.Equal("Bootstrap locked", lockedStatus.Reason);
    }
}
