using LocalGo.Application.Auth;
using LocalGo.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LocalGo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<DevAuthService>();
        services.AddScoped<AuthAppService>();
        services.AddScoped<ProviderAppService>();
        services.AddScoped<CategoryAppService>();
        services.AddScoped<SearchAppService>();
        services.AddScoped<ServiceRequestAppService>();
        services.AddScoped<BidAppService>();
        services.AddScoped<AppointmentAppService>();
        services.AddScoped<ReviewAppService>();
        services.AddScoped<AdminAppService>();
        services.AddScoped<ReportAppService>();
        return services;
    }
}
