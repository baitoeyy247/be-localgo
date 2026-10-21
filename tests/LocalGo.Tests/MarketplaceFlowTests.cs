using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Application.Services;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalGo.Tests;

/// <summary>
/// Critical API flows: provider matching (active jobs + needsAppointmentConfirm),
/// GetByRequestIdAsync for provider viewer, GetDetailAsync provider access.
/// E2E: scripts/smoke-appointment-flow.sh
/// </summary>
public sealed class MarketplaceFlowTests
{
    [Fact]
    public async Task ListMatchingForProvider_includes_scheduled_job_with_needsAppointmentConfirm()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var sut = new ServiceRequestAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<ServiceRequestAppService>.Instance);

        var inbox = await sut.ListMatchingForProviderAsync(
            seed.ProviderUser.Id,
            excludeSmokeTestData: false,
            includeOwnRequests: true,
            CancellationToken.None);

        Assert.False(inbox.ExcludesOwnRequests);
        Assert.Contains(inbox.Areas, area => area.Name == "Main" && area.ServiceRadiusMeters == 8000);
        Assert.DoesNotContain(inbox.Items, x => x.Id == seed.Request.Id);
        var row = Assert.Single(inbox.MyJobs!, x => x.Id == seed.Request.Id);
        Assert.True(row.NeedsAppointmentConfirm);
        Assert.Equal(ServiceRequestStatus.Scheduled.ToString(), row.Status);
    }

    [Fact]
    public async Task ListMatchingForProvider_includes_direct_booking_pending_confirm()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedDirectBookingAsync(db);
        var sut = new ServiceRequestAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<ServiceRequestAppService>.Instance);

        var inbox = await sut.ListMatchingForProviderAsync(
            seed.ProviderUser.Id,
            excludeSmokeTestData: false,
            includeOwnRequests: true,
            CancellationToken.None);

        Assert.DoesNotContain(inbox.Items, x => x.Id == seed.Request.Id);
        var row = Assert.Single(inbox.MyJobs!, x => x.Id == seed.Request.Id);
        Assert.True(row.NeedsAppointmentConfirm);
        Assert.Equal(ServiceRequestStatus.ProviderSelected.ToString(), row.Status);
    }

    [Fact]
    public async Task GetByRequestIdAsync_allows_provider_viewer()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var sut = new AppointmentAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<AppointmentAppService>.Instance);

        var appointment = await sut.GetByRequestIdAsync(
            seed.ProviderUser.Id,
            seed.Request.Id,
            CancellationToken.None);

        Assert.NotNull(appointment);
        Assert.Equal(seed.Appointment!.Id, appointment!.Id);
        Assert.Equal(AppointmentStatus.Pending, appointment.Status);
    }

    [Fact]
    public async Task GetDetailAsync_allows_selected_provider_without_bid_on_direct_booking()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedDirectBookingAsync(db);
        var sut = new ServiceRequestAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<ServiceRequestAppService>.Instance);

        var detail = await sut.GetDetailAsync(
            seed.ProviderUser.Id,
            seed.Request.Id,
            isAdmin: false,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(seed.Request.Id, detail!.Id);
        Assert.Empty(detail.Bids);
    }

    [Fact]
    public async Task GetDetailAsync_denies_unrelated_provider()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var otherProviderUser = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"other-{Guid.NewGuid():N}",
            DisplayName = "Other",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var otherProvider = new Domain.Entities.Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = otherProviderUser.Id,
            ProviderType = ProviderType.Individual,
            Name = "Other Co",
            Status = ProviderStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(otherProviderUser);
        db.Providers.Add(otherProvider);
        await db.SaveChangesAsync();

        var sut = new ServiceRequestAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<ServiceRequestAppService>.Instance);

        var detail = await sut.GetDetailAsync(
            otherProviderUser.Id,
            seed.Request.Id,
            isAdmin: false,
            CancellationToken.None);

        Assert.Null(detail);
    }

    [Fact]
    public async Task Provider_advances_confirmed_appointment_through_completion_and_requester_can_review()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var notifications = TestDbFactory.NoopNotifications();
        var appointments = new AppointmentAppService(
            db,
            notifications,
            NullLogger<AppointmentAppService>.Instance);
        var reviews = new ReviewAppService(db, notifications);

        var confirmed = await appointments.UpdateStatusAsync(
            seed.ProviderUser.Id,
            seed.Appointment!.Id,
            AppointmentStatus.Confirmed,
            null,
            CancellationToken.None);
        Assert.Equal(AppointmentStatus.Confirmed, confirmed.Status);
        Assert.Equal(ServiceRequestStatus.Scheduled, seed.Request.Status);

        var skipped = await Assert.ThrowsAsync<AppException>(() => appointments.UpdateStatusAsync(
            seed.ProviderUser.Id,
            seed.Appointment.Id,
            AppointmentStatus.Completed,
            null,
            CancellationToken.None));
        Assert.Equal(409, skipped.StatusCode);

        var requesterStart = await Assert.ThrowsAsync<AppException>(() => appointments.UpdateStatusAsync(
            seed.Requester.Id,
            seed.Appointment.Id,
            AppointmentStatus.InProgress,
            null,
            CancellationToken.None));
        Assert.Equal(403, requesterStart.StatusCode);

        seed.Appointment.ScheduledAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var started = await appointments.UpdateStatusAsync(
            seed.ProviderUser.Id,
            seed.Appointment.Id,
            AppointmentStatus.InProgress,
            null,
            CancellationToken.None);
        Assert.Equal(AppointmentStatus.InProgress, started.Status);
        Assert.Equal(ServiceRequestStatus.InProgress, seed.Request.Status);

        var requesterComplete = await Assert.ThrowsAsync<AppException>(() => appointments.UpdateStatusAsync(
            seed.Requester.Id,
            seed.Appointment.Id,
            AppointmentStatus.Completed,
            null,
            CancellationToken.None));
        Assert.Equal(403, requesterComplete.StatusCode);

        var completed = await appointments.UpdateStatusAsync(
            seed.ProviderUser.Id,
            seed.Appointment.Id,
            AppointmentStatus.Completed,
            null,
            CancellationToken.None);
        Assert.Equal(AppointmentStatus.Completed, completed.Status);
        Assert.Equal(ServiceRequestStatus.Completed, seed.Request.Status);

        var review = await reviews.CreateAsync(
            seed.Requester.Id,
            seed.Provider.Id,
            new CreateReviewRequest(seed.Request.Id, ReviewType.Verified.ToString(), 5, "สอนดี"),
            CancellationToken.None);
        Assert.Equal(5, review.Rating);
        Assert.Equal(seed.Request.Id, review.ServiceRequestId);
        Assert.Equal("Test Requester", review.ReviewerDisplayName);

        var duplicate = await Assert.ThrowsAsync<AppException>(() => reviews.CreateAsync(
            seed.Requester.Id,
            seed.Provider.Id,
            new CreateReviewRequest(seed.Request.Id, ReviewType.Verified.ToString(), 4, "ซ้ำ"),
            CancellationToken.None));
        Assert.Equal(409, duplicate.StatusCode);
        Assert.Equal("คุณรีวิวงานนี้แล้ว", duplicate.Message);
        Assert.Equal(1, await db.Reviews.CountAsync(r => r.ServiceRequestId == seed.Request.Id));
    }

    [Fact]
    public async Task Start_and_complete_endpoints_follow_the_same_appointment_chain()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var notifications = TestDbFactory.NoopNotifications();
        var appointments = new AppointmentAppService(
            db,
            notifications,
            NullLogger<AppointmentAppService>.Instance);
        var requests = new ServiceRequestAppService(
            db,
            notifications,
            NullLogger<ServiceRequestAppService>.Instance);

        var tooSoon = await Assert.ThrowsAsync<AppException>(() =>
            requests.StartAsync(seed.ProviderUser.Id, seed.Request.Id, CancellationToken.None));
        Assert.Equal(409, tooSoon.StatusCode);

        await appointments.UpdateStatusAsync(
            seed.ProviderUser.Id,
            seed.Appointment!.Id,
            AppointmentStatus.Confirmed,
            null,
            CancellationToken.None);
        seed.Appointment.ScheduledAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await requests.StartAsync(seed.ProviderUser.Id, seed.Request.Id, CancellationToken.None);
        var appointment = await db.Appointments.SingleAsync(a => a.Id == seed.Appointment.Id);
        Assert.Equal(AppointmentStatus.InProgress, appointment.Status);
        Assert.Equal(ServiceRequestStatus.InProgress, seed.Request.Status);

        var requester = await Assert.ThrowsAsync<AppException>(() =>
            requests.CompleteAsync(seed.Requester.Id, seed.Request.Id, CancellationToken.None));
        Assert.Equal(403, requester.StatusCode);

        await requests.CompleteAsync(seed.ProviderUser.Id, seed.Request.Id, CancellationToken.None);
        appointment = await db.Appointments.SingleAsync(a => a.Id == seed.Appointment.Id);
        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        Assert.Equal(ServiceRequestStatus.Completed, seed.Request.Status);
    }
}
