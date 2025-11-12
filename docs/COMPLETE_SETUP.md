# 🚀 Complete ShopAPI Setup Guide

## Architecture Overview

This is a complete .NET 8 Web API with:

✅ **JWT Authentication** - Local + OAuth (Google, GitHub, Microsoft)  
✅ **Event-Driven Architecture** - MassTransit + RabbitMQ  
✅ **Caching** - Redis for sessions and rate limiting  
✅ **Logging & Telemetry** - Serilog + Seq + OpenTelemetry  
✅ **Rate Limiting** - IP-based for auth endpoints  
✅ **PBKDF2 Password Hashing** - 100k iterations  
✅ **Refresh Tokens** - 7-day expiry with revocation  

---

## 📦 Prerequisites

- .NET 8 SDK
- Docker Desktop (for infrastructure)
- Visual Studio / VS Code / Rider

---

## 🐳 Step 1: Start Infrastructure (Docker)

```bash
# Start all services
docker-compose up -d

# Verify services are running
docker-compose ps
```

**Services Started:**
- SQL Server (port 1433)
- Redis (port 6379)
- RabbitMQ (port 5672, Management UI: 15672)
- Seq (port 5341)

**Access Management UIs:**
- RabbitMQ: http://localhost:15672 (guest/guest)
- Seq: http://localhost:5341

---

## 📦 Step 2: Install NuGet Packages

```bash
dotnet restore
```

---

## 🗄️ Step 3: Database Setup

### Update Connection String (if needed)

Edit `appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=ShopDB;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;"
}
```

### Create Migration

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## ⚙️ Step 4: Configure Settings

### JWT Secret Key

Generate a strong secret (32+ characters) in `appsettings.json`:
```json
"Jwt": {
  "SecretKey": "YOUR-SUPER-SECRET-KEY-AT-LEAST-32-CHARS",
  "Issuer": "ShopAPI",
  "Audience": "ShopAPIClient"
}
```

### OAuth Providers (Optional)

1. **Google OAuth Setup:**
   - Go to [Google Cloud Console](https://console.cloud.google.com/)
   - Create OAuth 2.0 credentials
   - Add redirect URI: `https://localhost:7xxx/api/auth/google/callback`

2. **GitHub OAuth Setup:**
   - Go to GitHub Settings → Developer settings → OAuth Apps
   - Create new OAuth App
   - Add callback URL: `https://localhost:7xxx/api/auth/github/callback`

3. **Microsoft OAuth Setup:**
   - Go to [Azure Portal](https://portal.azure.com/)
   - Register an application
   - Add redirect URI: `https://localhost:7xxx/api/auth/microsoft/callback`

Update `appsettings.json`:
```json
"Authentication": {
  "Google": {
    "ClientId": "your-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-client-secret"
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

---

## 🏃 Step 5: Run the Application

```bash
dotnet run
```

The API will be available at:
- HTTPS: https://localhost:7xxx
- Swagger: https://localhost:7xxx/swagger

---

## 🧪 API Testing

### 1. Register a New User

```http
POST https://localhost:7xxx/api/auth/register
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "SecurePassword123!"
}
```

**Response:**
```json
{
  "accessToken": "eyJhbGciOi...",
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

### 2. Login

```http
POST https://localhost:7xxx/api/auth/login
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "SecurePassword123!"
}
```

### 3. Refresh Token

```http
POST https://localhost:7xxx/api/auth/refresh
Content-Type: application/json

{
  "refreshToken": "your-refresh-token"
}
```

### 4. Get Current User (Protected)

```http
GET https://localhost:7xxx/api/users/me
Authorization: Bearer eyJhbGciOi...
```

### 5. Update Profile (Protected)

```http
PUT https://localhost:7xxx/api/users/me
Authorization: Bearer eyJhbGciOi...
Content-Type: application/json

{
  "email": "newemail@example.com",
  "currentPassword": "SecurePassword123!",
  "newPassword": "NewSecurePassword456!"
}
```

### 6. Deactivate Account (Protected)

```http
DELETE https://localhost:7xxx/api/users/me
Authorization: Bearer eyJhbGciOi...
```

### 7. OAuth Login (Google)

```http
GET https://localhost:7xxx/api/auth/google
```

This will redirect to Google login, then return JWT tokens.

### 8. OAuth Login (GitHub)

```http
GET https://localhost:7xxx/api/auth/github
```

### 9. OAuth Login (Microsoft)

```http
GET https://localhost:7xxx/api/auth/microsoft
```

---

## 📊 Event-Driven Architecture

### Published Events

All events are published to the `user.events` exchange in RabbitMQ:

| Event                 | Trigger                    |
|-----------------------|----------------------------|
| `UserRegisteredEvent` | User registration          |
| `UserUpdatedEvent`    | Profile update             |
| `UserDeactivatedEvent`| Account deactivation       |

### Event Consumers

Located in `Consumers/UserEventConsumer.cs`:

- **UserRegisteredConsumer** - Process new user registrations
- **UserUpdatedConsumer** - Handle profile updates
- **UserDeactivatedConsumer** - Clean up deactivated accounts

### View RabbitMQ Events

1. Open http://localhost:15672
2. Login with guest/guest
3. Go to Exchanges → `user.events`
4. View queues and message flow

---

## 🔴 Redis Caching

### Cache Keys

| Key Pattern         | Purpose                    | TTL    |
|---------------------|----------------------------|--------|
| `session:{userId}`  | User session data          | 15 min |
| `rt:{token}`        | Refresh token lookup       | 7 days |
| `rate:auth:{ip}`    | Rate limit auth attempts   | 1 min  |

### Test Redis

```bash
# Connect to Redis
docker exec -it shopapi-redis redis-cli

# View all keys
KEYS *

# Get session data
GET session:{userId}

# Check rate limit
GET rate:auth:127.0.0.1
```

---

## 📝 Logging with Serilog & Seq

### View Logs

1. Open http://localhost:5341
2. View structured logs with:
   - Request ID
   - Correlation ID
   - User ID
   - Timestamps
   - Properties

### Log Enrichment

All requests are enriched with:
- `RequestId` - Unique per request
- `CorrelationId` - Track across services
- `UserId` - Current authenticated user
- `MachineName` - Server name
- `ThreadId` - Thread information

### Query Logs in Seq

```
RequestId = "abc123"
UserId <> "anonymous"
@Level = "Error"
```

---

## 🔒 Security Features

### Password Hashing

- **Algorithm:** PBKDF2-HMAC-SHA256
- **Iterations:** 100,000
- **Salt:** 128-bit random
- **Output:** 256-bit hash

### JWT Tokens

- **Access Token:** 15 minutes
- **Refresh Token:** 7 days
- **Algorithm:** HMAC-SHA256
- **Claims:** Sub, Email, Role, Provider, Jti

### Rate Limiting

- **Auth Endpoints:** 10 requests/minute per IP
- **Storage:** Redis
- **Response:** HTTP 429 Too Many Requests

---

## 🗂️ Project Structure

```
ShopApI/
├── Controllers/
│   ├── AuthController.cs      # Local auth (register, login, refresh)
│   ├── OAuthController.cs     # OAuth (Google, GitHub, Microsoft)
│   ├── UsersController.cs     # User profile management
│   └── ProductsController.cs  # Product endpoints
├── Services/
│   ├── AuthService.cs         # Auth business logic
│   ├── OAuthService.cs        # OAuth handling
│   ├── JwtService.cs          # JWT generation/validation
│   ├── EventPublisher.cs      # RabbitMQ event publishing
│   ├── RedisCacheService.cs   # Redis caching
│   ├── ProductService.cs      # Product business logic
│   └── Interfaces/
├── Repositories/
│   ├── ProductRepository.cs
│   └── Interfaces/
├── Models/
│   ├── User.cs
│   ├── RefreshToken.cs
│   └── Product.cs
├── DTOs/
│   ├── AuthDto.cs
│   └── ProductDto.cs
├── Events/
│   └── UserEvents.cs          # Event definitions
├── Consumers/
│   └── UserEventConsumer.cs   # MassTransit consumers
├── Middleware/
│   ├── ExceptionHandlingMiddleware.cs
│   ├── RequestLoggingMiddleware.cs
│   └── RateLimitMiddleware.cs
├── Data/
│   └── ApplicationDbContext.cs
├── docker-compose.yml         # Infrastructure setup
├── appsettings.json
└── Program.cs                 # Application startup
```

---

## 🧪 Test Rate Limiting

Run this script to test rate limiting:

```bash
# Make 15 rapid requests (should get rate limited after 10)
for i in {1..15}; do
  curl -X POST https://localhost:7xxx/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email":"test@test.com","password":"pass"}' \
    -w "\nStatus: %{http_code}\n"
done
```

---

## 🐛 Troubleshooting

### Database Connection Failed

```bash
# Check SQL Server is running
docker ps | grep sqlserver

# Test connection
docker exec -it shopapi-sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P YourStrong@Passw0rd
```

### Redis Connection Failed

```bash
# Check Redis is running
docker ps | grep redis

# Test connection
docker exec -it shopapi-redis redis-cli ping
# Should return: PONG
```

### RabbitMQ Connection Failed

```bash
# Check RabbitMQ is running
docker ps | grep rabbitmq

# View logs
docker logs shopapi-rabbitmq
```

### Migration Errors

```bash
# Remove all migrations
dotnet ef migrations remove

# Create fresh migration
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## 🚀 Next Steps

1. ✅ **Add Unit Tests** - xUnit + Moq
2. ✅ **Add Integration Tests** - WebApplicationFactory
3. ✅ **Add API Versioning**
4. ✅ **Add Health Checks** - /health endpoint
5. ✅ **Add Swagger Documentation** - XML comments
6. ✅ **Add Background Jobs** - Hangfire
7. ✅ **Add File Upload** - Azure Blob Storage
8. ✅ **Add Email Service** - SendGrid
9. ✅ **Add Payment Integration** - Stripe
10. ✅ **Deploy to Azure/AWS**

---

## 📚 Resources

- [ASP.NET Core Docs](https://docs.microsoft.com/aspnet/core)
- [MassTransit Docs](https://masstransit-project.com/)
- [Serilog Docs](https://serilog.net/)
- [Redis Docs](https://redis.io/docs/)
- [RabbitMQ Docs](https://www.rabbitmq.com/documentation.html)

---

## 📄 License

MIT License - Feel free to use this for learning or production!

---

**🎉 Your complete event-driven authentication system is ready!**
