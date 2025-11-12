using Microsoft.Extensions.Logging.Abstractions;
using ShopApI.DTOs;
using ShopApI.Enums;
using ShopApI.Events;
using ShopApI.Models;
using ShopApI.Services;
using System.Linq;
using Xunit;

namespace ShopApI.IntegrationTests;

public class AdminManagementTests
{
    [Fact]
    public async Task AdminCrudLifecycle()
    {
        using var context = TestHelpers.CreateContext(nameof(AdminCrudLifecycle));
        var publisher = new FakeEventPublisher();
        var service = new AdminUserService(context, publisher, NullLogger<AdminUserService>.Instance);
        var actorId = Guid.NewGuid();

        var manager = await service.RegisterManagerAsync(actorId, new AdminRegisterManagerRequest("manager@corp.local", "StrongPass123!"));
        var support = await service.RegisterSupportAsync(actorId, new AdminRegisterSupportRequest("support@corp.local", "StrongPass123!"));
        var customer = await service.RegisterCustomerAsync(actorId, new AdminRegisterCustomerRequest("customer@corp.local", "+10000000000", true));

        var users = await service.GetUsersAsync(new UserQueryParameters(role: UserRole.Manager));
        Assert.Single(users);

        var updatedRole = await service.UpdateRoleAsync(actorId, support.Id, new UpdateUserRoleRequest(UserRole.Manager));
        Assert.Equal(UserRole.Manager.ToString(), updatedRole.Role);

        var updatedStatus = await service.UpdateStatusAsync(actorId, customer.Id, new UpdateUserStatusRequest(false));
        Assert.False(updatedStatus.IsActive);

        await service.SoftDeleteUserAsync(actorId, manager.Id, new SoftDeleteUserRequest("cleanup"));
        Assert.Contains(publisher.Events, e => e is UserDeactivatedEvent);
        Assert.True(context.AuditLogs.Any());
    }
}
