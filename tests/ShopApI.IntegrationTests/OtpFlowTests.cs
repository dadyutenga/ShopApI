using Microsoft.Extensions.Logging.Abstractions;
using ShopApI.DTOs;
using ShopApI.Enums;
using ShopApI.Models;
using ShopApI.Services;
using Xunit;

namespace ShopApI.IntegrationTests;

public class OtpFlowTests
{
    [Fact]
    public async Task CustomerOtpHappyPath()
    {
        using var context = TestHelpers.CreateContext(nameof(CustomerOtpHappyPath));
        var cache = new InMemoryCacheService();
        var otpService = new SpyOtpService(cache);
        var authService = new AuthService(context, new FakeJwtService(), otpService, new FakeEventPublisher(), cache, NullLogger<AuthService>.Instance);

        var user = new User
        {
            Email = "customer@example.com",
            PasswordHash = "na",
            Role = UserRole.Customer,
            IsActive = true,
            IsEmailVerified = true,
            CustomerProfile = new CustomerProfile { IsEmailVerified = true }
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await authService.RequestCustomerOtpAsync(new RequestOtpRequest(user.Email), "127.0.0.1");
        var otp = otpService.LastOtp;
        Assert.False(string.IsNullOrEmpty(otp));

        var response = await authService.VerifyCustomerOtpAsync(new VerifyOtpRequest(user.Email, otp!));
        Assert.Equal(user.Email, response.User.Email);
    }

    [Fact]
    public async Task CustomerOtpAbusePathClearsState()
    {
        using var context = TestHelpers.CreateContext(nameof(CustomerOtpAbusePathClearsState));
        var cache = new InMemoryCacheService();
        var otpService = new SpyOtpService(cache);
        var authService = new AuthService(context, new FakeJwtService(), otpService, new FakeEventPublisher(), cache, NullLogger<AuthService>.Instance);

        var user = new User
        {
            Email = "abuse@example.com",
            PasswordHash = "na",
            Role = UserRole.Customer,
            IsActive = true,
            IsEmailVerified = true,
            CustomerProfile = new CustomerProfile { IsEmailVerified = true }
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await authService.RequestCustomerOtpAsync(new RequestOtpRequest(user.Email), "127.0.0.1");
        var otp = otpService.LastOtp!;

        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authService.VerifyCustomerOtpAsync(new VerifyOtpRequest(user.Email, "000000")));
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authService.VerifyCustomerOtpAsync(new VerifyOtpRequest(user.Email, otp)));
    }
}
