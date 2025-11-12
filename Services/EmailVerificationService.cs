using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ShopApI.Models;

namespace ShopApI.Services;

public class EmailVerificationService : IEmailVerificationService
{
    private readonly string _secret;
    private readonly ILogger<EmailVerificationService> _logger;

    public EmailVerificationService(IConfiguration configuration, ILogger<EmailVerificationService> logger)
    {
        _secret = configuration["EMAIL_VERIFICATION_SECRET"] ?? "change-me";
        _logger = logger;
    }

    public string IssueToken(User user, TimeSpan? lifetime = null)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email)
            }),
            Expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1)),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    public Guid? ValidateToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                IssuerSigningKey = key,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(5)
            }, out _);

            var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return userId != null ? Guid.Parse(userId) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate email verification token");
            return null;
        }
    }
}
