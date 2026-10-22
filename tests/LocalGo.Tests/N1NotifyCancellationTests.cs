using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Services;
using LocalGo.Domain.Enums;
using LocalGo.Infrastructure.Line;
using LocalGo.Infrastructure.Notifications;
using LocalGo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LocalGo.Tests;

/// <summary>N-1: client abort (request token cancelled) right after the status commit must not drop the notification.</summary>
public sealed class N1NotifyCancellationTests
{
    private sealed class CancelAfterSaveInterceptor : SaveChangesInterceptor
    {
        public CancellationTokenSource? Cts { get; set; }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            Cts?.Cancel();
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task Confirm_with_token_cancelled_after_SaveChanges_still_sends_one_notification()
    {
        var interceptor = new CancelAfterSaveInterceptor();
        var options = new DbContextOptionsBuilder<LocalGoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        await using var db = new LocalGoDbContext(options);
        db.Database.EnsureCreated();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);

        var stdout = Substitute.For<INotifyStdout>();
        var sender = new ConsoleNotificationSender(db, Options.Create(new LineSettings()), stdout);
        var publisher = new NotificationPublisher(db, sender, NullLogger<NotificationPublisher>.Instance);
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<AppointmentAppService>>();
        var sut = new AppointmentAppService(db, publisher, logger);

        using var cts = new CancellationTokenSource();
        interceptor.Cts = cts; // cancels the "request" token as soon as the status change is committed

        await sut.UpdateStatusAsync(
            seed.ProviderUser.Id, seed.Appointment!.Id, AppointmentStatus.Confirmed, null, cts.Token);

        Assert.True(cts.IsCancellationRequested);
        var log = db.NotificationLogs.Single();
        Assert.Equal(NotificationEvents.AppointmentUpdated, log.EventType);
        Assert.Equal(NotificationStatus.Sent, log.Status);
        stdout.Received(1).WriteLine(Arg.Is<string>(l => l.StartsWith("[NOTIFY] ") && l.Contains("AppointmentUpdated")));
        Assert.DoesNotContain(logger.ReceivedCalls(), c => c.GetMethodInfo().Name == "Log"
            && c.GetArguments()[0] is Microsoft.Extensions.Logging.LogLevel lvl && lvl >= Microsoft.Extensions.Logging.LogLevel.Warning);
    }
}
