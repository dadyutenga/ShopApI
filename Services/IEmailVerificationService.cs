using ShopApI.Models;

namespace ShopApI.Services;

public interface IEmailVerificationService
{
    string IssueToken(User user, TimeSpan? lifetime = null);
    Guid? ValidateToken(string token);
}
