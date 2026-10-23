using System.Text.Encodings.Web;
using System.Text.Json;
using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Infrastructure.Notifications;

namespace LocalGo.Tests;

public sealed class R42NotifyTemplateTests
{
    private static readonly JsonSerializerOptions Opts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static async Task<string> Build(string evt, string json)
    {
        await using var db = TestDbFactory.CreateContext();
        var (alt, msg) = await NotificationMessageBuilder.BuildAsync(
            evt, JsonDocument.Parse(json).RootElement, "", db, null, CancellationToken.None);
        return alt + "\n" + JsonSerializer.Serialize(msg, Opts);
    }

    [Fact]
    public async Task Created_to_provider_asks_to_confirm()
    {
        var t = await Build(NotificationEvents.AppointmentCreated, """{"status":"Pending","actorRole":"requester"}""");
        Assert.Contains("มีนัดหมายใหม่จากลูกค้า กรุณายืนยัน", t);
        Assert.DoesNotContain("รอผู้ให้บริการ", t);
    }

    [Fact]
    public async Task Reschedule_to_provider_uses_provider_copy()
    {
        var t = await Build(NotificationEvents.AppointmentUpdated, """{"status":"Pending","actorRole":"requester"}""");
        Assert.Contains("ลูกค้าขอเลื่อนนัดหมาย กรุณายืนยันวันเวลาใหม่", t);
        Assert.DoesNotContain("รอผู้ให้บริการ", t);
    }

    [Theory]
    [InlineData("requester", "ลูกค้ายกเลิกนัดหมาย")]
    [InlineData("provider", "ผู้ให้บริการยกเลิกนัดหมาย")]
    public async Task Cancel_names_actor(string actor, string expected)
    {
        var t = await Build(NotificationEvents.AppointmentUpdated, $$"""{"status":"Cancelled","actorRole":"{{actor}}"}""");
        Assert.StartsWith(expected, t);
    }

    [Fact]
    public async Task Review_has_no_english()
    {
        var t = await Build(NotificationEvents.ReviewReceived, """{"reviewType":"Verified","rating":5}""");
        Assert.Contains("ยืนยันแล้ว · 5 ดาว", t);
        Assert.DoesNotContain("Verified", t);
    }
}
