using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShopApI.DTOs;
using ShopApI.Services;

namespace ShopApI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        try
        {
            var result = await _authService.VerifyEmailFromTokenAsync(token);

            if (!result)
                return BadRequest(new { message = "Invalid or expired token" });

            return Ok(new { message = "Email verified successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("request-otp")]
    public async Task<IActionResult> RequestOtp([FromBody] RequestOtpRequest request)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var envelope = await _authService.RequestCustomerOtpAsync(request, ipAddress);
            return Ok(new { envelope.ExpiresAt });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request)
    {
        try
        {
            var envelope = await _authService.ResendCustomerOtpAsync(request);
            return Ok(new { envelope.ExpiresAt });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        try
        {
            var response = await _authService.VerifyCustomerOtpAsync(request);
            SetRefreshTokenCookie(response.RefreshToken);
            return Ok(new
            {
                response.AccessToken,
                response.ExpiresAt,
                response.User
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            
            SetRefreshTokenCookie(response.RefreshToken);
            
            return Ok(new 
            {
                response.AccessToken,
                response.ExpiresAt,
                response.User
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        try
        {
            var refreshToken = Request.Cookies["refreshToken"];
            
            if (string.IsNullOrEmpty(refreshToken))
                return Unauthorized(new { message = "Refresh token not found" });

            var response = await _authService.RefreshTokenAsync(new RefreshTokenRequest(refreshToken));
            
            SetRefreshTokenCookie(response.RefreshToken);
            
            return Ok(new 
            {
                response.AccessToken,
                response.ExpiresAt,
                response.User
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("refreshToken");
        return Ok(new { message = "Logged out successfully" });
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7)
        };

        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }
}
