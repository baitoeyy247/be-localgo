namespace LocalGo.Application.Abstractions.Notifications;

public interface INotificationPublisher
{
    Task EnqueueAsync(string eventType, Guid? userId, string? lineUserId, object payload, CancellationToken cancellationToken = default);

    /// <summary>Retry all Failed notification logs that have not yet reached the max retry limit.</summary>
    Task RetryPendingAsync(CancellationToken cancellationToken = default);
}
