using ShopApI.DTOs;
using System.Security.Claims;

namespace ShopApI.Services;

public interface IOAuthService
{
    Task<AuthResponse> HandleOAuthCallbackAsync(string provider, ClaimsPrincipal principal);
}
