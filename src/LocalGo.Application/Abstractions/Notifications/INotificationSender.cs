using System.Text.Json;

namespace LocalGo.Application.Abstractions.Notifications;

public enum NotificationSendOutcome
{
    Sent,
    Skipped,
    Failed,
}

public sealed record NotificationSendRequest(
    string EventType,
    Guid? RecipientUserId,
    string? LineUserId,
    JsonElement? Payload);

public sealed record NotificationSendResult(
    NotificationSendOutcome Outcome,
    string? ErrorMessage = null,
    bool IncrementRetry = false);

public interface INotificationSender
{
    Task<NotificationSendResult> SendAsync(
        NotificationSendRequest request,
        CancellationToken cancellationToken = default);
}
