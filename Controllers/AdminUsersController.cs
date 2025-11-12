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

    public AdminUsersController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    [HttpPost("register-manager")]
    public async Task<ActionResult<UserDto>> RegisterManager([FromBody] RegisterManagerRequest request)
    {
        var actor = User.FindFirstValue(ClaimTypes.Email) ?? "admin";
        var user = await _adminUserService.RegisterManagerAsync(request, actor);
        return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
    }

    [HttpPost("register-support")]
    public async Task<ActionResult<UserDto>> RegisterSupport([FromBody] RegisterSupportRequest request)
    {
        var actor = User.FindFirstValue(ClaimTypes.Email) ?? "admin";
        var user = await _adminUserService.RegisterSupportAsync(request, actor);
        return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
    }

    [HttpPost("register-customer")]
    public async Task<ActionResult<UserDto>> RegisterCustomer([FromBody] RegisterCustomerRequest request)
    {
        var actor = User.FindFirstValue(ClaimTypes.Email) ?? "admin";
        var user = await _adminUserService.RegisterCustomerAsync(request, actor);
        return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
    }

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers([FromQuery] string? role, [FromQuery] string? status, [FromQuery] string? email)
    {
        var users = await _adminUserService.GetUsersAsync(role, status, email);
        return Ok(users);
    }

    [HttpGet("users/{id}")]
    public async Task<ActionResult<UserDto>> GetUserById(Guid id)
    {
        var user = await _adminUserService.GetUserAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    [HttpPatch("users/{id}/role")]
    public async Task<ActionResult<UserDto>> UpdateRole(Guid id, [FromBody] UpdateUserRoleRequest request)
    {
        var actor = User.FindFirstValue(ClaimTypes.Email) ?? "admin";
        var user = await _adminUserService.UpdateRoleAsync(id, request, actor);
        return Ok(user);
    }

    [HttpPatch("users/{id}/status")]
    public async Task<ActionResult<UserDto>> UpdateStatus(Guid id, [FromBody] UpdateUserStatusRequest request)
    {
        var actor = User.FindFirstValue(ClaimTypes.Email) ?? "admin";
        var user = await _adminUserService.UpdateStatusAsync(id, request, actor);
        return Ok(user);
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> SoftDelete(Guid id)
    {
        var actor = User.FindFirstValue(ClaimTypes.Email) ?? "admin";
        await _adminUserService.SoftDeleteAsync(id, actor);
        return NoContent();
    }
}
