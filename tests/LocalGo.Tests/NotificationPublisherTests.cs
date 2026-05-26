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
    public async Task EnqueueAsync_skips_when_messaging_token_not_configured()
    {
        await using var db = TestDbFactory.CreateContext();
        var sut = CreatePublisher(db, new LineSettings { MessagingAccessToken = "YOUR_MESSAGING_ACCESS_TOKEN" });

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
    public async Task EnqueueAsync_skips_dev_login_line_user_id()
    {
        await using var db = TestDbFactory.CreateContext();
        var sut = CreatePublisher(db, new LineSettings { MessagingAccessToken = "valid-channel-token" });

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

    private static NotificationPublisher CreatePublisher(LocalGo.Infrastructure.Persistence.LocalGoDbContext db, LineSettings settings)
    {
        var httpFactory = Substitute.For<IHttpClientFactory>();
        return new NotificationPublisher(
            db,
            httpFactory,
            Options.Create(settings),
            NullLogger<NotificationPublisher>.Instance);
    }
}
