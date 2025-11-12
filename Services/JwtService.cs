using Microsoft.IdentityModel.Tokens;
using ShopApI.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace ShopApI.Services;

public class JwtService : IJwtService
{
    private readonly ICacheService _cacheService;
    private readonly IKeyRotationService _keyRotationService;
    private readonly ILogger<JwtService> _logger;
    private static readonly TimeSpan ACCESS_TOKEN_EXPIRY = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan REFRESH_TOKEN_EXPIRY = TimeSpan.FromDays(7);

    public JwtService(
        ICacheService cacheService,
        IKeyRotationService keyRotationService,
        ILogger<JwtService> logger)
    {
        _cacheService = cacheService;
        _keyRotationService = keyRotationService;
        _logger = logger;
    }

    public async Task<string> GenerateAccessTokenAsync(User user)
    {
        var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "ShopAPI";
        var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "ShopAPIClient";

        var (signingKey, kid) = await _keyRotationService.GetCurrentSigningKeyAsync();
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var jti = Guid.NewGuid().ToString();

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("provider", user.Provider ?? "local"),
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.Add(ACCESS_TOKEN_EXPIRY),
            Issuer = jwtIssuer,
            Audience = jwtAudience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.SetDefaultTimesOnTokenCreation = false;
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        ((JwtSecurityToken)token).Header.Add("kid", kid);

        return tokenHandler.WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "ShopAPI";
        var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "ShopAPIClient";

        try
        {
            var jwtToken = tokenHandler.ReadJwtToken(token);
            var kid = jwtToken.Header.ContainsKey("kid") ? jwtToken.Header["kid"].ToString() : null;

            if (kid == null)
            {
                _logger.LogWarning("Token missing kid header");
                return null;
            }

            var signingKey = _keyRotationService.GetKeyByKidAsync(kid!).Result;
            
            if (signingKey == null)
            {
                _logger.LogWarning("Signing key not found for kid: {Kid}", kid);
                return null;
            }

            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            
            if (jti != null && IsTokenBlacklistedAsync(jti).Result)
            {
                _logger.LogWarning("Token is blacklisted: {Jti}", jti);
                return null;
            }

            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return null;
        }
    }

    public async Task SaveRefreshTokenAsync(Guid userId, string token)
    {
        var key = $"refresh:{token}";
        await _cacheService.SetAsync(key, userId.ToString(), REFRESH_TOKEN_EXPIRY);
    }

    public async Task<string?> ValidateRefreshTokenAsync(string token)
    {
        var key = $"refresh:{token}";
        var userId = await _cacheService.GetAsync<string>(key);
        return userId;
    }

    public async Task RevokeRefreshTokenAsync(string token)
    {
        var key = $"refresh:{token}";
        await _cacheService.RemoveAsync(key);
    }

    public async Task BlacklistTokenAsync(string jti, TimeSpan expiry)
    {
        var key = $"blacklist:{jti}";
        await _cacheService.SetAsync(key, true, expiry);
        _logger.LogInformation("Token blacklisted: {Jti}", jti);
    }

    public async Task<bool> IsTokenBlacklistedAsync(string jti)
    {
        var key = $"blacklist:{jti}";
        return await _cacheService.ExistsAsync(key);
    }
}
