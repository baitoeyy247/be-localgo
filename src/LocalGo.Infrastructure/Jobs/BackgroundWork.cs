using LocalGo.Application.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalGo.Infrastructure.Jobs;

public sealed class BackgroundWork(IServiceScopeFactory scopes, ILogger<BackgroundWork> logger) : IBackgroundWork
{
    public void Enqueue(Func<IServiceProvider, CancellationToken, Task> work)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await work(scope.ServiceProvider, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background work failed");
            }
        });
    }
}
