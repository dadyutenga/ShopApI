using Microsoft.EntityFrameworkCore;
using ShopApI.Data;
using ShopApI.DTOs;
using ShopApI.Enums;
using ShopApI.Events;
using ShopApI.Helpers;
using ShopApI.Models;

namespace ShopApI.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IOtpService _otpService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICacheService _cacheService;
    private readonly ILogger<AuthService> _logger;

    private const int MAX_FAILED_ATTEMPTS = 5;
    private static readonly TimeSpan LOCKOUT_DURATION = TimeSpan.FromMinutes(15);

    public AuthService(
        ApplicationDbContext context,
        IJwtService jwtService,
        IOtpService otpService,
        IEventPublisher eventPublisher,
        ICacheService cacheService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _otpService = otpService;
        _eventPublisher = eventPublisher;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            throw new InvalidOperationException("User with this email already exists");
        }

        var user = new User
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = PasswordHasher.HashPassword(request.Password),
            Role = request.Role,
            Provider = "local",
            IsActive = true,
            IsEmailVerified = request.Role != UserRole.Customer,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (request.Role == UserRole.Customer)
        {
            user.CustomerProfile = new CustomerProfile
            {
                IsEmailVerified = false,
                IsPhoneVerified = false,
                TwoFactorEnabled = false
            };
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var correlationId = Guid.NewGuid().ToString();
        await _eventPublisher.PublishAsync(new UserRegisteredEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role.ToString(),
            Provider = "local",
            CorrelationId = correlationId
        });

        return new RegisterResponse("Registration successful", user.Email);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        if (await IsAccountLockedAsync(request.Email))
        {
            throw new UnauthorizedAccessException("Account is temporarily locked due to too many failed login attempts");
        }

        var user = await _context.Users
            .Include(u => u.CustomerProfile)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || user.Role == UserRole.Customer)
        {
            throw new UnauthorizedAccessException("Password login is not enabled for this user");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Account is inactive");
        }

        if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            await IncrementFailedLoginAsync(request.Email);
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        if (!user.IsEmailVerified)
        {
            throw new UnauthorizedAccessException("Email verification required");
        }

        await ResetFailedLoginAsync(request.Email);
        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var userId = await _jwtService.ValidateRefreshTokenAsync(request.RefreshToken);
        if (userId == null)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        var user = await _context.Users.Include(u => u.CustomerProfile).FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("User not found or inactive");
        }

        await _jwtService.RevokeRefreshTokenAsync(request.RefreshToken);
        return await GenerateAuthResponseAsync(user);
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        var user = await _context.Users.Include(u => u.CustomerProfile).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || !user.IsActive)
        {
            return null;
        }

        return MapToUserDto(user);
    }

    public async Task<bool> UpdateUserAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _context.Users.Include(u => u.CustomerProfile).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || !user.IsActive)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                throw new InvalidOperationException("Email already in use");

            user.Email = request.Email.Trim().ToLowerInvariant();
            user.IsEmailVerified = false;
            if (user.CustomerProfile != null)
            {
                user.CustomerProfile.IsEmailVerified = false;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || !PasswordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
                throw new UnauthorizedAccessException("Current password is incorrect");

            user.PasswordHash = PasswordHasher.HashPassword(request.NewPassword);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _eventPublisher.PublishAsync(new UserUpdatedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Event = "profile.updated",
            CorrelationId = Guid.NewGuid().ToString()
        });

        await _cacheService.RemoveAsync($"session:{user.Id}");
        return true;
    }

    public async Task DeactivateUserAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return;

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _eventPublisher.PublishAsync(new UserDeactivatedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Reason = "self.deactivated",
            CorrelationId = Guid.NewGuid().ToString()
        });

        await _cacheService.RemoveAsync($"session:{user.Id}");
    }

    public async Task<bool> IsAccountLockedAsync(string email)
    {
        var key = $"loginfail:{email}";
        var attempts = await _cacheService.GetAsync<int>(key) ?? 0;
        return attempts >= MAX_FAILED_ATTEMPTS;
    }

    public async Task IncrementFailedLoginAsync(string email)
    {
        var key = $"loginfail:{email}";
        var attempts = await _cacheService.IncrementAsync(key, LOCKOUT_DURATION);
        if (attempts >= MAX_FAILED_ATTEMPTS)
        {
            _logger.LogWarning("Account locked for {Email}", MaskingHelper.MaskEmail(email));
        }
    }

    public async Task ResetFailedLoginAsync(string email)
    {
        await _cacheService.RemoveAsync($"loginfail:{email}");
    }

    public async Task<OtpEnvelope> RequestCustomerOtpAsync(RequestOtpRequest request, string ipAddress)
    {
        var user = await _context.Users.Include(u => u.CustomerProfile)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.Role == UserRole.Customer);

        if (user == null)
            throw new UnauthorizedAccessException("User not found");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is inactive");

        if (!(user.CustomerProfile?.IsEmailVerified ?? user.IsEmailVerified))
            throw new UnauthorizedAccessException("Email verification pending");

        var correlationId = Guid.NewGuid().ToString();
        var issuance = await _otpService.GenerateOtpAsync(user, ipAddress);

        await _eventPublisher.PublishAsync(new OtpGeneratedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            ExpiresAt = issuance.Envelope.ExpiresAt,
            CorrelationId = correlationId
        });

        _logger.LogInformation("OTP issued for {Email}", MaskingHelper.MaskEmail(user.Email));
        return issuance.Envelope;
    }

    public async Task<OtpEnvelope> ResendCustomerOtpAsync(ResendOtpRequest request)
    {
        var user = await _context.Users.Include(u => u.CustomerProfile)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.Role == UserRole.Customer);

        if (user == null)
            throw new UnauthorizedAccessException("User not found");

        var envelope = await _otpService.ResendOtpAsync(user);
        if (envelope == null)
            throw new InvalidOperationException("Unable to resend OTP yet");

        await _eventPublisher.PublishAsync(new OtpGeneratedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            ExpiresAt = envelope.ExpiresAt,
            CorrelationId = Guid.NewGuid().ToString()
        });

        return envelope;
    }

    public async Task<AuthResponse> VerifyCustomerOtpAsync(VerifyOtpRequest request)
    {
        var user = await _context.Users.Include(u => u.CustomerProfile)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.Role == UserRole.Customer);

        if (user == null)
            throw new UnauthorizedAccessException("User not found");

        var isValid = await _otpService.ValidateOtpAsync(user.Id, request.Otp);
        if (!isValid)
            throw new UnauthorizedAccessException("Invalid OTP");

        await _eventPublisher.PublishAsync(new OtpVerifiedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            CorrelationId = Guid.NewGuid().ToString()
        });

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<bool> VerifyEmailFromTokenAsync(string token)
    {
        var signingKey = Environment.GetEnvironmentVariable("EMAIL_VERIFICATION_SIGNING_KEY");
        if (string.IsNullOrEmpty(signingKey))
            throw new InvalidOperationException("EMAIL_VERIFICATION_SIGNING_KEY is not configured");

        var payload = EmailVerificationTokenHelper.TryParse(token, signingKey);
        if (payload == null)
            return false;

        var user = await _context.Users.Include(u => u.CustomerProfile)
            .FirstOrDefaultAsync(u => u.Id == payload.UserId);

        if (user == null)
            return false;

        user.IsEmailVerified = true;
        if (user.CustomerProfile != null)
        {
            user.CustomerProfile.IsEmailVerified = true;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _eventPublisher.PublishAsync(new EmailVerificationCompletedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Provider = payload.Provider,
            CorrelationId = Guid.NewGuid().ToString()
        });

        return true;
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(User user)
    {
        var accessToken = await _jwtService.GenerateAccessTokenAsync(user);
        var refreshToken = _jwtService.GenerateRefreshToken();
        await _jwtService.SaveRefreshTokenAsync(user.Id, refreshToken);

        await _cacheService.SetAsync($"session:{user.Id}", new
        {
            user.Id,
            user.Email,
            Role = user.Role.ToString()
        }, TimeSpan.FromMinutes(10));

        return new AuthResponse(accessToken, refreshToken, DateTime.UtcNow.AddMinutes(10), MapToUserDto(user));
    }

    private static UserDto MapToUserDto(User user)
        => new(
            user.Id,
            user.Email,
            user.Role.ToString(),
            user.IsActive,
            user.IsEmailVerified,
            user.CreatedAt,
            user.CustomerProfile == null
                ? null
                : new CustomerProfileDto(
                    user.CustomerProfile.PhoneNumber,
                    user.CustomerProfile.IsPhoneVerified,
                    user.CustomerProfile.IsEmailVerified,
                    user.CustomerProfile.TwoFactorEnabled));
}
