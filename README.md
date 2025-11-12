# ShopAPI - .NET 8 Web API Project

## Project Structure

```
ShopApI/
├── Controllers/          # API Controllers (HTTP endpoints)
│   └── ProductsController.cs
├── Models/              # Domain/Entity models
│   └── Product.cs
├── DTOs/                # Data Transfer Objects (API contracts)
│   └── ProductDto.cs
├── Services/            # Business logic layer
│   ├── IProductService.cs
│   └── ProductService.cs
├── Repositories/        # Data access layer
│   ├── IProductRepository.cs
│   └── ProductRepository.cs
├── Data/                # Database context and configurations
│   └── ApplicationDbContext.cs
├── Middleware/          # Custom middleware
│   └── ExceptionHandlingMiddleware.cs
├── Helpers/             # Utility classes and helpers
├── Properties/          # Launch settings
├── Program.cs           # Application entry point
└── appsettings.json     # Configuration files
```

## Folder Descriptions

### 📁 Controllers/
Contains API controllers that handle HTTP requests and responses.
- Define your API endpoints here
- Controllers should be lightweight and delegate business logic to services
- Use attribute routing: `[Route("api/[controller]")]`

**Example:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    // Your endpoints here
}
```

### 📁 Models/
Contains domain entities that represent your database tables.
- These are your core business objects
- Map directly to database tables when using Entity Framework

**Example:**
```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    // Other properties...
}
```

### 📁 DTOs/ (Data Transfer Objects)
Contains objects used for API requests and responses.
- Separate from domain models for security and flexibility
- Include: CreateDto, UpdateDto, ResponseDto
- Helps prevent over-posting attacks

**Example:**
```csharp
public class CreateProductDto { /* properties */ }
public class ProductDto { /* properties */ }
```

### 📁 Services/
Contains business logic and orchestrates operations.
- Implements interfaces for dependency injection
- Handles complex business rules
- Coordinates between repositories

**Example:**
```csharp
public interface IProductService { /* methods */ }
public class ProductService : IProductService { /* implementation */ }
```

### 📁 Repositories/
Contains data access logic.
- Abstracts database operations
- Implements the Repository pattern
- Works with DbContext (Entity Framework)

**Example:**
```csharp
public interface IProductRepository { /* methods */ }
public class ProductRepository : IProductRepository { /* implementation */ }
```

### 📁 Data/
Contains database context and configurations.
- ApplicationDbContext for Entity Framework
- Entity configurations
- Database migrations

### 📁 Middleware/
Contains custom middleware components.
- Exception handling
- Authentication/Authorization
- Logging
- Request/Response modification

### 📁 Helpers/
Contains utility classes and helper methods.
- Extension methods
- Mapping helpers
- Validation utilities
- Common functions

## Getting Started

### Prerequisites
- .NET 8 SDK
- Your preferred IDE (Visual Studio, VS Code, Rider)

### Running the Application

```bash
dotnet restore
dotnet build
dotnet run
```

The API will be available at:
- HTTPS: https://localhost:7xxx
- HTTP: http://localhost:5xxx
- Swagger UI: https://localhost:7xxx/swagger

## Adding a Database (Optional)

To add Entity Framework Core with SQL Server:

```bash
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.EntityFrameworkCore.Tools
```

Update `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=ShopDB;Trusted_Connection=true;TrustServerCertificate=true;"
  }
}
```

Uncomment the DbContext registration in `Program.cs` and the context in `Data/ApplicationDbContext.cs`.

### Create Migrations

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Best Practices

1. **Separation of Concerns**: Keep controllers thin, move logic to services
2. **Dependency Injection**: Use interfaces and inject dependencies
3. **DTOs**: Always use DTOs for API contracts, never expose domain models directly
4. **Async/Await**: Use async methods for I/O operations
5. **Error Handling**: Use middleware for global exception handling
6. **Logging**: Inject ILogger and log important operations
7. **Validation**: Use Data Annotations or FluentValidation

## Next Steps

1. Implement your business logic in Services
2. Add database configuration and Entity Framework
3. Create additional controllers for other resources
4. Add authentication and authorization
5. Implement input validation
6. Add unit tests
7. Configure environment-specific settings

## Resources

- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [Entity Framework Core](https://docs.microsoft.com/ef/core)
- [RESTful API Design](https://restfulapi.net/)
