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

    [Fact]
    public async Task EnqueueAsync_console_mode_review_received_with_numeric_rating_is_sent()
    {
        await using var db = TestDbFactory.CreateContext();
        var stdout = Substitute.For<INotifyStdout>();
        var sut = CreatePublisher(db, new LineSettings(), stdout: stdout, useConsoleSender: true);

        await sut.EnqueueAsync(
            NotificationEvents.ReviewReceived,
            Guid.NewGuid(),
            null,
            new { providerId = Guid.NewGuid(), rating = 5, reviewType = "RequesterToProvider", serviceRequestId = Guid.NewGuid() },
            CancellationToken.None);

        var log = db.NotificationLogs.Single();
        Assert.Equal(NotificationStatus.Sent, log.Status);
        Assert.Equal(1, log.RetryCount);
        stdout.Received(1).WriteLine(Arg.Is<string>(l => l.StartsWith("[NOTIFY] ") && l.Contains("ReviewReceived")));
    }

    [Theory]
    [InlineData(NotificationEvents.BidUpdated, "ช่างเอ แก้ไขข้อเสนองานของคุณ ราคา 1,500 บาท")]
    [InlineData(NotificationEvents.BidWithdrawn, "ช่างเอ ถอนข้อเสนอสำหรับงานของคุณแล้ว")]
    [InlineData(NotificationEvents.AccountSuspended, "ถูกระงับ")]
    [InlineData(NotificationEvents.AccountReactivated, "กลับมาใช้งานได้")]
    public async Task EnqueueAsync_console_mode_new_events_are_sent_with_message(string eventType, string expected)
    {
        await using var db = TestDbFactory.CreateContext();
        var stdout = Substitute.For<INotifyStdout>();
        var sut = CreatePublisher(db, new LineSettings(), stdout: stdout, useConsoleSender: true);

        await sut.EnqueueAsync(
            eventType,
            Guid.NewGuid(),
            null,
            new { requestId = Guid.NewGuid(), bidId = Guid.NewGuid(), providerName = "ช่างเอ", priceText = "1500 บาท", amount = 1500m, status = "Suspended" },
            CancellationToken.None);

        var log = db.NotificationLogs.Single();
        Assert.Equal(NotificationStatus.Sent, log.Status);
        stdout.Received(1).WriteLine(Arg.Is<string>(l => l.StartsWith("[NOTIFY] ") && l.Contains(eventType)
            && JsonDocument.Parse(l.Substring(9)).RootElement.GetProperty("message").GetString()!.Contains(expected)));
    }

    [Fact]
    public async Task Sender_exception_marks_failed_with_error_then_retry_sends()
    {
        await using var db = TestDbFactory.CreateContext();
        var sender = Substitute.For<INotificationSender>();
        var calls = 0;
        sender.SendAsync(Arg.Any<NotificationSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++calls == 1
                ? throw new InvalidOperationException("boom")
                : Task.FromResult(new NotificationSendResult(NotificationSendOutcome.Sent)));
        var sut = new NotificationPublisher(db, sender, NullLogger<NotificationPublisher>.Instance);

        await sut.EnqueueAsync(NotificationEvents.NewBidReceived, Guid.NewGuid(), null, new { requestId = Guid.NewGuid() });

        var log = db.NotificationLogs.Single();
        Assert.Equal(NotificationStatus.Failed, log.Status);
        Assert.Equal(1, log.RetryCount);
        Assert.Contains("boom", log.ErrorMessage);

        log.CreatedAt = DateTime.UtcNow.AddMinutes(-1); // past backoff window
        await db.SaveChangesAsync();
        await sut.RetryPendingAsync();

        Assert.Equal(NotificationStatus.Sent, log.Status);
        Assert.Equal(2, log.RetryCount);
        Assert.Null(log.ErrorMessage);
        Assert.NotNull(log.SentAt);
    }

    [Fact]
    public async Task RetryPendingAsync_picks_up_stale_pending_rows()
    {
        await using var db = TestDbFactory.CreateContext();
        var sender = Substitute.For<INotificationSender>();
        sender.SendAsync(Arg.Any<NotificationSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(new NotificationSendResult(NotificationSendOutcome.Sent));
        db.NotificationLogs.Add(new NotificationLog
        {
            Id = Guid.NewGuid(),
            EventType = NotificationEvents.ReviewReceived,
            Status = NotificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            PayloadJson = "{}",
        });
        db.NotificationLogs.Add(new NotificationLog
        {
            Id = Guid.NewGuid(),
            EventType = NotificationEvents.NewBidReceived,
            Status = NotificationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            PayloadJson = "{}",
        });
        await db.SaveChangesAsync();
        var sut = new NotificationPublisher(db, sender, NullLogger<NotificationPublisher>.Instance);

        await sut.RetryPendingAsync();

        Assert.Equal(1, db.NotificationLogs.Count(n => n.Status == NotificationStatus.Sent));
        Assert.Equal(1, db.NotificationLogs.Count(n => n.Status == NotificationStatus.Pending));
    }

    [Fact]
    public void Backoff_grows_exponentially()
    {
        var created = DateTime.UtcNow;
        var log = new NotificationLog { Id = Guid.NewGuid(), EventType = "x", CreatedAt = created, RetryCount = 1 };
        Assert.Equal(created.AddSeconds(15), NotificationPublisher.NextAttemptAt(log));
        log.RetryCount = 3;
        Assert.Equal(created.AddSeconds(105), NotificationPublisher.NextAttemptAt(log));
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
