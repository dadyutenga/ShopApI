using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShopApI.Data;
using ShopApI.DTOs;
using ShopApI.Services;
using Xunit;

namespace ShopApI.IntegrationTests;

public class BootstrapServiceTests
{
    [Fact]
    public async Task CompleteBootstrapLocksService()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        var service = new BootstrapService(context, NullLogger<BootstrapService>.Instance);

        var status = await service.GetStatusAsync();
        Assert.True(status.BootstrapEnabled);

        await service.CompleteAsync(new BootstrapCompleteRequest("admin@example.com", "Admin123!", null));

        var after = await service.GetStatusAsync();
        Assert.True(after.IsLocked);
        Assert.True(after.HasAdminUsers);
        Assert.False(after.BootstrapEnabled);
    }
}
