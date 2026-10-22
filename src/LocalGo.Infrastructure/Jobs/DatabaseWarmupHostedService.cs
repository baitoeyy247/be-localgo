using LocalGo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalGo.Infrastructure.Jobs;

/// <summary>Opens the first DB connection during startup so cold-start latency is not paid on the first API call.</summary>
public sealed class DatabaseWarmupHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseWarmupHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LocalGoDbContext>();
            _ = await db.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database warm-up ping failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
