using System.Net;
using System.Text.Json;
using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using LocalGo.Infrastructure.Line;
using LocalGo.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LocalGo.Tests;

public sealed class NotificationPublisherTests
{
    [Fact]
    public async Task EnqueueAsync_line_mode_skips_when_messaging_token_not_configured()
    {
        await using var db = TestDbFactory.CreateContext();
        var sut = CreatePublisher(db, lineSettings: new LineSettings { MessagingAccessToken = "YOUR_MESSAGING_ACCESS_TOKEN" });

        await sut.EnqueueAsync(
            NotificationEvents.NewMatchingRequest,
            Guid.NewGuid(),
            "Ureal123",
            new { requestId = Guid.NewGuid(), title = "Test" },
            CancellationToken.None);

        var log = db.NotificationLogs.Single();
        Assert.Equal(NotificationStatus.Skipped, log.Status);
        Assert.Contains("not configured", log.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnqueueAsync_line_mode_skips_dev_login_line_user_id()
    {
        await using var db = TestDbFactory.CreateContext();
        var sut = CreatePublisher(db, lineSettings: new LineSettings { MessagingAccessToken = "valid-channel-token" });

        await sut.EnqueueAsync(
            NotificationEvents.NewMatchingRequest,
            Guid.NewGuid(),
            "dev-abc123",
            new { requestId = Guid.NewGuid(), title = "Test" },
            CancellationToken.None);

        var log = db.NotificationLogs.Single();
        Assert.Equal(NotificationStatus.Skipped, log.Status);
        Assert.Contains("Dev login", log.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnqueueAsync_line_mode_calls_line_push_api()
    {
        await using var db = TestDbFactory.CreateContext();
        HttpRequestMessage? captured = null;
        var handler = new CapturingHttpHandler((req, _) =>
        {
            captured = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var httpFactory = CreateHttpFactory(handler);
        var sut = CreatePublisher(
            db,
            lineSettings: new LineSettings { MessagingAccessToken = "valid-channel-token", LiffId = "123" },
            httpFactory: httpFactory);

        await sut.EnqueueAsync(
            NotificationEvents.NewMatchingRequest,
            Guid.NewGuid(),
            "Ureal123",
            new { requestId = Guid.NewGuid(), title = "Test job" },
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https://api.line.me/v2/bot/message/push", captured.RequestUri?.ToString());
        httpFactory.Received(1).CreateClient("line");

        var log = db.NotificationLogs.Single();
        Assert.Equal(NotificationStatus.Sent, log.Status);
    }

    [Fact]
    public async Task EnqueueAsync_console_mode_logs_structured_line_without_line_client()
    {
        await using var db = TestDbFactory.CreateContext();
        var seeded = await TestDbFactory.SeedMarketplaceJobAsync(db);
        var stdout = Substitute.For<INotifyStdout>();
        var httpFactory = Substitute.For<IHttpClientFactory>();
        var appointmentId = Guid.NewGuid();

        var sut = CreatePublisher(
            db,
            lineSettings: new LineSettings { MessagingAccessToken = "YOUR_MESSAGING_ACCESS_TOKEN" },
            httpFactory: httpFactory,
            stdout: stdout,
            useConsoleSender: true);

        await sut.EnqueueAsync(
            NotificationEvents.AppointmentUpdated,
            seeded.Requester.Id,
            "dev-should-still-log",
            new
            {
                requestId = seeded.Request.Id,
                appointmentId,
                title = seeded.Request.Title,
                categoryName = "Test Category",
                status = "Confirmed",
            },
            CancellationToken.None);

        httpFactory.DidNotReceive().CreateClient(Arg.Any<string>());

        stdout.Received(1).WriteLine(Arg.Is<string>(line => line.StartsWith("[NOTIFY] ", StringComparison.Ordinal)));
        var json = stdout.ReceivedCalls().Single().GetArguments()[0]!.ToString()!["[NOTIFY] ".Length..];
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(NotificationEvents.AppointmentUpdated, root.GetProperty("event").GetString());
        Assert.Equal(seeded.Requester.Id, root.GetProperty("recipientId").GetGuid());
        Assert.Equal("requester", root.GetProperty("recipientRole").GetString());
        Assert.Equal(seeded.Request.Id, root.GetProperty("requestId").GetGuid());
        Assert.Equal(appointmentId, root.GetProperty("appointmentId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
        Assert.True(root.TryGetProperty("timestamp", out _));

        var log = db.NotificationLogs.Single();
        Assert.Equal(NotificationStatus.Sent, log.Status);
    }

    [Theory]
    [InlineData("line", false)]
    [InlineData("console", true)]
    [InlineData("LINE", false)]
    [InlineData("Console", true)]
    public void NotifySettings_parses_mode(string mode, bool isConsole)
    {
        var settings = new NotifySettings { Mode = mode };
        Assert.Equal(isConsole, settings.IsConsole);
    }

    private static NotificationPublisher CreatePublisher(
        LocalGo.Infrastructure.Persistence.LocalGoDbContext db,
        LineSettings lineSettings,
        IHttpClientFactory? httpFactory = null,
        INotifyStdout? stdout = null,
        bool useConsoleSender = false)
    {
        httpFactory ??= Substitute.For<IHttpClientFactory>();
        stdout ??= Substitute.For<INotifyStdout>();

        INotificationSender sender = useConsoleSender
            ? new ConsoleNotificationSender(db, Options.Create(lineSettings), stdout)
            : new LineNotificationSender(
                db,
                httpFactory,
                Options.Create(lineSettings),
                NullLogger<LineNotificationSender>.Instance);

        return new NotificationPublisher(db, sender, NullLogger<NotificationPublisher>.Instance);
    }

    private static IHttpClientFactory CreateHttpFactory(HttpMessageHandler handler)
    {
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient("line").Returns(_ => new HttpClient(handler, disposeHandler: false));
        return httpFactory;
    }

    private sealed class CapturingHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
