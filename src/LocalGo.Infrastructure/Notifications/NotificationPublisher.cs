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
    internal const int MaxAttempts = 5;
    /// <summary>Pending rows older than this are treated as stuck (process died / exception mid-send).</summary>
    internal static readonly TimeSpan StalePendingAfter = TimeSpan.FromSeconds(30);
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

        // Dispatch must not depend on the caller's request lifetime (client abort would leave Pending).
        await TrySendAsync(log, CancellationToken.None);
    }

    public async Task RetryPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var staleBefore = now - StalePendingAfter;
        var candidates = await db.NotificationLogs
            .Where(n => n.RetryCount < MaxAttempts
                && (n.Status == NotificationStatus.Failed
                    || (n.Status == NotificationStatus.Pending && n.CreatedAt < staleBefore)))
            .OrderBy(n => n.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var log in candidates)
        {
            if (log.Status == NotificationStatus.Failed && now < NextAttemptAt(log)) continue;
            await TrySendAsync(log, cancellationToken);
        }
    }

    /// <summary>Exponential backoff from CreatedAt: 15s, 30s, 60s, 120s ... (cumulative).</summary>
    public static DateTime NextAttemptAt(NotificationLog log)
    {
        var seconds = 15 * ((1 << Math.Clamp(log.RetryCount, 0, 10)) - 1);
        return log.CreatedAt.AddSeconds(seconds);
    }

    private async Task TrySendAsync(NotificationLog log, CancellationToken cancellationToken)
    {
        NotificationSendResult result;
        try
        {
            var payload = ParsePayload(log.PayloadJson);
            result = await sender.SendAsync(
                new NotificationSendRequest(log.EventType, log.UserId, log.LineUserId, payload),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            result = new NotificationSendResult(
                NotificationSendOutcome.Failed,
                $"{ex.GetType().Name}: {ex.Message}",
                IncrementRetry: true);
        }

        switch (result.Outcome)
        {
            case NotificationSendOutcome.Sent:
                log.Status = NotificationStatus.Sent;
                log.SentAt = DateTime.UtcNow;
                log.ErrorMessage = null;
                log.RetryCount += 1;
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
                log.RetryCount += 1;
                log.ErrorMessage = Truncate(result.ErrorMessage ?? "send failed", 1000);
                Console.Out.WriteLine("[NOTIFY-ERR] " + JsonSerializer.Serialize(new
                {
                    @event = log.EventType,
                    logId = log.Id,
                    recipientId = log.UserId,
                    attemptCount = log.RetryCount,
                    willRetry = log.RetryCount < MaxAttempts,
                    error = log.ErrorMessage,
                    timestamp = DateTime.UtcNow,
                }, JsonOpts));
                break;
        }

        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static JsonElement? ParsePayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json).RootElement; }
        catch { return null; }
    }
}
