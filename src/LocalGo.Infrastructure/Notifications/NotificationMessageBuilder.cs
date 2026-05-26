using System.Text.Json;
using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Infrastructure.Notifications;

/// <summary>Builds concise Thai LINE notification text with service/request context.</summary>
public static class NotificationMessageBuilder
{
    public static async Task<(string altText, object message)> BuildAsync(
        string eventType,
        JsonElement? payload,
        string liffBaseUrl,
        ILocalGoDbContext db,
        Guid? recipientUserId,
        CancellationToken cancellationToken)
    {
        var ctx = await ResolveServiceContextAsync(payload, db, cancellationToken);
        var serviceBlock = FormatServiceContext(ctx);

        var (title, body, liffPath) = eventType switch
        {
            NotificationEvents.NewMatchingRequest => (
                "มีงานใหม่ในพื้นที่ของคุณ",
                serviceBlock,
                string.IsNullOrWhiteSpace(GetStr(payload, "requestId"))
                    ? "/provider/requests"
                    : $"/provider/requests/{GetStr(payload, "requestId")}"),

            NotificationEvents.NewBidReceived => (
                "มีผู้เสนอราคาใหม่",
                JoinLines(
                    serviceBlock,
                    $"{GetStr(payload, "providerName")} · {GetStr(payload, "priceText")}"),
                $"/requester/bids?requestId={GetStr(payload, "requestId")}"),

            NotificationEvents.BidSelected => (
                "คุณถูกเลือกเป็นผู้ให้บริการแล้ว!",
                serviceBlock,
                string.IsNullOrWhiteSpace(GetStr(payload, "requestId"))
                    ? "/provider/requests"
                    : $"/provider/requests/{GetStr(payload, "requestId")}"),

            NotificationEvents.AppointmentCreated => (
                "สร้างนัดหมายแล้ว",
                JoinLines(serviceBlock, $"สถานะ: {GetStr(payload, "status")}"),
                null),

            NotificationEvents.AppointmentUpdated => (
                "อัปเดตสถานะนัดหมาย",
                JoinLines(serviceBlock, $"สถานะ: {GetStr(payload, "status")}"),
                null),

            NotificationEvents.RequestExpiringSoon => (
                "ประกาศใกล้หมดอายุ",
                JoinLines(serviceBlock, $"เหลือ {GetStr(payload, "daysRemaining")} วัน"),
                $"/requester/bids?requestId={GetStr(payload, "requestId")}"),

            NotificationEvents.RequestExpired => (
                "ประกาศหมดอายุแล้ว",
                serviceBlock,
                $"/requester/bids?requestId={GetStr(payload, "requestId")}"),

            NotificationEvents.ReviewReceived => (
                "คุณได้รับรีวิวใหม่",
                string.IsNullOrWhiteSpace(serviceBlock)
                    ? $"{GetStr(payload, "reviewType")} · {GetStr(payload, "rating")} ดาว"
                    : JoinLines(serviceBlock, $"{GetStr(payload, "reviewType")} · {GetStr(payload, "rating")} ดาว"),
                "/provider/profile"),

            _ => ("LocalGo แจ้งเตือน", eventType, null),
        };

        if (liffPath is null
            && eventType is NotificationEvents.AppointmentCreated or NotificationEvents.AppointmentUpdated)
        {
            liffPath = await ResolveAppointmentLiffPathAsync(
                payload,
                recipientUserId,
                db,
                cancellationToken);
        }

        var liffUrl = !string.IsNullOrWhiteSpace(liffBaseUrl) && !string.IsNullOrWhiteSpace(liffPath)
            ? $"{liffBaseUrl}{liffPath}"
            : null;

        if (liffUrl is null)
        {
            return (title, new { type = "text", text = JoinLines(title, body) });
        }

        return (title, BuildFlexBubble(title, body, liffUrl));
    }

    public static string FormatServiceContext(ServiceContext? ctx)
    {
        if (ctx is null)
        {
            return "";
        }

        var category = ctx.CategoryName?.Trim();
        var requestTitle = ctx.Title?.Trim();

        if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(requestTitle))
        {
            return $"บริการ: {category}\n{requestTitle}";
        }

        if (!string.IsNullOrEmpty(category))
        {
            return $"บริการ: {category}";
        }

        return requestTitle ?? "";
    }

    private static async Task<ServiceContext?> ResolveServiceContextAsync(
        JsonElement? payload,
        ILocalGoDbContext db,
        CancellationToken cancellationToken)
    {
        var title = GetStr(payload, "title");
        var categoryName = GetStr(payload, "categoryName");

        if (Guid.TryParse(GetStr(payload, "requestId"), out var requestId))
        {
            var fromRequest = await db.ServiceRequests.AsNoTracking()
                .Where(r => r.Id == requestId)
                .Select(r => new ServiceContext(r.Title, r.Category.Name))
                .FirstOrDefaultAsync(cancellationToken);

            if (fromRequest is not null)
            {
                return new ServiceContext(
                    string.IsNullOrWhiteSpace(title) ? fromRequest.Title : title,
                    string.IsNullOrWhiteSpace(categoryName) ? fromRequest.CategoryName : categoryName);
            }
        }

        if (Guid.TryParse(GetStr(payload, "serviceRequestId"), out var serviceRequestId))
        {
            var fromReview = await db.ServiceRequests.AsNoTracking()
                .Where(r => r.Id == serviceRequestId)
                .Select(r => new ServiceContext(r.Title, r.Category.Name))
                .FirstOrDefaultAsync(cancellationToken);

            if (fromReview is not null)
            {
                return new ServiceContext(
                    string.IsNullOrWhiteSpace(title) ? fromReview.Title : title,
                    string.IsNullOrWhiteSpace(categoryName) ? fromReview.CategoryName : categoryName);
            }
        }

        if (string.IsNullOrWhiteSpace(categoryName) && Guid.TryParse(GetStr(payload, "categoryId"), out var categoryId))
        {
            categoryName = await db.ServiceCategories.AsNoTracking()
                .Where(c => c.Id == categoryId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "";
        }

        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(categoryName))
        {
            return null;
        }

        return new ServiceContext(
            string.IsNullOrWhiteSpace(title) ? null : title,
            string.IsNullOrWhiteSpace(categoryName) ? null : categoryName);
    }

    private static async Task<string> ResolveAppointmentLiffPathAsync(
        JsonElement? payload,
        Guid? recipientUserId,
        ILocalGoDbContext db,
        CancellationToken cancellationToken)
    {
        var requestIdStr = GetStr(payload, "requestId");
        if (string.IsNullOrWhiteSpace(requestIdStr) || !Guid.TryParse(requestIdStr, out var requestId))
        {
            return "/requester/appointment";
        }

        if (recipientUserId is null)
        {
            return $"/requester/appointment?requestId={requestIdStr}";
        }

        var requesterUserId = await db.ServiceRequests.AsNoTracking()
            .Where(r => r.Id == requestId)
            .Select(r => r.RequesterUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (requesterUserId == recipientUserId)
        {
            return $"/requester/appointment?requestId={requestIdStr}";
        }

        return $"/provider/requests/{requestIdStr}";
    }

    private static string GetStr(JsonElement? el, string key) =>
        el?.TryGetProperty(key, out var v) == true ? v.GetString() ?? "" : "";

    private static string JoinLines(params string?[] lines) =>
        string.Join('\n', lines.Where(l => !string.IsNullOrWhiteSpace(l)));

    private static object BuildFlexBubble(string title, string body, string liffUrl) => new
    {
        type = "flex",
        altText = title,
        contents = new
        {
            type = "bubble",
            size = "kilo",
            body = new
            {
                type = "box",
                layout = "vertical",
                spacing = "sm",
                paddingAll = "16px",
                contents = new object[]
                {
                    new
                    {
                        type = "text",
                        text = "LocalGo",
                        size = "xs",
                        color = "#00B900",
                        weight = "bold",
                    },
                    new
                    {
                        type = "text",
                        text = title,
                        weight = "bold",
                        size = "md",
                        color = "#1C1C1C",
                        wrap = true,
                    },
                    new
                    {
                        type = "text",
                        text = body,
                        size = "sm",
                        color = "#555555",
                        wrap = true,
                        margin = "sm",
                    },
                },
            },
            footer = new
            {
                type = "box",
                layout = "vertical",
                spacing = "none",
                contents = new[]
                {
                    new
                    {
                        type = "button",
                        style = "primary",
                        color = "#00B900",
                        height = "sm",
                        action = new { type = "uri", label = "ดูรายละเอียด", uri = liffUrl },
                    },
                },
            },
        },
    };

    public sealed record ServiceContext(string? Title, string? CategoryName);
}
