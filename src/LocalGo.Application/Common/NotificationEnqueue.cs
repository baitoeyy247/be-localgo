using LocalGo.Application.Abstractions.Notifications;
using Microsoft.Extensions.Logging;

namespace LocalGo.Application.Common;

internal static class NotificationEnqueue
{
    public static async Task TryAsync(
        ILogger logger,
        INotificationPublisher notifications,
        string eventType,
        Guid userId,
        string? lineUserId,
        object payload,
        CancellationToken cancellationToken,
        string failureMessage,
        params object?[] logArgs)
    {
        try
        {
            await notifications.EnqueueAsync(eventType, userId, lineUserId, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, failureMessage, logArgs);
        }
    }

    /// <summary>Enqueues only when recipient is not the actor (the other party).</summary>
    public static async Task TryExcludingActorAsync(
        ILogger logger,
        INotificationPublisher notifications,
        string eventType,
        Guid recipientUserId,
        Guid actorUserId,
        string? lineUserId,
        object payload,
        CancellationToken cancellationToken,
        string failureMessage,
        params object?[] logArgs)
    {
        if (!NotificationRecipients.ShouldNotify(recipientUserId, actorUserId))
        {
            return;
        }

        await TryAsync(
            logger,
            notifications,
            eventType,
            recipientUserId,
            lineUserId,
            payload,
            cancellationToken,
            failureMessage,
            logArgs);
    }
}
