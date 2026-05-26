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
                null,
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
                null,
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
                null,
                Publish: true),
            CancellationToken.None);

        var matching = await sut.ListMatchingForProviderAsync(
            seed.MatchingProviderUser.Id,
            excludeSmokeTestData: false,
            includeOwnRequests: false,
            CancellationToken.None);

        Assert.Contains(matching, x => x.Id == request.Id);

        await notifications.Received(1).EnqueueAsync(
            NotificationEvents.NewMatchingRequest,
            seed.MatchingProviderUser.Id,
            seed.MatchingProviderUser.LineUserId,
            Arg.Is<object>(p => PayloadContainsRequestId(p, request.Id)),
            Arg.Any<CancellationToken>());
    }

    private static bool PayloadContainsRequestId(object payload, Guid requestId)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        return json.Contains(requestId.ToString(), StringComparison.Ordinal);
    }
}
