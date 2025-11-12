using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopApI.DTOs;
using ShopApI.Services;
using System.Security.Claims;

namespace ShopApI.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin")]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(IAdminUserService adminUserService, ILogger<AdminUsersController> logger)
    {
        _adminUserService = adminUserService;
        _logger = logger;
    }

    [HttpPost("register-manager")]
    public async Task<ActionResult<UserDto>> RegisterManager([FromBody] AdminRegisterManagerRequest request)
        => Ok(await _adminUserService.RegisterManagerAsync(GetActorId(), request));

    [HttpPost("register-support")]
    public async Task<ActionResult<UserDto>> RegisterSupport([FromBody] AdminRegisterSupportRequest request)
        => Ok(await _adminUserService.RegisterSupportAsync(GetActorId(), request));

    [HttpPost("register-customer")]
    public async Task<ActionResult<UserDto>> RegisterCustomer([FromBody] AdminRegisterCustomerRequest request)
        => Ok(await _adminUserService.RegisterCustomerAsync(GetActorId(), request));

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers([FromQuery] UserQueryParameters parameters)
        => Ok(await _adminUserService.GetUsersAsync(parameters));

    [HttpGet("users/{id}")]
    public async Task<ActionResult<UserDto>> GetUser(Guid id)
    {
        var user = await _adminUserService.GetUserAsync(id);
        return user == null ? NotFound() : Ok(user);
    }

    [HttpPatch("users/{id}/role")]
    public async Task<ActionResult<UserDto>> UpdateRole(Guid id, [FromBody] UpdateUserRoleRequest request)
        => Ok(await _adminUserService.UpdateRoleAsync(GetActorId(), id, request));

    [HttpPatch("users/{id}/status")]
    public async Task<ActionResult<UserDto>> UpdateStatus(Guid id, [FromBody] UpdateUserStatusRequest request)
        => Ok(await _adminUserService.UpdateStatusAsync(GetActorId(), id, request));

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> SoftDelete(Guid id, [FromBody] SoftDeleteUserRequest request)
    {
        await _adminUserService.SoftDeleteUserAsync(GetActorId(), id, request);
        return NoContent();
    }

    private Guid GetActorId()
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
