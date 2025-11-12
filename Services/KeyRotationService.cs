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

        var material = await _cacheService.GetAsync<RsaKeyMaterial>($"{KEY_PREFIX}{currentKid}");

        if (material == null)
        {
            return await CreateNewKeyAsync();
        }

        var key = CreatePrivateKey(material, currentKid);
        return (key, currentKid);
    }

    public async Task<SecurityKey?> GetKeyByKidAsync(string kid)
    {
        var material = await _cacheService.GetAsync<RsaKeyMaterial>($"{KEY_PREFIX}{kid}");

        if (material == null)
            return null;

        var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(material.PublicKey), out _);
        return new RsaSecurityKey(rsa) { KeyId = kid };
    }

    public async Task RotateKeysAsync()
    {
        await CreateNewKeyAsync();
        _logger.LogInformation("JWT signing keys rotated");
    }

    private async Task<(SecurityKey Key, string Kid)> CreateNewKeyAsync()
    {
        var kid = Guid.NewGuid().ToString();
        using var rsa = RSA.Create(2048);
        var material = new RsaKeyMaterial
        {
            PrivateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey()),
            PublicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo())
        };

        await _cacheService.SetAsync($"{KEY_PREFIX}{kid}", material, TimeSpan.FromDays(60));
        await _cacheService.SetAsync(CURRENT_KID_KEY, kid, TimeSpan.FromDays(60));

        _logger.LogInformation("New JWT signing key created with kid: {Kid}", kid);

        var key = CreatePrivateKey(material, kid);
        return (key, kid);
    }

    private static SecurityKey CreatePrivateKey(RsaKeyMaterial material, string kid)
    {
        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(Convert.FromBase64String(material.PrivateKey), out _);
        return new RsaSecurityKey(rsa) { KeyId = kid };
    }

    private record RsaKeyMaterial
    {
        public string PrivateKey { get; init; } = string.Empty;
        public string PublicKey { get; init; } = string.Empty;
    }
}
