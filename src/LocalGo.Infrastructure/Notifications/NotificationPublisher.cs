using System.Text.Json;
using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LocalGo.Infrastructure.Notifications;

public sealed class NotificationPublisher(
    ILocalGoDbContext db,
    INotificationSender sender,
    ILogger<NotificationPublisher> logger) : INotificationPublisher
{
    private const int MaxRetries = 3;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task EnqueueAsync(
        string eventType,
        Guid? userId,
        string? lineUserId,
        object payload,
        CancellationToken cancellationToken = default)
    {
        var log = new NotificationLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LineUserId = lineUserId,
            EventType = eventType,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOpts),
            Status = NotificationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };

        db.NotificationLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);

        await TrySendAsync(log, cancellationToken);
    }

    public async Task RetryPendingAsync(CancellationToken cancellationToken = default)
    {
        var failed = await db.NotificationLogs
            .Where(n => n.Status == NotificationStatus.Failed && n.RetryCount < MaxRetries)
            .ToListAsync(cancellationToken);

        foreach (var log in failed)
        {
            await TrySendAsync(log, cancellationToken);
        }
    }

    private async Task TrySendAsync(NotificationLog log, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(log.PayloadJson);
        var result = await sender.SendAsync(
            new NotificationSendRequest(log.EventType, log.UserId, log.LineUserId, payload),
            cancellationToken);

        switch (result.Outcome)
        {
            case NotificationSendOutcome.Sent:
                log.Status = NotificationStatus.Sent;
                log.SentAt = DateTime.UtcNow;
                log.ErrorMessage = null;
                break;
            case NotificationSendOutcome.Skipped:
                log.Status = NotificationStatus.Skipped;
                log.ErrorMessage = result.ErrorMessage;
                logger.LogDebug(
                    "Notification skipped. Event={EventType} LogId={Id} Reason={Reason}",
                    log.EventType,
                    log.Id,
                    result.ErrorMessage);
                break;
            case NotificationSendOutcome.Failed:
                log.Status = NotificationStatus.Failed;
                if (result.IncrementRetry)
                {
                    log.RetryCount += 1;
                }

                log.ErrorMessage = result.ErrorMessage;
                break;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static JsonElement? ParsePayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json).RootElement; }
        catch { return null; }
    }
}
