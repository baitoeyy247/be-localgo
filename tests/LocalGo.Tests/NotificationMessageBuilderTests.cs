using System.Text.Encodings.Web;
using System.Text.Json;
using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Infrastructure.Notifications;

namespace LocalGo.Tests;

public sealed class NotificationMessageBuilderTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    [Theory]
    [InlineData("ช่างประปา", "ซ่อมท่อรั่ว", "บริการ: ช่างประปา\nซ่อมท่อรั่ว")]
    [InlineData("ช่างประปา", "", "บริการ: ช่างประปา")]
    [InlineData("", "ซ่อมท่อรั่ว", "ซ่อมท่อรั่ว")]
    public void FormatServiceContext_formats_category_and_title(string category, string title, string expected)
    {
        var result = NotificationMessageBuilder.FormatServiceContext(
            new NotificationMessageBuilder.ServiceContext(
                string.IsNullOrEmpty(title) ? null : title,
                string.IsNullOrEmpty(category) ? null : category));

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task BuildAsync_NewMatchingRequest_includes_service_context_from_db()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedOpenMatchingNotificationAsync(db);
        var requestId = Guid.NewGuid();
        db.ServiceRequests.Add(new LocalGo.Domain.Entities.ServiceRequest
        {
            Id = requestId,
            RequesterUserId = seed.Requester.Id,
            CategoryId = seed.Category.Id,
            Title = "ซ่อมท่อรั่ว",
            Latitude = seed.RequestLatitude,
            Longitude = seed.RequestLongitude,
            Location = LocalGo.Application.Common.GeoHelper.CreatePoint(seed.RequestLatitude, seed.RequestLongitude),
            Status = LocalGo.Domain.Enums.ServiceRequestStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var payload = System.Text.Json.JsonDocument.Parse(
            $$"""{"requestId":"{{requestId}}"}""").RootElement;

        var (altText, message) = await NotificationMessageBuilder.BuildAsync(
            NotificationEvents.NewMatchingRequest,
            payload,
            "https://liff.example",
            db,
            null,
            CancellationToken.None);

        Assert.Equal("มีงานใหม่ในพื้นที่ของคุณ", altText);
        var json = JsonSerializer.Serialize(message, JsonOpts);
        Assert.Contains("บริการ:", json, StringComparison.Ordinal);
        Assert.Contains("ซ่อมท่อรั่ว", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_NewBidReceived_uses_payload_service_context()
    {
        await using var db = TestDbFactory.CreateContext();
        var payload = System.Text.Json.JsonDocument.Parse(
            """
            {
              "requestId":"00000000-0000-0000-0000-000000000001",
              "title":"ทำความสะอาดบ้าน",
              "categoryName":"แม่บ้าน",
              "providerName":"คุณสมหญิง",
              "priceText":"800 บาท"
            }
            """).RootElement;

        var (_, message) = await NotificationMessageBuilder.BuildAsync(
            NotificationEvents.NewBidReceived,
            payload,
            "https://liff.example",
            db,
            null,
            CancellationToken.None);

        var json = JsonSerializer.Serialize(message, JsonOpts);
        Assert.Contains("บริการ: แม่บ้าน", json, StringComparison.Ordinal);
        Assert.Contains("ทำความสะอาดบ้าน", json, StringComparison.Ordinal);
        Assert.Contains("คุณสมหญิง · 800 บาท", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_AppointmentCreated_uses_provider_path_for_provider_recipient()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var payload = JsonDocument.Parse(
            $$"""{"requestId":"{{seed.Request.Id}}","status":"Pending"}""").RootElement;

        var (_, message) = await NotificationMessageBuilder.BuildAsync(
            NotificationEvents.AppointmentCreated,
            payload,
            "https://liff.example",
            db,
            seed.ProviderUser.Id,
            CancellationToken.None);

        var json = JsonSerializer.Serialize(message, JsonOpts);
        Assert.Contains($"/provider/requests/{seed.Request.Id}", json, StringComparison.Ordinal);
        Assert.DoesNotContain("/requester/appointment", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_AppointmentUpdated_uses_requester_path_for_requester_recipient()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var payload = JsonDocument.Parse(
            $$"""{"requestId":"{{seed.Request.Id}}","status":"Confirmed"}""").RootElement;

        var (_, message) = await NotificationMessageBuilder.BuildAsync(
            NotificationEvents.AppointmentUpdated,
            payload,
            "https://liff.example",
            db,
            seed.Requester.Id,
            CancellationToken.None);

        var json = JsonSerializer.Serialize(message, JsonOpts);
        Assert.Contains(
            $"/requester/appointment?requestId={seed.Request.Id}",
            json,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_AppointmentUpdated_uses_provider_path_for_provider_recipient()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var payload = JsonDocument.Parse(
            $$"""{"requestId":"{{seed.Request.Id}}","status":"Cancelled"}""").RootElement;

        var (_, message) = await NotificationMessageBuilder.BuildAsync(
            NotificationEvents.AppointmentUpdated,
            payload,
            "https://liff.example",
            db,
            seed.ProviderUser.Id,
            CancellationToken.None);

        var json = JsonSerializer.Serialize(message, JsonOpts);
        Assert.Contains($"/provider/requests/{seed.Request.Id}", json, StringComparison.Ordinal);
    }
}
