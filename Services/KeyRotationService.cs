using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ShopApI.Services;

public class KeyRotationService : IKeyRotationService
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<KeyRotationService> _logger;
    private const string CURRENT_KID_KEY = "jwt:current_kid";
    private const string KEY_PREFIX = "jwt:key:";
    private static readonly TimeSpan KEY_EXPIRY = TimeSpan.FromDays(30);

    public KeyRotationService(ICacheService cacheService, ILogger<KeyRotationService> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<(SecurityKey Key, string Kid)> GetCurrentSigningKeyAsync()
    {
        var currentKid = await _cacheService.GetAsync<string>(CURRENT_KID_KEY);
        
        if (currentKid == null)
        {
            return await CreateNewKeyAsync();
        }

        var keyData = await _cacheService.GetAsync<string>($"{KEY_PREFIX}{currentKid}");
        
        if (keyData == null)
        {
            return await CreateNewKeyAsync();
        }

        var key = new SymmetricSecurityKey(Convert.FromBase64String(keyData));
        return (key, currentKid);
    }

    public async Task<SecurityKey?> GetKeyByKidAsync(string kid)
    {
        var keyData = await _cacheService.GetAsync<string>($"{KEY_PREFIX}{kid}");
        
        if (keyData == null)
            return null;

        return new SymmetricSecurityKey(Convert.FromBase64String(keyData));
    }

    public async Task RotateKeysAsync()
    {
        await CreateNewKeyAsync();
        _logger.LogInformation("JWT signing keys rotated");
    }

    private async Task<(SecurityKey Key, string Kid)> CreateNewKeyAsync()
    {
        var keyBytes = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(keyBytes);
        }

        var kid = Guid.NewGuid().ToString();
        var keyData = Convert.ToBase64String(keyBytes);
        
        await _cacheService.SetAsync($"{KEY_PREFIX}{kid}", keyData, TimeSpan.FromDays(60));
        await _cacheService.SetAsync(CURRENT_KID_KEY, kid, TimeSpan.FromDays(60));
        
        _logger.LogInformation("New JWT signing key created with kid: {Kid}", kid);
        
        var key = new SymmetricSecurityKey(keyBytes);
        return (key, kid);
    }
}
