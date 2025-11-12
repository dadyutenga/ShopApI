using ShopApI.DTOs;
using System.Security.Claims;

namespace ShopApI.Services;

public interface IOAuthService
{
    Task<OAuthCallbackResult> HandleOAuthCallbackAsync(string provider, ClaimsPrincipal principal);
}
