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

        var material = JsonSerializer.Deserialize<KeyMaterial>(keyData);
        if (material == null)
        {
            return await CreateNewKeyAsync();
        }

        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(Convert.FromBase64String(material.PrivateKey), out _);
        return (new RsaSecurityKey(rsa) { KeyId = currentKid }, currentKid);
    }

    public async Task<SecurityKey?> GetKeyByKidAsync(string kid)
    {
        var keyData = await _cacheService.GetAsync<string>($"{KEY_PREFIX}{kid}");

        if (keyData == null)
            return null;

        var material = JsonSerializer.Deserialize<KeyMaterial>(keyData);
        if (material == null)
        {
            return null;
        }

        var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(Convert.FromBase64String(material.PublicKey), out _);
        return new RsaSecurityKey(rsa) { KeyId = kid };
    }

    public async Task RotateKeysAsync()
    {
        await CreateNewKeyAsync();
        _logger.LogInformation("JWT signing keys rotated");
    }

    private async Task<(SecurityKey Key, string Kid)> CreateNewKeyAsync()
    {
        using var rsa = RSA.Create(4096);
        var kid = Guid.NewGuid().ToString();
        var material = new KeyMaterial
        {
            PrivateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey()),
            PublicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey())
        };

        var serialized = JsonSerializer.Serialize(material);
        await _cacheService.SetAsync($"{KEY_PREFIX}{kid}", serialized, TimeSpan.FromDays(60));
        await _cacheService.SetAsync(CURRENT_KID_KEY, kid, TimeSpan.FromDays(60));

        _logger.LogInformation("New JWT signing key created with kid: {Kid}", kid);

        var signingKey = new RsaSecurityKey(rsa.ExportParameters(true)) { KeyId = kid };
        return (signingKey, kid);
    }

    private sealed class KeyMaterial
    {
        public string PrivateKey { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty;
    }
}
