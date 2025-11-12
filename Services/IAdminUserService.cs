using ShopApI.DTOs;

namespace ShopApI.Services;

public interface IAdminUserService
{
    Task<UserDto> RegisterManagerAsync(Guid actorId, AdminRegisterManagerRequest request);
    Task<UserDto> RegisterSupportAsync(Guid actorId, AdminRegisterSupportRequest request);
    Task<UserDto> RegisterCustomerAsync(Guid actorId, AdminRegisterCustomerRequest request);
    Task<IEnumerable<UserDto>> GetUsersAsync(UserQueryParameters parameters);
    Task<UserDto?> GetUserAsync(Guid userId);
    Task<UserDto> UpdateRoleAsync(Guid actorId, Guid userId, UpdateUserRoleRequest request);
    Task<UserDto> UpdateStatusAsync(Guid actorId, Guid userId, UpdateUserStatusRequest request);
    Task SoftDeleteUserAsync(Guid actorId, Guid userId, SoftDeleteUserRequest request);
}
