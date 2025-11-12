# Authentication Setup Guide

## Files Created

### ✅ Models
- `Models/User.cs` - Updated with namespace
- `Models/RefreshToken.cs` - Token storage for JWT refresh

### ✅ DTOs
- `DTOs/AuthDto.cs` - Register, Login, Refresh, AuthResponse, UserDto, UpdateProfile

### ✅ Services
- `Services/IJwtService.cs` - JWT service interface
- `Services/JwtService.cs` - JWT generation, validation, refresh token management
- `Services/IAuthService.cs` - Auth service interface
- `Services/AuthService.cs` - Registration, Login, User management with PBKDF2 password hashing

### ✅ Controllers
- `Controllers/AuthController.cs` - `/api/auth/register`, `/api/auth/login`, `/api/auth/refresh`
- `Controllers/UsersController.cs` - `/api/users/me` (GET, PUT, DELETE)

### ✅ Configuration
- `ShopApI.csproj` - All NuGet packages added
- `appsettings.json` - JWT, Database, OAuth config
- `Program.cs` - Full JWT + OAuth setup with Serilog
- `Data/ApplicationDbContext.cs` - Updated with User and RefreshToken entities

## Next Steps

### 1. Restore Packages
```bash
dotnet restore
```

### 2. Update appsettings.json
Update the connection string if needed:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.;Database=ShopDB;Trusted_Connection=true;TrustServerCertificate=true;MultipleActiveResultSets=true"
}
```

### 3. Create Database Migration
```bash
dotnet ef migrations add InitialAuthSetup
dotnet ef database update
```

### 4. Run the Application
```bash
dotnet run
```

## API Endpoints

### Authentication
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/register` | Register new user |
| POST | `/api/auth/login` | Login with email/password |
| POST | `/api/auth/refresh` | Refresh JWT token |

### User Management (Requires JWT)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/users/me` | Get current user profile |
| PUT | `/api/users/me` | Update profile (email, password) |
| DELETE | `/api/users/me` | Soft delete account |

## Test the API

### 1. Register
```bash
POST http://localhost:5000/api/auth/register
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "SecurePassword123!"
}
```

### 2. Login
```bash
POST http://localhost:5000/api/auth/login
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "SecurePassword123!"
}
```

Response:
```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "base64string...",
  "expiresAt": "2025-11-13T10:15:00Z",
  "user": {
    "id": "guid",
    "email": "test@example.com",
    "role": "Customer",
    "provider": "local",
    "createdAt": "2025-11-13T09:00:00Z"
  }
}
```

### 3. Access Protected Endpoint
```bash
GET http://localhost:5000/api/users/me
Authorization: Bearer eyJhbGc...
```

## Security Features Implemented

✅ **Password Hashing**: PBKDF2 with 100,000 iterations  
✅ **JWT Authentication**: 15-minute access tokens  
✅ **Refresh Tokens**: 7-day expiry with revocation  
✅ **Soft Delete**: User deactivation instead of hard delete  
✅ **Global Exception Handling**: Custom middleware  
✅ **CORS Configuration**: Ready for frontend integration  

## OAuth Setup (Optional)

To enable Google/GitHub/Microsoft OAuth, update `appsettings.json`:

```json
"Authentication": {
  "Google": {
    "ClientId": "your-google-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-google-client-secret"
  },
  "GitHub": {
    "ClientId": "your-github-client-id",
    "ClientSecret": "your-github-client-secret"
  },
  "Microsoft": {
    "ClientId": "your-microsoft-client-id",
    "ClientSecret": "your-microsoft-client-secret"
  }
}
```

## Logging

Serilog is configured to log to:
- Console
- Seq (http://localhost:5341)

Install Seq for advanced logging (optional):
```bash
docker run -d --name seq -e ACCEPT_EULA=Y -p 5341:80 datalust/seq
```

## Database Schema

### Users Table
- Id (Guid, PK)
- Email (unique)
- PasswordHash
- Provider (local/google/github/microsoft)
- ProviderId
- Role
- IsActive
- CreatedAt
- UpdatedAt

### RefreshTokens Table
- Id (Guid, PK)
- UserId (FK to Users)
- Token (unique)
- ExpiresAt
- Revoked
- CreatedAt

## Troubleshooting

### Connection String Issues
If you get database connection errors, update the connection string in `appsettings.json`.

### JWT Secret Key
In production, use a strong secret key and store it securely (Azure Key Vault, AWS Secrets Manager, etc.).

### Migration Errors
```bash
# Remove all migrations
dotnet ef migrations remove

# Create fresh migration
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Next: Add OAuth Controllers

You still need to implement:
- `/api/auth/google` - Google OAuth redirect
- `/api/auth/github` - GitHub OAuth redirect
- `/api/auth/microsoft` - Microsoft OAuth redirect

These will be added in the next phase!
