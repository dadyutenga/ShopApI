using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopApI.DTOs;
using ShopApI.Services;

namespace ShopApI.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/bootstrap")]
public class BootstrapController : ControllerBase
{
    private readonly IBootstrapService _bootstrapService;
    private readonly ILogger<BootstrapController> _logger;

    public BootstrapController(IBootstrapService bootstrapService, ILogger<BootstrapController> logger)
    {
        _bootstrapService = bootstrapService;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<ActionResult<BootstrapStatusResponse>> GetStatus()
        => Ok(await _bootstrapService.GetStatusAsync());

    [HttpPost("complete")]
    public async Task<ActionResult<UserDto>> Complete([FromBody] BootstrapCompleteRequest request)
    {
        try
        {
            var user = await _bootstrapService.CompleteBootstrapAsync(request);
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bootstrap completion failed");
            return BadRequest(new { message = ex.Message });
        }
    }
}
