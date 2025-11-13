using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShopApI.DTOs;
using ShopApI.Options;
using ShopApI.Services;
using System;

namespace ShopApI.Controllers;

[ApiController]
[Route("api/auth")]
public class OAuthController : ControllerBase
{
    private readonly IOAuthService _oauthService;
    private readonly ILogger<OAuthController> _logger;
    private readonly string _verificationBaseUrl;

    public OAuthController(IOAuthService oauthService, ILogger<OAuthController> logger, IOptions<AppUrlOptions> appUrlOptions)
    {
        _oauthService = oauthService;
        _logger = logger;

        var configuredBaseUrl = appUrlOptions.Value.BaseUrl?.Trim();

        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
            throw new InvalidOperationException("APP_BASE_URL is not configured.");

        configuredBaseUrl = configuredBaseUrl.TrimEnd('/');

        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var parsedUri) ||
            (parsedUri.Scheme != Uri.UriSchemeHttps && parsedUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("APP_BASE_URL must be an absolute HTTP or HTTPS URL.");
        }

        _verificationBaseUrl = $"{configuredBaseUrl}/api/auth/verify-email";
    }

    [HttpGet("google")]
    public IActionResult GoogleLogin()
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleCallback))
        };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google/callback")]
    public async Task<ActionResult<OAuthPendingResponse>> GoogleCallback()
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

        if (!authenticateResult.Succeeded)
            return Unauthorized(new { message = "Google authentication failed" });

        var response = await _oauthService.HandleOAuthCallbackAsync("google", authenticateResult.Principal, BuildVerificationBaseUrl());
        return Ok(response);
    }

    [HttpGet("github")]
    public IActionResult GitHubLogin()
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GitHubCallback))
        };
        return Challenge(properties, "GitHub");
    }

    [HttpGet("github/callback")]
    public async Task<ActionResult<OAuthPendingResponse>> GitHubCallback()
    {
        var authenticateResult = await HttpContext.AuthenticateAsync("GitHub");

        if (!authenticateResult.Succeeded)
            return Unauthorized(new { message = "GitHub authentication failed" });

        var response = await _oauthService.HandleOAuthCallbackAsync("github", authenticateResult.Principal, BuildVerificationBaseUrl());
        return Ok(response);
    }

    [HttpGet("microsoft")]
    public IActionResult MicrosoftLogin()
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(MicrosoftCallback))
        };
        return Challenge(properties, MicrosoftAccountDefaults.AuthenticationScheme);
    }

    [HttpGet("microsoft/callback")]
    public async Task<ActionResult<OAuthPendingResponse>> MicrosoftCallback()
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(MicrosoftAccountDefaults.AuthenticationScheme);

        if (!authenticateResult.Succeeded)
            return Unauthorized(new { message = "Microsoft authentication failed" });

        var response = await _oauthService.HandleOAuthCallbackAsync("microsoft", authenticateResult.Principal, BuildVerificationBaseUrl());
        return Ok(response);
    }

    private string BuildVerificationBaseUrl() => _verificationBaseUrl;
}
