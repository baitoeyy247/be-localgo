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
        var actorRole = GetStr(payload, "actorRole");
        var toProvider = eventType is NotificationEvents.AppointmentCreated or NotificationEvents.AppointmentUpdated
            && await IsProviderRecipientAsync(payload, recipientUserId, actorRole, db, cancellationToken);

        var (title, body, liffPath) = eventType switch
        {
            NotificationEvents.NewMatchingRequest => (
                "มีงานใหม่ในพื้นที่ของคุณ",
                serviceBlock,
                string.IsNullOrWhiteSpace(GetStr(payload, "requestId"))
                    ? "/provider/requests"
                    : $"/provider/bid?requestId={GetStr(payload, "requestId")}"),

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

            NotificationEvents.AppointmentCreated => toProvider
                ? (
                    "มีนัดหมายใหม่จากลูกค้า กรุณายืนยัน",
                    JoinLines(serviceBlock, FormatScheduledAt(GetStr(payload, "scheduledAt"))),
                    null)
                : (
                    "สร้างนัดหมายแล้ว",
                    JoinLines(serviceBlock, $"สถานะ: {FormatAppointmentStatus(GetStr(payload, "status"))}"),
                    null),

            NotificationEvents.AppointmentUpdated => (
                AppointmentUpdatedTitle(GetStr(payload, "status"), toProvider, actorRole),
                JoinLines(
                    serviceBlock,
                    AppointmentUpdatedDetail(GetStr(payload, "status"), GetStr(payload, "declineReason"), toProvider),
                    FormatScheduledAt(GetStr(payload, "scheduledAt"))),
                null),

            NotificationEvents.RequestExpiringSoon => (
                "ประกาศใกล้หมดอายุ",
                JoinLines(serviceBlock, $"เหลือ {GetStr(payload, "daysRemaining")} วัน"),
                $"/requester/bids?requestId={GetStr(payload, "requestId")}"),

            NotificationEvents.RequestExpired => (
                "ประกาศหมดอายุแล้ว",
                serviceBlock,
                $"/requester/bids?requestId={GetStr(payload, "requestId")}"),

            NotificationEvents.RequestCancelled => (
                "งานถูกยกเลิก",
                JoinLines(serviceBlock, "ผู้ประกาศยกเลิกงานนี้แล้ว"),
                "/provider/requests"),

            NotificationEvents.ReviewReceived => (
                "คุณได้รับรีวิวใหม่",
                string.IsNullOrWhiteSpace(serviceBlock)
                    ? $"{FormatReviewType(GetStr(payload, "reviewType"))} · {GetStr(payload, "rating")} ดาว"
                    : JoinLines(serviceBlock, $"{FormatReviewType(GetStr(payload, "reviewType"))} · {GetStr(payload, "rating")} ดาว"),
                "/provider/profile"),

            NotificationEvents.BidUpdated => (
                "ข้อเสนอมีการเปลี่ยนแปลง",
                JoinLines(serviceBlock, BidUpdatedDetail(payload, ctx?.Title)),
                $"/requester/bids?requestId={GetStr(payload, "requestId")}"),

            NotificationEvents.BidWithdrawn => (
                "ผู้ให้บริการถอนข้อเสนอ",
                JoinLines(serviceBlock, $"{ProviderOrDefault(payload)} ถอนข้อเสนอสำหรับ{TitleOrDefault(ctx?.Title)}แล้ว"),
                $"/requester/bids?requestId={GetStr(payload, "requestId")}"),

            NotificationEvents.AccountSuspended => (
                "บัญชีของคุณถูกระงับ",
                "บัญชี LocalGo ของคุณถูกระงับการใช้งานชั่วคราว หากมีข้อสงสัยโปรดติดต่อผู้ดูแลระบบ",
                null),

            NotificationEvents.AccountReactivated => (
                "บัญชีของคุณเปิดใช้งานแล้ว",
                "บัญชี LocalGo ของคุณกลับมาใช้งานได้ตามปกติแล้ว",
                null),

            _ => ("LocalGo แจ้งเตือน", "มีการอัปเดตใหม่ใน LocalGo", null),
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

        // R45 NR-5: LINE rejects empty text components (HTTP 400) — always send a non-empty body.
        if (string.IsNullOrWhiteSpace(body)) body = "แตะเพื่อดูรายละเอียดใน LocalGo";

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

    private static string FormatAppointmentStatus(string status) => status switch
    {
        "Pending" => "รอผู้ให้บริการยืนยันนัด",
        "Confirmed" => "ผู้ให้บริการยืนยันนัดแล้ว",
        "InProgress" => "กำลังดำเนินงาน",
        "Completed" => "เสร็จสิ้น",
        "Cancelled" => "ยกเลิกแล้ว",
        "Rejected" => "ผู้ให้บริการปฏิเสธ",
        _ => "อัปเดตแล้ว",
    };

    // R42 NT-3: never show raw English enum values to users.
    internal static string FormatReviewType(string reviewType) => reviewType switch
    {
        "Verified" or "0" => "ยืนยันแล้ว",
        "Public" or "1" => "รีวิวทั่วไป",
        _ => "รีวิว",
    };

    private static async Task<bool> IsProviderRecipientAsync(
        JsonElement? payload, Guid? recipientUserId, string actorRole, ILocalGoDbContext db, CancellationToken ct)
    {
        if (recipientUserId is not null && Guid.TryParse(GetStr(payload, "requestId"), out var requestId))
        {
            var requesterUserId = await db.ServiceRequests.AsNoTracking()
                .Where(r => r.Id == requestId)
                .Select(r => (Guid?)r.RequesterUserId)
                .FirstOrDefaultAsync(ct);
            if (requesterUserId is not null)
            {
                return requesterUserId != recipientUserId;
            }
        }

        return actorRole == "requester";
    }

    internal static string AppointmentUpdatedTitle(string status, bool toProvider = false, string actorRole = "") => status switch
    {
        "Pending" when toProvider => "ลูกค้าขอเลื่อนนัดหมาย กรุณายืนยันวันเวลาใหม่",
        "Pending" => "นัดหมายถูกเลื่อน รอผู้ให้บริการยืนยัน",
        "Cancelled" when actorRole == "requester" => "ลูกค้ายกเลิกนัดหมาย",
        "Cancelled" when actorRole == "provider" => "ผู้ให้บริการยกเลิกนัดหมาย",
        "Cancelled" when actorRole == "admin" => "ผู้ดูแลระบบยกเลิกนัดหมาย",
        "Confirmed" => "ผู้ให้บริการยืนยันนัดหมายแล้ว",
        "InProgress" => "ผู้ให้บริการเริ่มงานแล้ว",
        "Completed" => "งานเสร็จสิ้นแล้ว",
        "Cancelled" => "นัดหมายถูกยกเลิก",
        "Rejected" => "ผู้ให้บริการปฏิเสธนัดหมาย",
        _ => "อัปเดตสถานะนัดหมาย",
    };

    private static string AppointmentUpdatedDetail(string status, string declineReason, bool toProvider = false) => status switch
    {
        "Pending" when toProvider => "ตรวจสอบวันเวลาใหม่แล้วกดยืนยันนัด",
        "Pending" => "มีการเปลี่ยนวันเวลานัด กรุณาตรวจสอบและยืนยันอีกครั้ง",
        "Confirmed" => "นัดหมายได้รับการยืนยันแล้ว",
        "InProgress" => "งานกำลังดำเนินการ",
        "Completed" => "งานปิดเรียบร้อย คุณสามารถให้รีวิวได้",
        "Cancelled" => "นัดหมายนี้ถูกยกเลิกแล้ว",
        "Rejected" => string.IsNullOrWhiteSpace(declineReason)
            ? "คุณสามารถเลือกข้อเสนออื่นหรือนัดใหม่ได้"
            : $"เหตุผล: {declineReason.Trim()}",
        _ => $"สถานะ: {FormatAppointmentStatus(status)}",
    };

    public static string FormatScheduledAt(string scheduledAt)
    {
        if (!DateTime.TryParse(scheduledAt, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var utc))
        {
            return "";
        }

        var th = utc.AddHours(7);
        return $"วันเวลา: {th:dd/MM}/{th.Year + 543} {th:HH:mm} น.";
    }

    private static string ProviderOrDefault(JsonElement? payload)
    {
        var name = GetStr(payload, "providerName").Trim();
        return string.IsNullOrEmpty(name) ? "ผู้ให้บริการ" : name;
    }

    private static string TitleOrDefault(string? title) =>
        string.IsNullOrWhiteSpace(title) ? "งานของคุณ" : $"งาน “{title.Trim()}” ";

    private static string BidUpdatedDetail(JsonElement? payload, string? title)
    {
        var next = FormatBidPrice(payload);
        var prev = FormatAmount(GetStr(payload, "previousAmount"), GetStr(payload, "previousPriceText"));
        var who = ProviderOrDefault(payload);
        var what = TitleOrDefault(title).TrimEnd();
        return !string.IsNullOrEmpty(prev) && prev != next
            ? $"{who} ปรับราคา{what} จาก {prev} เป็น {next} บาท"
            : $"{who} แก้ไขข้อเสนอ{what} ราคา {next} บาท";
    }

    private static string FormatAmount(string amount, string priceText)
    {
        if (decimal.TryParse(amount, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d))
        {
            return d.ToString("#,0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        return priceText.Replace("บาท", string.Empty).Trim();
    }

    private static string FormatBidPrice(JsonElement? payload)
    {
        var amount = GetStr(payload, "amount");
        if (decimal.TryParse(amount, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d))
        {
            return d.ToString("#,0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        return GetStr(payload, "priceText").Replace("บาท", string.Empty).Trim();
    }

    private static string GetStr(JsonElement? el, string key) =>
        el?.TryGetProperty(key, out var v) == true
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString() ?? "",
                JsonValueKind.Null or JsonValueKind.Undefined => "",
                _ => v.GetRawText(),
            }
            : "";

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
