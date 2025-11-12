using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ShopApI.Data;
using ShopApI.Services;
using ShopApI.IntegrationTests.TestDoubles;
using System.Security.Claims;
using System.Linq;
using Xunit;

namespace ShopApI.IntegrationTests;

public class OAuthServiceTests
{
    [Fact]
    public async Task RequiresVerificationOnFirstLogin()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var service = new OAuthService(
            context,
            new FakeJwtService(),
            new FakeEventPublisher(),
            new InMemoryCacheService(),
            new EmailVerificationService(new ConfigurationBuilder().AddInMemoryCollection().Build(), NullLogger<EmailVerificationService>.Instance),
            NullLogger<OAuthService>.Instance);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Email, "oauth@example.com"),
            new Claim(ClaimTypes.NameIdentifier, "oauth-1")
        }));

        var result = await service.HandleOAuthCallbackAsync("google", principal);
        Assert.True(result.RequiresVerification);
        Assert.False(string.IsNullOrEmpty(result.VerificationLink));

        // simulate verification
        var user = context.Users.Single();
        user.IsEmailVerified = true;
        await context.SaveChangesAsync();

        var second = await service.HandleOAuthCallbackAsync("google", principal);
        Assert.False(second.RequiresVerification);
        Assert.NotNull(second.AuthResponse);
    }
}
