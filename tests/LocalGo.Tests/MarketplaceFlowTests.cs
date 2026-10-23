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

    [Fact]
    public async Task BookFromProviderService_creates_appointment_with_null_decline_reason()
    {
        await using var db = TestDbFactory.CreateContext();
        var now = DateTime.UtcNow;
        var requester = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-book-{Guid.NewGuid():N}",
            DisplayName = "Booker",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var providerUser = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-book-prov-{Guid.NewGuid():N}",
            DisplayName = "Book Provider",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var category = new Domain.Entities.ServiceCategory
        {
            Id = Guid.NewGuid(),
            Name = "Book Cat",
            Slug = $"book-{Guid.NewGuid():N}",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var provider = new Domain.Entities.Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = providerUser.Id,
            ProviderType = ProviderType.Individual,
            Name = "Book Co",
            Status = ProviderStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var service = new Domain.Entities.ProviderService
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            CategoryId = category.Id,
            Title = "Bookable Service",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Users.AddRange(requester, providerUser);
        db.ServiceCategories.Add(category);
        db.Providers.Add(provider);
        db.ProviderServices.Add(service);
        await db.SaveChangesAsync();

        var appointments = new AppointmentAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<AppointmentAppService>.Instance);

        var appointment = await appointments.BookFromProviderServiceAsync(
            requester.Id,
            provider.Id,
            service.Id,
            new CreateAppointmentRequest(
                now.AddDays(1),
                "123 Test Rd",
                13.7563,
                100.5018,
                "note only"),
            CancellationToken.None);

        Assert.Equal(AppointmentStatus.Pending, appointment.Status);
        Assert.Null(appointment.DeclineReason);
        Assert.Equal("note only", appointment.Note);

        // Same projection shape as GET /api/admin/appointments — must not throw on DeclineReason.
        var adminRows = await db.Appointments.AsNoTracking()
            .Where(a => a.Id == appointment.Id)
            .Select(a => new
            {
                a.Id,
                a.Note,
                a.DeclineReason,
                Status = a.Status.ToString(),
            })
            .ToListAsync();

        Assert.Single(adminRows);
        Assert.Null(adminRows[0].DeclineReason);
        Assert.Equal("note only", adminRows[0].Note);
    }

    [Fact]
    public async Task Reject_stores_decline_reason_separate_from_note_and_admin_can_read_it()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        seed.Appointment!.Note = "โน้ตภายใน";
        await db.SaveChangesAsync();

        var appointments = new AppointmentAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<AppointmentAppService>.Instance);

        var rejected = await appointments.UpdateStatusAsync(
            seed.ProviderUser.Id,
            seed.Appointment.Id,
            AppointmentStatus.Rejected,
            "ไม่ว่างช่วงนี้",
            CancellationToken.None);

        Assert.Equal(AppointmentStatus.Rejected, rejected.Status);
        Assert.Equal("ไม่ว่างช่วงนี้", rejected.DeclineReason);
        Assert.Equal("โน้ตภายใน", rejected.Note);

        var adminRow = await db.Appointments.AsNoTracking()
            .Where(a => a.Id == rejected.Id)
            .Select(a => new { a.DeclineReason, a.Note, a.Status })
            .SingleAsync();

        Assert.Equal(AppointmentStatus.Rejected, adminRow.Status);
        Assert.Equal("ไม่ว่างช่วงนี้", adminRow.DeclineReason);
        Assert.Equal("โน้ตภายใน", adminRow.Note);
    }

    [Fact]
    public async Task R43_provider_who_rejected_appointment_no_longer_sees_job_as_open_and_cannot_bid()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var appointments = new AppointmentAppService(
            db, TestDbFactory.NoopNotifications(), NullLogger<AppointmentAppService>.Instance);
        await appointments.UpdateStatusAsync(
            seed.ProviderUser.Id, seed.Appointment!.Id, AppointmentStatus.Rejected, "ไม่ว่าง", CancellationToken.None);

        var sut = new ServiceRequestAppService(
            db, TestDbFactory.NoopNotifications(), NullLogger<ServiceRequestAppService>.Instance);
        var request = await db.ServiceRequests.AsNoTracking().SingleAsync(r => r.Id == seed.Request.Id);
        var inbox = await sut.ListMatchingForProviderAsync(
            seed.ProviderUser.Id, excludeSmokeTestData: false, includeOwnRequests: true, CancellationToken.None);
        Assert.DoesNotContain(inbox.Items, x => x.Id == seed.Request.Id);

        if (request.Status is ServiceRequestStatus.Open or ServiceRequestStatus.HasOffers)
        {
            var form = await sut.GetBidFormAsync(seed.ProviderUser.Id, seed.Request.Id, CancellationToken.None);
            Assert.False(form.Allowed);
            Assert.Equal("bid_closed", form.ReasonCode);
        }
    }
}
