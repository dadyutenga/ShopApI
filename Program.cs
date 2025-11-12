using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ShopApI.Data;
using ShopApI.Middleware;
using ShopApI.Services;
using ShopApI.Consumers;
using Serilog;
using StackExchange.Redis;
using MassTransit;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using DotNetEnv;
using FluentValidation;
using FluentValidation.AspNetCore;
using ShopApI.Validators;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ShopAPI"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("MassTransit")
            .AddConsoleExporter();
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ShopAPI", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "ShopDB";
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "root";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";
var connectionString = $"Server={dbHost};Port={dbPort};Database={dbName};User={dbUser};Password={dbPassword};";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "ShopAPI";
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "ShopAPIClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        ClockSkew = TimeSpan.Zero
    };
    
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var jwtService = context.HttpContext.RequestServices.GetRequiredService<IJwtService>();
            var jti = context.Principal?.FindFirst("jti")?.Value;
            
            if (jti != null && await jwtService.IsTokenBlacklistedAsync(jti))
            {
                context.Fail("Token has been revoked");
            }
        }
    };
})
.AddGoogle(options =>
{
    options.ClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? "";
    options.ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? "";
})
.AddGitHub(options =>
{
    options.ClientId = Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID") ?? "";
    options.ClientSecret = Environment.GetEnvironmentVariable("GITHUB_CLIENT_SECRET") ?? "";
})
.AddMicrosoftAccount(options =>
{
    options.ClientId = Environment.GetEnvironmentVariable("MICROSOFT_CLIENT_ID") ?? "";
    options.ClientSecret = Environment.GetEnvironmentVariable("MICROSOFT_CLIENT_SECRET") ?? "";
});

builder.Services.AddAuthorization();

var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse($"{redisHost}:{redisPort}");
    return ConnectionMultiplexer.Connect(configuration);
});
builder.Services.AddSingleton<ICacheService, RedisCacheService>();

var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
var rabbitVHost = Environment.GetEnvironmentVariable("RABBITMQ_VHOST") ?? "/";
var rabbitUser = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
var rabbitPassword = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest";

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<UserRegisteredConsumer>();
    x.AddConsumer<UserRoleAssignedConsumer>();
    x.AddConsumer<UserStatusChangedConsumer>();
    x.AddConsumer<OtpGeneratedConsumer>();
    x.AddConsumer<OtpVerifiedConsumer>();
    x.AddConsumer<EmailVerificationEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, rabbitVHost, h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPassword);
        });

        cfg.Message<ShopApI.Events.UserRegisteredEvent>(e => e.SetEntityName("user.events"));
        cfg.Message<ShopApI.Events.UserRoleAssignedEvent>(e => e.SetEntityName("user.events"));
        cfg.Message<ShopApI.Events.UserStatusChangedEvent>(e => e.SetEntityName("user.events"));
        cfg.Message<ShopApI.Events.UserDeletedEvent>(e => e.SetEntityName("user.events"));
        cfg.Message<ShopApI.Events.EmailVerificationEvent>(e => e.SetEntityName("user.events"));
        cfg.Message<ShopApI.Events.OtpGeneratedEvent>(e => e.SetEntityName("otp.events"));
        cfg.Message<ShopApI.Events.OtpVerifiedEvent>(e => e.SetEntityName("otp.events"));

        cfg.Publish<ShopApI.Events.UserRegisteredEvent>(p => p.Durable = true);
        cfg.Publish<ShopApI.Events.OtpGeneratedEvent>(p => p.Durable = true);
        cfg.Publish<ShopApI.Events.OtpVerifiedEvent>(p => p.Durable = true);

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOAuthService, OAuthService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IEventPublisher, EventPublisher>();
builder.Services.AddSingleton<IKeyRotationService, KeyRotationService>();
builder.Services.AddScoped<IBootstrapService, BootstrapService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();

var corsOrigins = Environment.GetEnvironmentVariable("CORS_ORIGINS")?.Split(',') ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("RestrictedCors", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .WithMethods("GET", "POST", "PUT", "DELETE")
              .WithHeaders("Content-Type", "Authorization")
              .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<BodySizeLimitMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<RateLimitMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseCors("RestrictedCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
