using LocalGo.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalGo.Infrastructure.Jobs;

public sealed class RequestExpirationHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<RequestExpirationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ServiceRequestAppService>();
                await service.ExpireStaleAsync(stoppingToken);
                await service.NotifyExpiringSoonAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Request expiration job failed");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
