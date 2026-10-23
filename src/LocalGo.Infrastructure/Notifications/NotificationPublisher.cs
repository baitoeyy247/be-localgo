using System.Text.Json;
using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalGo.Infrastructure.Notifications;

public sealed class NotificationPublisher(
    ILocalGoDbContext db,
    INotificationSender sender,
    ILogger<NotificationPublisher> logger,
    LocalGo.Application.Common.IBackgroundWork? background = null,
    IServiceScopeFactory? scopes = null) : INotificationPublisher
{
    internal const int MaxAttempts = 5;
    /// <summary>Pending rows older than this are treated as stuck (process died / exception mid-send).</summary>
    internal static readonly TimeSpan StalePendingAfter = TimeSpan.FromSeconds(30);
    /// <summary>R46 D6: a Sending claim older than this is considered abandoned (crash mid-send).</summary>
    internal static readonly TimeSpan StaleSendingAfter = TimeSpan.FromSeconds(60);
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

        var t0 = LocalGo.Application.Common.ServerTiming.Start();
        var save0 = LocalGo.Application.Common.ServerTiming.Current?.SaveMs ?? 0;

        // R46 D6: write the outbox row through its own scope/DbContext so the caller's pending
        // (unsaved) changes are never flushed as a side effect of notifying.
        using var scope = scopes?.CreateScope();
        var outboxDb = scope?.ServiceProvider.GetRequiredService<ILocalGoDbContext>() ?? db;
        outboxDb.NotificationLogs.Add(log);
        // N-1: the outbox row must be written even if the caller's request was aborted.
        await outboxDb.SaveChangesAsync(CancellationToken.None);

        if (background is null)
        {
            // Dispatch must not depend on the caller's request lifetime (client abort would leave Pending).
            await DispatchCoreAsync(outboxDb, log.Id, CancellationToken.None);
        }
        else
        {
            // P-1: async outbox — the request only pays for the outbox insert; send runs after the response.
            var id = log.Id;
            background.Enqueue((sp, ct) => sp.GetRequiredService<INotificationPublisher>() is NotificationPublisher p
                ? p.DispatchAsync(id, ct)
                : Task.CompletedTask);
        }

        var saved = (LocalGo.Application.Common.ServerTiming.Current?.SaveMs ?? 0) - save0;
        var elapsed = LocalGo.Application.Common.ServerTiming.Since(t0) - TimeSpan.FromMilliseconds(saved);
        LocalGo.Application.Common.ServerTiming.AddNotify(elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero);
    }

    /// <summary>P-1 / R46 D6: send one outbox row only if this caller wins the atomic Pending→Sending claim.</summary>
    public Task DispatchAsync(Guid logId, CancellationToken cancellationToken = default)
        => DispatchCoreAsync(db, logId, cancellationToken);

    private async Task DispatchCoreAsync(ILocalGoDbContext d, Guid logId, CancellationToken cancellationToken)
    {
        var log = await d.NotificationLogs.FirstOrDefaultAsync(n => n.Id == logId, cancellationToken);
        if (log is null || log.Status != NotificationStatus.Pending) return;
        if (!await TryClaimAsync(d, log, cancellationToken)) return;
        await TrySendAsync(d, log, CancellationToken.None);
    }

    public async Task RetryPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var staleBefore = now - StalePendingAfter;
        var staleSendingBefore = now - StaleSendingAfter;
        var candidates = await db.NotificationLogs
            .Where(n => n.RetryCount < MaxAttempts
                && (n.Status == NotificationStatus.Failed
                    || (n.Status == NotificationStatus.Pending && n.CreatedAt < staleBefore)
                    || (n.Status == NotificationStatus.Sending && n.ClaimedAt < staleSendingBefore)))
            .OrderBy(n => n.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var log in candidates)
        {
            if (log.Status == NotificationStatus.Failed && now < NextAttemptAt(log)) continue;
            if (!await TryClaimAsync(db, log, cancellationToken)) continue;
            await TrySendAsync(db, log, cancellationToken);
        }
    }

    /// <summary>
    /// R46 D6: atomic claim. On a relational DB this is
    /// UPDATE notification_logs SET status=Sending, claimed_at=now WHERE id=@id AND status=@expected [AND claimed_at stale]
    /// and only the caller that affected exactly one row sends. Non-relational providers (unit tests) fall back
    /// to a tracked compare-and-set.
    /// </summary>
    internal async Task<bool> TryClaimAsync(ILocalGoDbContext d, NotificationLog log, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expected = log.Status;
        var staleSendingBefore = now - StaleSendingAfter;
        if (expected == NotificationStatus.Sending && log.ClaimedAt >= staleSendingBefore) return false;

        if (d is DbContext ctx && ctx.Database.IsRelational())
        {
            var id = log.Id;
            var rows = await d.NotificationLogs
                .Where(n => n.Id == id
                    && n.Status == expected
                    && (expected != NotificationStatus.Sending || n.ClaimedAt < staleSendingBefore))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(n => n.Status, NotificationStatus.Sending)
                          .SetProperty(n => n.ClaimedAt, (DateTime?)now),
                    CancellationToken.None);
            if (rows != 1) return false;
            log.Status = NotificationStatus.Sending;
            log.ClaimedAt = now;
            var entry = ctx.Entry(log);
            entry.Property(n => n.Status).OriginalValue = NotificationStatus.Sending;
            entry.Property(n => n.ClaimedAt).OriginalValue = now;
            return true;
        }

        if (log.Status != expected) return false;
        log.Status = NotificationStatus.Sending;
        log.ClaimedAt = now;
        await d.SaveChangesAsync(CancellationToken.None);
        return true;
    }

    /// <summary>Exponential backoff from CreatedAt: 15s, 30s, 60s, 120s ... (cumulative).</summary>
    public static DateTime NextAttemptAt(NotificationLog log)
    {
        var seconds = 15 * ((1 << Math.Clamp(log.RetryCount, 0, 10)) - 1);
        return log.CreatedAt.AddSeconds(seconds);
    }

    private async Task TrySendAsync(ILocalGoDbContext d, NotificationLog log, CancellationToken cancellationToken)
    {
        NotificationSendResult result;
        try
        {
            // R46 D1/D2: recipient gate for every sender mode (console and LINE).
            var blocked = await LocalGo.Application.Common.NotificationRecipientFilter.BlockReasonAsync(
                d, log.UserId, log.EventType, CancellationToken.None);
            var payload = ParsePayload(log.PayloadJson);
            result = blocked is not null
                ? new NotificationSendResult(NotificationSendOutcome.Skipped, blocked)
                : await sender.SendAsync(
                new NotificationSendRequest(log.EventType, log.UserId, log.LineUserId, payload, log.Id),
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
                // R45 NR-4: permanent LINE rejections (4xx except 429) are not retried.
                if (result.Permanent) log.RetryCount = Math.Max(log.RetryCount, MaxAttempts);
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

        await d.SaveChangesAsync(CancellationToken.None);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static JsonElement? ParsePayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json).RootElement; }
        catch { return null; }
    }
}
