using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Dtos;
using LocalGo.Application.Services;
using LocalGo.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LocalGo.Tests;

public sealed class MatchingNotificationTests
{
    [Fact]
    public async Task PublishAsync_enqueues_NewMatchingRequest_for_in_range_provider()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedOpenMatchingNotificationAsync(db);
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
        var draft = await sut.CreateAsync(
            seed.Requester.Id,
            new CreateServiceRequestDto(
                seed.Category.Id,
                "Need plumber",
                null,
                "Bangkok",
                seed.RequestLatitude,
                seed.RequestLongitude,
                seed.SearchRadiusMeters,
                null,
                null,
                "เปิดรับราคา",
                Publish: false),
            CancellationToken.None);

        await sut.PublishAsync(seed.Requester.Id, draft.Id, CancellationToken.None);

        await notifications.Received(1).EnqueueAsync(
            NotificationEvents.NewMatchingRequest,
            seed.MatchingProviderUser.Id,
            seed.MatchingProviderUser.LineUserId,
            Arg.Is<object>(p => PayloadContainsRequestId(p, draft.Id)),
            Arg.Any<CancellationToken>());

        await notifications.DidNotReceive().EnqueueAsync(
            NotificationEvents.NewMatchingRequest,
            seed.FarProviderUser.Id,
            Arg.Any<string?>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());

        await notifications.DidNotReceive().EnqueueAsync(
            NotificationEvents.NewMatchingRequest,
            seed.WrongCategoryProviderUser.Id,
            Arg.Any<string?>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_with_Publish_enqueues_for_provider_without_service_branch_links()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedOpenMatchingNotificationAsync(
            db,
            matchingProviderWithoutBranchLinks: true);
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
        var published = await sut.CreateAsync(
            seed.Requester.Id,
            new CreateServiceRequestDto(
                seed.Category.Id,
                "Published on create",
                null,
                "Bangkok",
                seed.RequestLatitude,
                seed.RequestLongitude,
                seed.SearchRadiusMeters,
                null,
                null,
                "เปิดรับราคา",
                Publish: true),
            CancellationToken.None);

        await notifications.Received(1).EnqueueAsync(
            NotificationEvents.NewMatchingRequest,
            seed.MatchingProviderUser.Id,
            seed.MatchingProviderUser.LineUserId,
            Arg.Is<object>(p => PayloadContainsRequestId(p, published.Id)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListMatching_and_notification_use_same_in_range_provider()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedOpenMatchingNotificationAsync(db);
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
        var request = await sut.CreateAsync(
            seed.Requester.Id,
            new CreateServiceRequestDto(
                seed.Category.Id,
                "List vs notify",
                null,
                "Bangkok",
                seed.RequestLatitude,
                seed.RequestLongitude,
                seed.SearchRadiusMeters,
                null,
                null,
                "เปิดรับราคา",
                Publish: true),
            CancellationToken.None);

        var matching = await sut.ListMatchingForProviderAsync(
            seed.MatchingProviderUser.Id,
            excludeSmokeTestData: false,
            includeOwnRequests: false,
            CancellationToken.None);

        Assert.Contains(matching.Items, x => x.Id == request.Id);
        Assert.True(matching.ExcludesOwnRequests);

        var ownerForm = await sut.GetBidFormAsync(seed.Requester.Id, request.Id, CancellationToken.None);
        Assert.False(ownerForm.Allowed);
        Assert.Contains("โพสต์เอง", ownerForm.Reason);

        var strangerForm = await sut.GetBidFormAsync(Guid.NewGuid(), request.Id, CancellationToken.None);
        Assert.False(strangerForm.Allowed);
        Assert.Contains("โปรไฟล์", strangerForm.Reason);

        var farForm = await sut.GetBidFormAsync(seed.FarProviderUser.Id, request.Id, CancellationToken.None);
        Assert.False(farForm.Allowed);
        Assert.Contains("นอกพื้นที่", farForm.Reason);

        var allowedForm = await sut.GetBidFormAsync(seed.MatchingProviderUser.Id, request.Id, CancellationToken.None);
        Assert.True(allowedForm.Allowed);
        Assert.NotNull(allowedForm.Request);

        var missingForm = await sut.GetBidFormAsync(seed.MatchingProviderUser.Id, Guid.NewGuid(), CancellationToken.None);
        Assert.False(missingForm.Allowed);
        Assert.Equal("ไม่พบงานนี้", missingForm.Reason);

        await notifications.Received(1).EnqueueAsync(
            NotificationEvents.NewMatchingRequest,
            seed.MatchingProviderUser.Id,
            seed.MatchingProviderUser.LineUserId,
            Arg.Is<object>(p => PayloadContainsRequestId(p, request.Id)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsync_notifies_each_bidder_once()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db);
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
        await sut.CancelAsync(seed.Requester.Id, seed.Request.Id, CancellationToken.None);

        await notifications.Received(1).EnqueueAsync(
            NotificationEvents.RequestCancelled,
            seed.ProviderUser.Id,
            seed.ProviderUser.LineUserId,
            Arg.Is<object>(p => PayloadContainsRequestId(p, seed.Request.Id)),
            Arg.Any<CancellationToken>());
        await notifications.DidNotReceive().EnqueueAsync(
            NotificationEvents.RequestCancelled,
            seed.Requester.Id,
            Arg.Any<string?>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    private static bool PayloadContainsRequestId(object payload, Guid requestId)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        return json.Contains(requestId.ToString(), StringComparison.Ordinal);
    }
}
