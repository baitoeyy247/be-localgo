using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Application.Services;
using LocalGo.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LocalGo.Tests;

public sealed class NotificationRecipientTests
{
    [Fact]
    public async Task SubmitAsync_notifies_requester_not_provider()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedOpenBidRequestAsync(db);
        var notifications = Substitute.For<INotificationPublisher>();
        notifications
            .EnqueueAsync(
                Arg.Any<string>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = new BidAppService(db, notifications, NullLogger<BidAppService>.Instance);
        await sut.SubmitAsync(
            seed.ProviderUser.Id,
            seed.Request.Id,
            new SubmitBidRequest("500 THB", null, null, null, null),
            CancellationToken.None);

        await notifications.Received(1).EnqueueAsync(
            NotificationEvents.NewBidReceived,
            seed.Requester.Id,
            seed.Requester.LineUserId,
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());

        await notifications.DidNotReceive().EnqueueAsync(
            NotificationEvents.NewBidReceived,
            seed.ProviderUser.Id,
            Arg.Any<string?>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAsync_notifies_provider_owner_not_requester()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedOpenBidRequestAsync(db);
        var now = DateTime.UtcNow;
        seed.Request.Status = ServiceRequestStatus.HasOffers;
        var submitted = new Domain.Entities.Bid
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = seed.Request.Id,
            ProviderId = seed.Provider.Id,
            PriceText = "700",
            Status = BidStatus.Submitted,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Bids.Add(submitted);
        await db.SaveChangesAsync();

        var notifications = Substitute.For<INotificationPublisher>();
        notifications
            .EnqueueAsync(
                Arg.Any<string>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = new BidAppService(db, notifications, NullLogger<BidAppService>.Instance);
        await sut.SelectAsync(
            seed.Requester.Id,
            seed.Request.Id,
            new SelectBidRequest(submitted.Id),
            CancellationToken.None);

        await notifications.Received(1).EnqueueAsync(
            NotificationEvents.BidSelected,
            seed.ProviderUser.Id,
            seed.ProviderUser.LineUserId,
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());

        await notifications.DidNotReceive().EnqueueAsync(
            NotificationEvents.BidSelected,
            seed.Requester.Id,
            Arg.Any<string?>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectProviderAsync_notifies_provider_not_requester()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedOpenBidRequestAsync(db);
        var notifications = Substitute.For<INotificationPublisher>();
        notifications
            .EnqueueAsync(
                Arg.Any<string>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = new ServiceRequestAppService(db, notifications, NullLogger<ServiceRequestAppService>.Instance);
        await sut.SelectProviderAsync(
            seed.Requester.Id,
            seed.Request.Id,
            seed.Provider.Id,
            CancellationToken.None);

        await notifications.Received(1).EnqueueAsync(
            NotificationEvents.BidSelected,
            seed.ProviderUser.Id,
            seed.ProviderUser.LineUserId,
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());

        await notifications.DidNotReceive().EnqueueAsync(
            NotificationEvents.BidSelected,
            seed.Requester.Id,
            Arg.Any<string?>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAppointment_notifies_provider_not_requester()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedProviderSelectedBidAsync(db);
        var notifications = Substitute.For<INotificationPublisher>();
        notifications
            .EnqueueAsync(
                Arg.Any<string>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = new AppointmentAppService(db, notifications, NullLogger<AppointmentAppService>.Instance);
        await sut.CreateAsync(
            seed.Requester.Id,
            seed.Request.Id,
            new CreateAppointmentRequest(
                DateTime.UtcNow.AddDays(1),
                "Addr",
                null,
                null,
                null),
            CancellationToken.None);

        await notifications.Received(1).EnqueueAsync(
            NotificationEvents.AppointmentCreated,
            seed.ProviderUser.Id,
            seed.ProviderUser.LineUserId,
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());

        await notifications.DidNotReceive().EnqueueAsync(
            NotificationEvents.AppointmentCreated,
            seed.Requester.Id,
            Arg.Any<string?>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAppointmentStatus_by_provider_notifies_requester_only()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var notifications = Substitute.For<INotificationPublisher>();
        notifications
            .EnqueueAsync(
                Arg.Any<string>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = new AppointmentAppService(db, notifications, NullLogger<AppointmentAppService>.Instance);
        await sut.UpdateStatusAsync(
            seed.ProviderUser.Id,
            seed.Appointment!.Id,
            AppointmentStatus.Confirmed,
            CancellationToken.None);

        await notifications.Received(1).EnqueueAsync(
            NotificationEvents.AppointmentUpdated,
            seed.Requester.Id,
            seed.Requester.LineUserId,
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());

        await notifications.DidNotReceive().EnqueueAsync(
            NotificationEvents.AppointmentUpdated,
            seed.ProviderUser.Id,
            Arg.Any<string?>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAppointmentStatus_by_requester_notifies_provider_only()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var notifications = Substitute.For<INotificationPublisher>();
        notifications
            .EnqueueAsync(
                Arg.Any<string>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = new AppointmentAppService(db, notifications, NullLogger<AppointmentAppService>.Instance);
        await sut.UpdateStatusAsync(
            seed.Requester.Id,
            seed.Appointment!.Id,
            AppointmentStatus.Cancelled,
            CancellationToken.None);

        await notifications.Received(1).EnqueueAsync(
            NotificationEvents.AppointmentUpdated,
            seed.ProviderUser.Id,
            seed.ProviderUser.LineUserId,
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());

        await notifications.DidNotReceive().EnqueueAsync(
            NotificationEvents.AppointmentUpdated,
            seed.Requester.Id,
            Arg.Any<string?>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReviewCreate_notifies_provider_owner_not_reviewer()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: false);
        seed.Request.Status = ServiceRequestStatus.Completed;
        await db.SaveChangesAsync();

        var notifications = Substitute.For<INotificationPublisher>();
        notifications
            .EnqueueAsync(
                Arg.Any<string>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = new ReviewAppService(db, notifications);
        await sut.CreateAsync(
            seed.Requester.Id,
            seed.Provider.Id,
            new CreateReviewRequest(seed.Request.Id, ReviewType.Verified.ToString(), 5, "Great"),
            CancellationToken.None);

        await notifications.Received(1).EnqueueAsync(
            NotificationEvents.ReviewReceived,
            seed.ProviderUser.Id,
            seed.ProviderUser.LineUserId,
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());

        await notifications.DidNotReceive().EnqueueAsync(
            NotificationEvents.ReviewReceived,
            seed.Requester.Id,
            Arg.Any<string?>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ShouldNotifyRecipient_returns_false_for_self()
    {
        var id = Guid.NewGuid();
        Assert.False(NotificationRecipients.ShouldNotify(id, id));
        Assert.True(NotificationRecipients.ShouldNotify(id, Guid.NewGuid()));
    }
}
