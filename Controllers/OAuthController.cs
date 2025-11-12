using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Mvc;
using ShopApI.DTOs;
using ShopApI.Services;

namespace ShopApI.Controllers;

[ApiController]
[Route("api/auth")]
public class OAuthController : ControllerBase
{
    private readonly IOAuthService _oauthService;
    private readonly ILogger<OAuthController> _logger;

    public OAuthController(IOAuthService oauthService, ILogger<OAuthController> logger)
    {
        _oauthService = oauthService;
        _logger = logger;
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
    public async Task<ActionResult<AuthResponse>> GoogleCallback()
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
        
        if (!authenticateResult.Succeeded)
            return Unauthorized(new { message = "Google authentication failed" });

        var response = await _oauthService.HandleOAuthCallbackAsync("google", authenticateResult.Principal);
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
    public async Task<ActionResult<AuthResponse>> GitHubCallback()
    {
        var authenticateResult = await HttpContext.AuthenticateAsync("GitHub");
        
        if (!authenticateResult.Succeeded)
            return Unauthorized(new { message = "GitHub authentication failed" });

        var response = await _oauthService.HandleOAuthCallbackAsync("github", authenticateResult.Principal);
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
    public async Task<ActionResult<AuthResponse>> MicrosoftCallback()
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(MicrosoftAccountDefaults.AuthenticationScheme);
        
        if (!authenticateResult.Succeeded)
            return Unauthorized(new { message = "Microsoft authentication failed" });

        var response = await _oauthService.HandleOAuthCallbackAsync("microsoft", authenticateResult.Principal);
        return Ok(response);
    }
}
