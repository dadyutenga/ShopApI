using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ShopApI.Data;
using ShopApI.DTOs;
using ShopApI.Enums;
using ShopApI.Models;
using ShopApI.Services;
using ShopApI.IntegrationTests.TestDoubles;
using Xunit;

namespace ShopApI.IntegrationTests;

public class AuthServiceOtpTests
{
    private readonly InMemoryCacheService _cache = new();

    private AuthService BuildService(ApplicationDbContext context, IOtpService? otpService = null)
    {
        otpService ??= new OtpService(_cache, NullLogger<OtpService>.Instance);
        var emailVerifier = new EmailVerificationService(new ConfigurationBuilder().AddInMemoryCollection().Build(), NullLogger<EmailVerificationService>.Instance);
        return new AuthService(
            context,
            new FakeJwtService(),
            otpService,
            new FakeEventPublisher(),
            _cache,
            emailVerifier,
            NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task CustomerOtpHappyPath()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var user = new User { Email = "customer@example.com", Role = UserRole.Customer, IsActive = true, IsEmailVerified = true };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var otpService = new OtpService(_cache, NullLogger<OtpService>.Instance);
        var service = BuildService(context, otpService);
        await service.RequestCustomerOtpAsync(new OtpRequest(user.Email), "127.0.0.1");

        var descriptor = await otpService.GenerateAsync(user.Id, user.Email, "customer_auth");
        var response = await service.VerifyCustomerOtpAsync(new OtpVerifyRequest(user.Email, descriptor.Code), "127.0.0.1");

        Assert.NotNull(response.AccessToken);
        Assert.Equal(user.Email, response.User.Email);
    }

    [Fact]
    public async Task CustomerOtpRateLimit()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var user = new User { Email = "slow@example.com", Role = UserRole.Customer, IsActive = true, IsEmailVerified = true };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = BuildService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            for (var i = 0; i < 6; i++)
            {
                await service.RequestCustomerOtpAsync(new OtpRequest(user.Email), "10.0.0.1");
            }
        });
    }
}
