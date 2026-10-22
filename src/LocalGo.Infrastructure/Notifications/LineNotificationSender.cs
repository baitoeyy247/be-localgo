using System.Net.Http.Json;
using System.Text.Json;
using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Infrastructure.Line;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalGo.Infrastructure.Notifications;

public sealed class LineNotificationSender(
    ILocalGoDbContext db,
    IHttpClientFactory httpClientFactory,
    IOptions<LineSettings> lineOptions,
    ILogger<LineNotificationSender> logger) : INotificationSender
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<NotificationSendResult> SendAsync(
        NotificationSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = lineOptions.Value;

        if (!settings.MessagingReady)
        {
            logger.LogWarning(
                "Notification skipped (LINE messaging not configured). Event={EventType}. " +
                "Set Line:MessagingAccessToken or LINE_MESSAGING_ACCESS_TOKEN.",
                request.EventType);
            return new NotificationSendResult(
                NotificationSendOutcome.Skipped,
                "LINE Messaging Access Token not configured");
        }

        if (string.IsNullOrWhiteSpace(request.LineUserId))
        {
            logger.LogWarning(
                "Notification skipped (recipient has no LineUserId). Event={EventType} UserId={UserId}",
                request.EventType,
                request.RecipientUserId);
            return new NotificationSendResult(
                NotificationSendOutcome.Skipped,
                "No LINE user id for recipient");
        }

        if (request.LineUserId.StartsWith("dev-", StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Notification skipped (dev login LineUserId). Event={EventType} LineUserId={LineUserId}",
                request.EventType,
                request.LineUserId);
            return new NotificationSendResult(
                NotificationSendOutcome.Skipped,
                "Dev login account cannot receive LINE push; sign in via LINE LIFF and add the OA as friend");
        }

        try
        {
            var (altText, message) = await NotificationMessageBuilder.BuildAsync(
                request.EventType,
                request.Payload,
                settings.LiffBaseUrl,
                db,
                request.RecipientUserId,
                cancellationToken);

            _ = altText;

            var body = new
            {
                to = request.LineUserId,
                messages = new[] { message },
            };

            var client = httpClientFactory.CreateClient("line");
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/push");
            httpRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.MessagingAccessToken);
            httpRequest.Content = JsonContent.Create(body, options: JsonOpts);

            var response = await client.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "LINE push sent: {EventType} → {LineUserId}",
                    request.EventType,
                    request.LineUserId);
                return new NotificationSendResult(NotificationSendOutcome.Sent);
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "LINE push failed: {EventType} status={Status} error={Error}",
                request.EventType,
                response.StatusCode,
                error);
            return new NotificationSendResult(
                NotificationSendOutcome.Failed,
                $"HTTP {(int)response.StatusCode}: {error}",
                IncrementRetry: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LINE push exception: {EventType}", request.EventType);
            return new NotificationSendResult(
                NotificationSendOutcome.Failed,
                ex.Message,
                IncrementRetry: true);
        }
    }
}
