using Microsoft.AspNetCore.Mvc;
using ShopApI.DTOs;
using ShopApI.Services;

namespace ShopApI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BootstrapController : ControllerBase
{
    private readonly IBootstrapService _bootstrapService;

    public BootstrapController(IBootstrapService bootstrapService)
    {
        _bootstrapService = bootstrapService;
    }

    [HttpGet("status")]
    public async Task<ActionResult<BootstrapStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _bootstrapService.GetStatusAsync(cancellationToken);
        return Ok(status);
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody] BootstrapCompleteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _bootstrapService.CompleteAsync(request, cancellationToken);
            return Ok(new { message = "Bootstrap completed" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
