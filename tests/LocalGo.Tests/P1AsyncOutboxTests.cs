using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Common;
using LocalGo.Domain.Enums;
using LocalGo.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LocalGo.Tests;

public sealed class P1AsyncOutboxTests
{
    private sealed class CapturingWork : IBackgroundWork
    {
        public List<Func<IServiceProvider, CancellationToken, Task>> Items { get; } = new();
        public void Enqueue(Func<IServiceProvider, CancellationToken, Task> work) => Items.Add(work);
    }

    [Fact]
    public async Task Enqueue_defers_send_and_dispatch_sends_exactly_once()
    {
        await using var db = TestDbFactory.CreateContext();
        var sender = Substitute.For<INotificationSender>();
        sender.SendAsync(default!, default).ReturnsForAnyArgs(new NotificationSendResult(NotificationSendOutcome.Sent));
        var work = new CapturingWork();
        var sut = new NotificationPublisher(db, sender, NullLogger<NotificationPublisher>.Instance, work);

        await sut.EnqueueAsync(NotificationEvents.BidSelected, Guid.NewGuid(), "U1", new { }, CancellationToken.None);

        Assert.Equal(NotificationStatus.Pending, db.NotificationLogs.Single().Status);
        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
        Assert.Single(work.Items);

        var id = db.NotificationLogs.Single().Id;
        await sut.DispatchAsync(id);
        await sut.DispatchAsync(id); // second dispatch (e.g. retry race) must not resend
        await sender.ReceivedWithAnyArgs(1).SendAsync(default!, default);
        Assert.Equal(NotificationStatus.Sent, db.NotificationLogs.Single().Status);
    }

    [Fact]
    public void ServerTiming_accumulates_per_request()
    {
        var t = ServerTiming.Begin();
        ServerTiming.AddSave(TimeSpan.FromMilliseconds(5));
        ServerTiming.AddNotify(TimeSpan.FromMilliseconds(3));
        Assert.Equal(5, t.SaveMs, 1);
        Assert.Equal(3, t.NotifyMs, 1);
    }
}
