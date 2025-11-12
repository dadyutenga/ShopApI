using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShopApI.Data;
using ShopApI.DTOs;
using ShopApI.Enums;
using ShopApI.Services;
using ShopApI.IntegrationTests.TestDoubles;
using Xunit;
using System.Linq;

namespace ShopApI.IntegrationTests;

public class AdminUserServiceTests
{
    [Fact]
    public async Task CrudFlowWorks()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var publisher = new FakeEventPublisher();
        var service = new AdminUserService(context, publisher, NullLogger<AdminUserService>.Instance);

        var manager = await service.RegisterManagerAsync(new RegisterManagerRequest("manager@corp.local", "Pass123!"), "root@corp.local");
        var support = await service.RegisterSupportAsync(new RegisterSupportRequest("support@corp.local", "Pass123!"), "root@corp.local");
        var customer = await service.RegisterCustomerAsync(new RegisterCustomerRequest("cust@corp.local", "+15551234567"), "root@corp.local");

        Assert.Equal(UserRole.Manager.ToString(), manager.Role);
        Assert.Equal(3, context.Users.Count());

        var users = await service.GetUsersAsync(null, null, null);
        Assert.Equal(3, users.Count());

        var updatedRole = await service.UpdateRoleAsync(customer.Id, new UpdateUserRoleRequest(UserRole.Support), "root@corp.local");
        Assert.Equal(UserRole.Support.ToString(), updatedRole.Role);

        var updatedStatus = await service.UpdateStatusAsync(customer.Id, new UpdateUserStatusRequest(false), "root@corp.local");
        Assert.False(updatedStatus.IsActive);

        await service.SoftDeleteAsync(customer.Id, "root@corp.local");
        var softDeleted = await service.GetUserAsync(customer.Id);
        Assert.Null(softDeleted);
    }
}
