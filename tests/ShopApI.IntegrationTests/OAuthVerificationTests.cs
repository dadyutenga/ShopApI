using Microsoft.Extensions.Logging.Abstractions;
using ShopApI.Events;
using ShopApI.Services;
using System.Security.Claims;
using Xunit;

namespace ShopApI.IntegrationTests;

public class OAuthVerificationTests
{
    [Fact]
    public async Task OAuthVerificationRequiresEmailConfirmation()
    {
        Environment.SetEnvironmentVariable("EMAIL_VERIFICATION_SIGNING_KEY", "test-signing-key");
        using var context = TestHelpers.CreateContext(nameof(OAuthVerificationRequiresEmailConfirmation));
        var publisher = new FakeEventPublisher();
        var oauthService = new OAuthService(context, publisher, NullLogger<OAuthService>.Instance);
        var cache = new InMemoryCacheService();
        var authService = new AuthService(context, new FakeJwtService(), new SpyOtpService(cache), publisher, cache, NullLogger<AuthService>.Instance);

        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Email, "oauth@example.com"),
            new Claim(ClaimTypes.NameIdentifier, "oauth-123")
        }));

        var response = await oauthService.HandleOAuthCallbackAsync("google", claims, "https://example.com/api/auth/verify-email");
        Assert.Contains("verify-email", response.VerificationLink);

        var token = response.VerificationLink.Split("token=")[1];
        var verified = await authService.VerifyEmailFromTokenAsync(token);
        Assert.True(verified);
        Assert.Contains(publisher.Events, e => e is EmailVerificationCompletedEvent);
    }
}
