using Microsoft.EntityFrameworkCore;
using ShopApI.Data;
using ShopApI.DTOs;
using ShopApI.Models;
using ShopApI.Events;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

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
            Email = request.Email,
            PasswordHash = HashPassword(request.Password),
            Provider = "local",
            Role = request.Role,
            EmailVerified = false
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var otp = await _otpService.GenerateOtpAsync(user.Email);
        
        _logger.LogInformation("User registered: {Email}, OTP: {Otp}", user.Email, otp);

        await _eventPublisher.PublishAsync(new UserRegisteredEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Provider = user.Provider ?? "local"
        });

        return new RegisterResponse(
            "Registration successful. Please verify your email with the OTP sent.",
            user.Email
        );
    }

    public async Task<bool> VerifyEmailAsync(VerifyOtpRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        
        if (user == null)
            return false;

        var isValid = await _otpService.ValidateOtpAsync(request.Email, request.Otp);
        
        if (isValid)
        {
            user.EmailVerified = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Email verified for {Email}", user.Email);
        }

        return isValid;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        if (await IsAccountLockedAsync(request.Email))
        {
            throw new UnauthorizedAccessException("Account is temporarily locked due to too many failed login attempts");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

        if (user == null || !VerifyPassword(request.Password, user.PasswordHash!))
        {
            await IncrementFailedLoginAsync(request.Email);
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        if (!user.EmailVerified)
        {
            throw new UnauthorizedAccessException("Please verify your email before logging in");
        }

        await ResetFailedLoginAsync(request.Email);
        
        _logger.LogInformation("User logged in: {Email}", user.Email);

        await _cacheService.SetAsync($"session:{user.Id}", new
        {
            user.Id,
            user.Email,
            Role = user.Role.ToString()
        }, TimeSpan.FromMinutes(10));

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
        
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("User not found or inactive");
        }

        await _jwtService.RevokeRefreshTokenAsync(request.RefreshToken);

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        
        if (user == null || !user.IsActive)
            return null;

        return MapToUserDto(user);
    }

    public async Task<bool> UpdateUserAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        
        if (user == null || !user.IsActive)
            return false;

        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                throw new InvalidOperationException("Email already in use");
            
            user.Email = request.Email;
            user.EmailVerified = false;
            
            var otp = await _otpService.GenerateOtpAsync(user.Email);
            _logger.LogInformation("Email update OTP for {Email}: {Otp}", user.Email, otp);
        }

        if (!string.IsNullOrEmpty(request.NewPassword) && !string.IsNullOrEmpty(request.CurrentPassword))
        {
            if (!VerifyPassword(request.CurrentPassword, user.PasswordHash!))
                throw new UnauthorizedAccessException("Current password is incorrect");
            
            user.PasswordHash = HashPassword(request.NewPassword);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _eventPublisher.PublishAsync(new UserUpdatedEvent
        {
            UserId = user.Id,
            Email = user.Email
        });

        await _cacheService.RemoveAsync($"session:{user.Id}");

        return true;
    }

    public async Task DeactivateUserAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        
        if (user != null)
        {
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User deactivated: {UserId}", userId);

            await _eventPublisher.PublishAsync(new UserDeactivatedEvent
            {
                UserId = user.Id,
                Email = user.Email
            });

            await _cacheService.RemoveAsync($"session:{user.Id}");
        }
    }

    public async Task<bool> IsAccountLockedAsync(string email)
    {
        var key = $"loginfail:{email}";
        var attempts = await _cacheService.GetAsync<int>(key);
        return attempts >= MAX_FAILED_ATTEMPTS;
    }

    public async Task IncrementFailedLoginAsync(string email)
    {
        var key = $"loginfail:{email}";
        var attempts = await _cacheService.IncrementAsync(key);
        
        if (attempts == 1)
        {
            await _cacheService.SetAsync(key, attempts, LOCKOUT_DURATION);
        }

        if (attempts >= MAX_FAILED_ATTEMPTS)
        {
            _logger.LogWarning("Account locked for {Email} due to {Attempts} failed attempts", email, attempts);
        }
    }

    public async Task ResetFailedLoginAsync(string email)
    {
        var key = $"loginfail:{email}";
        await _cacheService.RemoveAsync(key);
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
            MapToUserDto(user)
        );
    }

    private static UserDto MapToUserDto(User user) => new(
        user.Id,
        user.Email,
        user.Role.ToString(),
        user.Provider,
        user.CreatedAt
    );

    private static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(128 / 8);
        
        string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100000,
            numBytesRequested: 256 / 8));

        return $"{Convert.ToBase64String(salt)}.{hashed}";
    }

    private static bool VerifyPassword(string password, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var storedHash = parts[1];

        string computedHash = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100000,
            numBytesRequested: 256 / 8));

        return computedHash == storedHash;
    }
}
