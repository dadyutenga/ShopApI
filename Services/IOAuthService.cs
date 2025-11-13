using ShopApI.DTOs;
using System.Security.Claims;

namespace ShopApI.Services;

public interface IOAuthService
{
    Task<OAuthPendingResponse> HandleOAuthCallbackAsync(string provider, ClaimsPrincipal principal, string verificationBaseUrl);
}
