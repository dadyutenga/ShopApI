using ShopApI.DTOs;

namespace ShopApI.Services;

public interface IBootstrapService
{
    Task<BootstrapStatusResponse> GetStatusAsync();
    Task<UserDto> CompleteBootstrapAsync(BootstrapCompleteRequest request);
}
