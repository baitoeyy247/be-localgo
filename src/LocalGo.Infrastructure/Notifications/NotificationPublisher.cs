using System.Net.Http.Json;
using LocalGo.Application.Abstractions.Notifications;
using System.Text.Json;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using LocalGo.Infrastructure.Line;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalGo.Infrastructure.Notifications;

public sealed class NotificationPublisher(
    ILocalGoDbContext db,
    IHttpClientFactory httpClientFactory,
    IOptions<LineSettings> lineOptions,
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
        var settings = lineOptions.Value;

        if (!settings.MessagingReady)
        {
            log.Status = NotificationStatus.Skipped;
            log.ErrorMessage = "LINE Messaging Access Token not configured";
            logger.LogWarning(
                "Notification skipped (LINE messaging not configured). Event={EventType} LogId={Id}. " +
                "Set Line:MessagingAccessToken in appsettings.Development.local.json or LINE_MESSAGING_ACCESS_TOKEN.",
                log.EventType,
                log.Id);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(log.LineUserId))
        {
            log.Status = NotificationStatus.Skipped;
            log.ErrorMessage = "No LINE user id for recipient";
            logger.LogWarning(
                "Notification skipped (recipient has no LineUserId). Event={EventType} UserId={UserId} LogId={Id}",
                log.EventType,
                log.UserId,
                log.Id);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (log.LineUserId.StartsWith("dev-", StringComparison.Ordinal))
        {
            log.Status = NotificationStatus.Skipped;
            log.ErrorMessage = "Dev login account cannot receive LINE push; sign in via LINE LIFF and add the OA as friend";
            logger.LogWarning(
                "Notification skipped (dev login LineUserId). Event={EventType} LineUserId={LineUserId} LogId={Id}",
                log.EventType,
                log.LineUserId,
                log.Id);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            var payload = ParsePayload(log.PayloadJson);
            var (altText, message) = await NotificationMessageBuilder.BuildAsync(
                log.EventType, payload, settings.LiffBaseUrl, db, log.UserId, cancellationToken);

            var body = new
            {
                to = log.LineUserId,
                messages = new[] { message },
            };

            var client = httpClientFactory.CreateClient("line");
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/push");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.MessagingAccessToken);
            request.Content = JsonContent.Create(body, options: JsonOpts);

            var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                log.Status = NotificationStatus.Sent;
                log.SentAt = DateTime.UtcNow;
                log.ErrorMessage = null;
                logger.LogInformation("LINE push sent: {EventType} → {LineUserId}", log.EventType, log.LineUserId);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                log.Status = NotificationStatus.Failed;
                log.RetryCount += 1;
                log.ErrorMessage = $"HTTP {(int)response.StatusCode}: {error}";
                logger.LogWarning("LINE push failed: {EventType} status={Status} error={Error}",
                    log.EventType, response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            log.Status = NotificationStatus.Failed;
            log.RetryCount += 1;
            log.ErrorMessage = ex.Message;
            logger.LogWarning(ex, "LINE push exception: {EventType}", log.EventType);
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
