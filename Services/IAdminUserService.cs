using ShopApI.DTOs;

namespace ShopApI.Services;

public interface IAdminUserService
{
    Task<UserDto> RegisterManagerAsync(RegisterManagerRequest request, string actorEmail);
    Task<UserDto> RegisterSupportAsync(RegisterSupportRequest request, string actorEmail);
    Task<UserDto> RegisterCustomerAsync(RegisterCustomerRequest request, string actorEmail);
    Task<IEnumerable<UserDto>> GetUsersAsync(string? role, string? status, string? email);
    Task<UserDto?> GetUserAsync(Guid id);
    Task<UserDto> UpdateRoleAsync(Guid id, UpdateUserRoleRequest request, string actorEmail);
    Task<UserDto> UpdateStatusAsync(Guid id, UpdateUserStatusRequest request, string actorEmail);
    Task SoftDeleteAsync(Guid id, string actorEmail);
}
