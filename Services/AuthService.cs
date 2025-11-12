using Microsoft.EntityFrameworkCore;
using ShopApI.Data;
using ShopApI.DTOs;
using ShopApI.Events;
using ShopApI.Helpers;
using ShopApI.Models;
using ShopApI.Enums;

namespace ShopApI.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IOtpService _otpService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICacheService _cacheService;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ILogger<AuthService> _logger;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const int OtpRateLimit = 5;
    private static readonly TimeSpan OtpRateWindow = TimeSpan.FromMinutes(10);

    public AuthService(
        ApplicationDbContext context,
        IJwtService jwtService,
        IOtpService otpService,
        IEventPublisher eventPublisher,
        ICacheService cacheService,
        IEmailVerificationService emailVerificationService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _otpService = otpService;
        _eventPublisher = eventPublisher;
        _cacheService = cacheService;
        _emailVerificationService = emailVerificationService;
        _logger = logger;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();
        if (await _context.Users.AnyAsync(u => u.Email == normalizedEmail && !u.IsDeleted))
        {
            throw new InvalidOperationException("User with this email already exists");
        }

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = PasswordHasher.HashPassword(request.Password),
            Provider = "local",
            Role = request.Role,
            IsEmailVerified = false,
            IsActive = true,
            PhoneNumber = request.PhoneNumber
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var otp = await _otpService.GenerateAsync(user.Id, user.Email, "email_verification");

        await LogAuditAsync(user.Id, "user_registered", new { user.Email, user.Role });
        await _eventPublisher.PublishAsync(new UserRegisteredEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role.ToString(),
            Provider = user.Provider ?? "local",
            CorrelationId = Guid.NewGuid().ToString()
        });

        return new RegisterResponse(
            $"Registration successful. OTP valid until {otp.ExpiresAt:O}",
            user.Email);
    }

    public async Task<bool> VerifyEmailAsync(VerifyOtpRequest request)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted);
        if (user == null)
        {
            return false;
        }

        var result = await _otpService.ValidateAsync(user.Id, request.Otp);
        if (result != OtpValidationResult.Valid)
        {
            return false;
        }

        user.IsEmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await LogAuditAsync(user.Id, "email_verified", null);
        await _eventPublisher.PublishAsync(new EmailVerificationEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Type = "otp",
            Completed = true,
            CorrelationId = Guid.NewGuid().ToString()
        });

        return true;
    }

    public async Task<bool> VerifyEmailLinkAsync(string token)
    {
        var userId = _emailVerificationService.ValidateToken(token);
        if (userId == null)
        {
            return false;
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (user == null)
        {
            return false;
        }

        user.IsEmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await LogAuditAsync(user.Id, "email_verified", new { via = "signed_link" });
        await _eventPublisher.PublishAsync(new EmailVerificationEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Type = "oauth_link",
            Completed = true,
            CorrelationId = Guid.NewGuid().ToString()
        });

        return true;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();
        if (await IsAccountLockedAsync(normalizedEmail))
        {
            throw new UnauthorizedAccessException("Account locked due to failed login attempts");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.IsActive && !u.IsDeleted);

        if (user == null)
        {
            await IncrementFailedLoginAsync(normalizedEmail);
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        if (user.Role == Enums.UserRole.Customer)
        {
            throw new UnauthorizedAccessException("Customers must authenticate via OTP");
        }

        if (user.PasswordHash == null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            await IncrementFailedLoginAsync(normalizedEmail);
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        if (!user.IsEmailVerified)
        {
            throw new UnauthorizedAccessException("Please verify your email before logging in");
        }

        await ResetFailedLoginAsync(normalizedEmail);
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _cacheService.SetAsync($"session:{user.Id}", new
        {
            user.Id,
            user.Email,
            Role = user.Role.ToString()
        }, TimeSpan.FromMinutes(10));

        return await GenerateAuthResponseAsync(user);
    }

    public async Task RequestCustomerOtpAsync(OtpRequest request, string ipAddress)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.Role == Enums.UserRole.Customer && u.IsActive && !u.IsDeleted);

        if (user == null)
        {
            throw new InvalidOperationException("Customer not found or inactive");
        }

        if (!user.IsEmailVerified)
        {
            throw new InvalidOperationException("Customer email must be verified before OTP login");
        }

        await EnforceOtpRateLimit(ipAddress);

        if (!await _otpService.CanResendAsync(user.Id))
        {
            throw new InvalidOperationException("OTP recently sent. Please wait before requesting again");
        }

        var descriptor = await _otpService.GenerateAsync(user.Id, user.Email, "customer_auth");

        await LogAuditAsync(user.Id, "otp_generated", new { user.Email, descriptor.ExpiresAt });
        await _eventPublisher.PublishAsync(new OtpGeneratedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            CorrelationId = Guid.NewGuid().ToString()
        });
    }

    public async Task ResendCustomerOtpAsync(OtpRequest request)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.Role == Enums.UserRole.Customer);
        if (user == null)
        {
            throw new InvalidOperationException("Customer not found");
        }

        if (!await _otpService.CanResendAsync(user.Id))
        {
            throw new InvalidOperationException("Resend requested too soon");
        }

        var refreshed = await _otpService.RefreshAsync(user.Id);
        if (!refreshed)
        {
            await _otpService.GenerateAsync(user.Id, user.Email, "customer_auth");
        }

        await LogAuditAsync(user.Id, "otp_resend", new { user.Email });
    }

    public async Task<AuthResponse> VerifyCustomerOtpAsync(OtpVerifyRequest request, string ipAddress)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.Role == Enums.UserRole.Customer && u.IsActive && !u.IsDeleted);
        if (user == null)
        {
            throw new InvalidOperationException("Customer not found or inactive");
        }

        var result = await _otpService.ValidateAsync(user.Id, request.Otp);
        if (result == OtpValidationResult.Expired)
        {
            throw new InvalidOperationException("OTP expired");
        }
        if (result == OtpValidationResult.AttemptsExceeded)
        {
            throw new InvalidOperationException("Maximum OTP attempts exceeded");
        }
        if (result == OtpValidationResult.Invalid)
        {
            throw new InvalidOperationException("Invalid OTP provided");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _eventPublisher.PublishAsync(new OtpVerifiedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            CorrelationId = Guid.NewGuid().ToString()
        });

        await LogAuditAsync(user.Id, "otp_verified", new { ipAddress });
        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var userId = await _jwtService.ValidateRefreshTokenAsync(request.RefreshToken);
        if (userId == null)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        var user = await _context.Users.FindAsync(Guid.Parse(userId));
        if (user == null || !user.IsActive || user.IsDeleted)
        {
            throw new UnauthorizedAccessException("User not found or inactive");
        }

        await _jwtService.RevokeRefreshTokenAsync(request.RefreshToken);
        return await GenerateAuthResponseAsync(user);
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsActive || user.IsDeleted)
        {
            return null;
        }

        return MapToUserDto(user);
    }

    public async Task<bool> UpdateUserAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsActive)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(request.Email) && !request.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email && u.Id != userId))
            {
                throw new InvalidOperationException("Email already in use");
            }

            user.Email = request.Email.ToLowerInvariant();
            user.IsEmailVerified = false;
            await _otpService.GenerateAsync(user.Id, user.Email, "email_change");
        }

        if (!string.IsNullOrEmpty(request.PhoneNumber))
        {
            user.PhoneNumber = request.PhoneNumber;
        }

        if (!string.IsNullOrEmpty(request.NewPassword) && !string.IsNullOrEmpty(request.CurrentPassword))
        {
            if (user.PasswordHash == null || !PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Current password is incorrect");
            }

            user.PasswordHash = PasswordHasher.HashPassword(request.NewPassword);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await LogAuditAsync(user.Id, "user_profile_updated", new { user.Email, user.PhoneNumber });
        await _cacheService.RemoveAsync($"session:{user.Id}");
        return true;
    }

    public async Task DeactivateUserAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return;
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await LogAuditAsync(user.Id, "user_deactivated", null);
        await _eventPublisher.PublishAsync(new UserStatusChangedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            IsActive = user.IsActive,
            CorrelationId = Guid.NewGuid().ToString()
        });

        await _cacheService.RemoveAsync($"session:{user.Id}");
    }

    public async Task<bool> IsAccountLockedAsync(string email)
    {
        var key = $"loginfail:{email}";
        var attempts = await _cacheService.GetAsync<int>(key);
        return attempts >= MaxFailedAttempts;
    }

    public async Task IncrementFailedLoginAsync(string email)
    {
        var key = $"loginfail:{email}";
        var attempts = await _cacheService.IncrementAsync(key, LockoutDuration);
        if (attempts >= MaxFailedAttempts)
        {
            _logger.LogWarning("Account locked for {Email}", email);
        }
    }

    public async Task ResetFailedLoginAsync(string email)
    {
        await _cacheService.RemoveAsync($"loginfail:{email}");
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(User user)
    {
        var accessToken = await _jwtService.GenerateAccessTokenAsync(user);
        var refreshToken = _jwtService.GenerateRefreshToken();
        await _jwtService.SaveRefreshTokenAsync(user.Id, refreshToken);

        return new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(10),
            MapToUserDto(user));
    }

    private static UserDto MapToUserDto(User user) => new(
        user.Id,
        user.Email,
        user.Role.ToString(),
        user.Provider,
        user.CreatedAt,
        user.IsActive,
        user.IsEmailVerified,
        user.IsPhoneVerified,
        user.TwoFactorEnabled);

    private async Task LogAuditAsync(Guid userId, string action, object? metadata, bool immutable = false)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            Metadata = metadata != null ? System.Text.Json.JsonSerializer.Serialize(metadata) : null,
            IsImmutable = immutable,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    private async Task EnforceOtpRateLimit(string ipAddress)
    {
        var key = $"rate:otp:{ipAddress}";
        var attempts = await _cacheService.IncrementAsync(key, OtpRateWindow);
        if (attempts > OtpRateLimit)
        {
            throw new InvalidOperationException("OTP rate limit exceeded");
        }
    }
}
