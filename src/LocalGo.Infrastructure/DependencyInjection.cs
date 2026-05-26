using System.Text;
using LocalGo.Application.Abstractions.Auth;
using LocalGo.Application.Abstractions.Line;
using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Infrastructure.Auth;
using LocalGo.Infrastructure.Jobs;
using LocalGo.Infrastructure.Line;
using LocalGo.Infrastructure.Notifications;
using LocalGo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace LocalGo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<LineSettings>(configuration.GetSection(LineSettings.SectionName));

        services.AddDbContext<LocalGoDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.UseNetTopologySuite().MigrationsAssembly(typeof(LocalGoDbContext).Assembly.FullName)));

        services.AddScoped<ILocalGoDbContext>(sp => sp.GetRequiredService<LocalGoDbContext>());
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ILineIdTokenVerifier, LineIdTokenVerifier>();
        services.AddScoped<INotificationPublisher, NotificationPublisher>();
        services.AddHttpClient("line");

        services.AddHostedService<RequestExpirationHostedService>();
        services.AddHostedService<NotificationRetryHostedService>();

        var jwt = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("Jwt settings are required.");

        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("Admin", policy => policy.RequireRole(nameof(Domain.Enums.SystemRole.Admin)));

        return services;
    }
}
