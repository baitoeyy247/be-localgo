using LocalGo.Application.Abstractions.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalGo.Infrastructure.Jobs;

/// <summary>
/// Background service that retries Failed and stale Pending notification logs every 30s with backoff.
/// Runs every 5 minutes with exponential back-off built into retry count.
/// </summary>
public sealed class NotificationRetryHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationRetryHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay so startup doesn't overlap with first-request notifications
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
                await publisher.RetryPendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification retry job failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
