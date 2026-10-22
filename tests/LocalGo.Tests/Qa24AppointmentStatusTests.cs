using LocalGo.Application.Common;
using LocalGo.Application.Services;
using LocalGo.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalGo.Tests;

/// <summary>QA round 24 — appointment PATCH transitions and start window.</summary>
public sealed class Qa24AppointmentStatusTests
{
    [Fact]
    public async Task H1_in_progress_before_scheduled_time_is_rejected_with_thai_message()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        seed.Appointment!.Status = AppointmentStatus.Confirmed;
        seed.Appointment.ScheduledAt = DateTime.UtcNow.AddMinutes(24);
        seed.Request.Status = ServiceRequestStatus.Scheduled;
        await db.SaveChangesAsync();

        var sut = new AppointmentAppService(db, TestDbFactory.NoopNotifications(), NullLogger<AppointmentAppService>.Instance);
        var ex = await Assert.ThrowsAsync<AppException>(() => sut.UpdateStatusAsync(
            seed.ProviderUser.Id,
            seed.Appointment.Id,
            AppointmentStatus.InProgress,
            null,
            CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("เริ่มงานได้ตั้งแต่", ex.Message);
    }

    [Fact]
    public async Task H1_completed_before_in_progress_is_rejected()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        seed.Appointment!.Status = AppointmentStatus.Confirmed;
        seed.Appointment.ScheduledAt = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        var sut = new AppointmentAppService(db, TestDbFactory.NoopNotifications(), NullLogger<AppointmentAppService>.Instance);
        var ex = await Assert.ThrowsAsync<AppException>(() => sut.UpdateStatusAsync(
            seed.ProviderUser.Id,
            seed.Appointment.Id,
            AppointmentStatus.Completed,
            null,
            CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task H1_requester_cannot_confirm_pending()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var sut = new AppointmentAppService(db, TestDbFactory.NoopNotifications(), NullLogger<AppointmentAppService>.Instance);

        var ex = await Assert.ThrowsAsync<AppException>(() => sut.UpdateStatusAsync(
            seed.Requester.Id,
            seed.Appointment!.Id,
            AppointmentStatus.Confirmed,
            null,
            CancellationToken.None));

        Assert.Equal(403, ex.StatusCode);
    }
}
