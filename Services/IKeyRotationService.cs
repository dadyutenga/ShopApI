using Microsoft.IdentityModel.Tokens;

namespace ShopApI.Services;

public interface IKeyRotationService
{
    Task<(SecurityKey Key, string Kid)> GetCurrentSigningKeyAsync();
    Task<SecurityKey?> GetKeyByKidAsync(string kid);
    Task RotateKeysAsync();
}
