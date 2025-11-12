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
    private readonly IEventPublisher _eventPublisher;
    private readonly ICacheService _cacheService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        ApplicationDbContext context,
        IJwtService jwtService,
        IEventPublisher eventPublisher,
        ICacheService cacheService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _eventPublisher = eventPublisher;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Check if user exists
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            throw new InvalidOperationException("User with this email already exists");
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = HashPassword(request.Password),
            Provider = "local",
            Role = "Customer"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User registered: {Email}", user.Email);

        // Publish user.registered event
        await _eventPublisher.PublishAsync(new UserRegisteredEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Provider = user.Provider ?? "local"
        });

        // Cache user session
        await _cacheService.SetAsync($"session:{user.Id}", new
        {
            user.Id,
            user.Email,
            user.Role
        }, TimeSpan.FromMinutes(15));

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

        if (user == null || !VerifyPassword(request.Password, user.PasswordHash!))
        {
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        _logger.LogInformation("User logged in: {Email}", user.Email);

        // Cache user session
        await _cacheService.SetAsync($"session:{user.Id}", new
        {
            user.Id,
            user.Email,
            user.Role
        }, TimeSpan.FromMinutes(15));

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var refreshToken = await _jwtService.ValidateRefreshTokenAsync(request.RefreshToken);
        
        if (refreshToken == null)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        var user = await _context.Users.FindAsync(refreshToken.UserId);
        
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("User not found or inactive");
        }

        // Revoke old refresh token
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
        }

        if (!string.IsNullOrEmpty(request.NewPassword) && !string.IsNullOrEmpty(request.CurrentPassword))
        {
            if (!VerifyPassword(request.CurrentPassword, user.PasswordHash!))
                throw new UnauthorizedAccessException("Current password is incorrect");
            
            user.PasswordHash = HashPassword(request.NewPassword);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Publish user.updated event
        await _eventPublisher.PublishAsync(new UserUpdatedEvent
        {
            UserId = user.Id,
            Email = user.Email
        });

        // Invalidate cache
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

            // Publish user.deactivated event
            await _eventPublisher.PublishAsync(new UserDeactivatedEvent
            {
                UserId = user.Id,
                Email = user.Email
            });

            // Invalidate cache
            await _cacheService.RemoveAsync($"session:{user.Id}");
        }
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(User user)
    {
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();
        
        await _jwtService.SaveRefreshTokenAsync(user.Id, refreshToken);

        return new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(15),
            MapToUserDto(user)
        );
    }

    private static UserDto MapToUserDto(User user) => new(
        user.Id,
        user.Email,
        user.Role,
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
