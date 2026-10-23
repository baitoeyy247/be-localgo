using System.Text.Encodings.Web;
using System.Text.Json;
using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Infrastructure.Notifications;

namespace LocalGo.Tests;

public sealed class R45NotifyReviewTests
{
    private static readonly JsonSerializerOptions Opts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static async Task<string> Build(string evt, string json, string liff = "")
    {
        await using var db = TestDbFactory.CreateContext();
        var (alt, msg) = await NotificationMessageBuilder.BuildAsync(
            evt, JsonDocument.Parse(json).RootElement, liff, db, null, CancellationToken.None);
        return alt + "\n" + JsonSerializer.Serialize(msg, Opts);
    }

    [Fact]
    public async Task Unknown_event_does_not_leak_english_event_name()
    {
        var t = await Build("SomethingNew", "{}");
        Assert.DoesNotContain("SomethingNew", t);
        Assert.Contains("มีการอัปเดตใหม่ใน LocalGo", t);
    }

    [Fact]
    public async Task Flex_body_is_never_empty()
    {
        var t = await Build(NotificationEvents.NewMatchingRequest, """{"requestId":"6026d7b7-0000-0000-0000-000000000000"}""", "https://liff.line.me/x");
        Assert.DoesNotContain("\"text\":\"\"", t);
        Assert.Contains("https://liff.line.me/x/provider/bid?requestId=", t);
    }

    [Fact]
    public async Task Unknown_status_is_thai()
    {
        var t = await Build(NotificationEvents.AppointmentCreated, """{"status":"Weird","actorRole":"provider"}""");
        Assert.DoesNotContain("Weird", t);
    }

    [Fact]
    public void Scheduled_time_is_thai_time_buddhist_year()
    {
        Assert.Equal("วันเวลา: 24/10/2569 09:30 น.", NotificationMessageBuilder.FormatScheduledAt("2026-10-24T02:30:00Z"));
    }

    [Theory]
    [InlineData(400, true)]
    [InlineData(403, true)]
    [InlineData(429, false)]
    [InlineData(500, false)]
    public void Line_4xx_is_permanent_except_429(int code, bool permanent)
        => Assert.Equal(permanent, LineNotificationSender.IsPermanent(code));
}
