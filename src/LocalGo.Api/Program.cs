using System.Threading.RateLimiting;
using LocalGo.Api.Mapping;
using LocalGo.Api.Middleware;
using LocalGo.Application;
using LocalGo.Infrastructure;
using LocalGo.Infrastructure.Persistence;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);
}
else if (builder.Environment.IsStaging())
{
    builder.Configuration.AddJsonFile("appsettings.Staging.local.json", optional: true, reloadOnChange: true);
}

ApplyEnvOverride(builder.Configuration, "DATABASE_URL", "ConnectionStrings:Default");
ApplyEnvOverride(builder.Configuration, "REDIS_URL", "ConnectionStrings:Redis");
ApplyEnvOverride(builder.Configuration, "JWT_SIGNING_KEY", "Jwt:SigningKey");
ApplyEnvOverride(builder.Configuration, "JWT_ISSUER", "Jwt:Issuer");
ApplyEnvOverride(builder.Configuration, "JWT_AUDIENCE", "Jwt:Audience");
ApplyEnvOverride(builder.Configuration, "CORS_ALLOWED_ORIGINS", "Cors:AllowedOrigins");
ApplyEnvOverride(builder.Configuration, "LINE_CHANNEL_ID", "Line:ChannelId");
ApplyEnvOverride(builder.Configuration, "LINE_CHANNEL_SECRET", "Line:ChannelSecret");
ApplyEnvOverride(builder.Configuration, "LINE_MESSAGING_ACCESS_TOKEN", "Line:MessagingAccessToken");
ApplyEnvOverride(builder.Configuration, "LINE_LIFF_ID", "Line:LiffId");
ApplyEnvOverride(builder.Configuration, "NGROK_PUBLIC_URL", "Cors:NgrokPublicUrl");

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://+:{port}");
}

builder.Services.AddControllers();
builder.Services.AddApiMapping();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = 200;
        limiter.QueueLimit = 0;
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("Default");
var redisConnection = builder.Configuration.GetConnectionString("Redis");

var healthChecks = builder.Services.AddHealthChecks();
if (!string.IsNullOrWhiteSpace(connectionString))
{
    healthChecks.AddNpgSql(connectionString, name: "postgresql");
}

if (!string.IsNullOrWhiteSpace(redisConnection))
{
    healthChecks.AddRedis(redisConnection, name: "redis");
}

var corsOrigins = builder.Configuration["Cors:AllowedOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToList() ?? ["http://localhost:4200"];

var ngrokOrigin = builder.Configuration["Cors:NgrokPublicUrl"]?.TrimEnd('/');
if (!string.IsNullOrWhiteSpace(ngrokOrigin) && !corsOrigins.Contains(ngrokOrigin, StringComparer.OrdinalIgnoreCase))
{
    corsOrigins.Add(ngrokOrigin);
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalGoCors", policy =>
    {
        policy.WithOrigins(corsOrigins.ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

var isDevOrSit = app.Environment.IsDevelopment() || app.Environment.IsStaging();

if (isDevOrSit)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "LocalGo API v1");
    });
}

if (isDevOrSit)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LocalGoDbContext>();
    await db.Database.MigrateAsync();
    await DataSeeder.SeedAsync(db);
}

app.UseCors("LocalGoCors");
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("api");

app.Run();

static void ApplyEnvOverride(IConfiguration config, string envName, string configKey)
{
    var value = Environment.GetEnvironmentVariable(envName);
    if (!string.IsNullOrWhiteSpace(value))
    {
        config[configKey] = value;
    }
}
