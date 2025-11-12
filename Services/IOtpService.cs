using ShopApI.DTOs;
using ShopApI.Models;

namespace ShopApI.Services;

public interface IOtpService
{
    Task<OtpIssuanceResult> GenerateOtpAsync(User user, string ipAddress);
    Task<OtpEnvelope?> ResendOtpAsync(User user);
    Task<bool> ValidateOtpAsync(Guid userId, string otp);
    Task ClearOtpStateAsync(Guid userId);
}
