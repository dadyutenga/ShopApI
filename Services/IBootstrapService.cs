using ShopApI.DTOs;

namespace ShopApI.Services;

public interface IBootstrapService
{
    Task<BootstrapStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default);
    Task CompleteAsync(BootstrapCompleteRequest request, CancellationToken cancellationToken = default);
}
