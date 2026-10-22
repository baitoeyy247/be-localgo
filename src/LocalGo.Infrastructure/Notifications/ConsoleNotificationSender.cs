using System.Text.Json;
using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Infrastructure.Line;
using Microsoft.Extensions.Options;

namespace LocalGo.Infrastructure.Notifications;

public sealed class ConsoleNotificationSender(
    ILocalGoDbContext db,
    IOptions<LineSettings> lineOptions,
    INotifyStdout stdout) : INotificationSender
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<NotificationSendResult> SendAsync(
        NotificationSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = lineOptions.Value;
        var (altText, message) = await NotificationMessageBuilder.BuildAsync(
            request.EventType,
            request.Payload,
            settings.LiffBaseUrl,
            db,
            request.RecipientUserId,
            cancellationToken);

        var textMessage = ExtractTextMessage(message, altText);
        var recipientRole = await NotificationRecipientRoleResolver.ResolveAsync(
            request.RecipientUserId,
            request.Payload,
            db,
            cancellationToken);

        var record = new ConsoleNotifyRecord(
            request.EventType,
            request.RecipientUserId,
            recipientRole,
            ParseGuidOrNull(request.Payload, "requestId"),
            ParseGuidOrNull(request.Payload, "appointmentId"),
            textMessage,
            DateTime.UtcNow);

        var json = JsonSerializer.Serialize(record, JsonOpts);
        stdout.WriteLine($"[NOTIFY] {json}");

        return new NotificationSendResult(NotificationSendOutcome.Sent);
    }

    private static string ExtractTextMessage(object message, string altText)
    {
        if (message is JsonElement el && el.TryGetProperty("text", out var textProp))
        {
            var text = textProp.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        try
        {
            var serialized = JsonSerializer.Serialize(message);
            using var doc = JsonDocument.Parse(serialized);
            if (doc.RootElement.TryGetProperty("text", out var t))
            {
                var s = t.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }
            }

            // L-N3: flex bubbles carry title + body in contents.body.contents; log the full text, not only altText.
            if (doc.RootElement.TryGetProperty("contents", out var bubble)
                && bubble.TryGetProperty("body", out var bodyBox)
                && bodyBox.TryGetProperty("contents", out var items)
                && items.ValueKind == JsonValueKind.Array)
            {
                var parts = items.EnumerateArray()
                    .Skip(1)
                    .Select(i => i.TryGetProperty("text", out var tx) ? tx.GetString() : null)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                if (parts.Count > 0)
                {
                    return string.Join('\n', parts);
                }
            }
        }
        catch
        {
            // fall through to altText
        }

        return altText;
    }

    private static Guid? ParseGuidOrNull(JsonElement? payload, string key)
    {
        var s = payload?.TryGetProperty(key, out var v) == true ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : v.GetRawText()) : null;
        return Guid.TryParse(s, out var id) ? id : null;
    }

    private sealed record ConsoleNotifyRecord(
        string Event,
        Guid? RecipientId,
        string? RecipientRole,
        Guid? RequestId,
        Guid? AppointmentId,
        string Message,
        DateTime Timestamp);
}
