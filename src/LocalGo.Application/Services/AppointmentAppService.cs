using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LocalGo.Application.Services;

public sealed class AppointmentAppService(
    ILocalGoDbContext db,
    INotificationPublisher notifications,
    ILogger<AppointmentAppService> logger)
{
    /// <summary>Books an appointment from a provider catalog service (direct booking flow).</summary>
    public async Task<Appointment> BookFromProviderServiceAsync(
        Guid userId,
        Guid providerId,
        Guid providerServiceId,
        CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var service = await db.ProviderServices.AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == providerServiceId && s.ProviderId == providerId && s.IsActive,
                cancellationToken)
            ?? throw new AppException("Service not found.", 404);

        var provider = await db.Providers.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken)
            ?? throw new AppException("Provider not found.", 404);
        if (provider.OwnerUserId == userId)
        {
            throw new AppException("ไม่สามารถจองบริการของตัวเองได้ กรุณาเลือกผู้ให้บริการรายอื่น", 400);
        }

        if (request.Latitude is not double lat || request.Longitude is not double lng)
        {
            throw new AppException("Location is required for booking.", 400);
        }

        var now = DateTime.UtcNow;
        var whenLabel = request.ScheduledAt is DateTime at
            ? FormatThaiDateTime(at)
            : "รอยืนยันเวลา";
        var serviceRequest = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            RequesterUserId = userId,
            CategoryId = service.CategoryId,
            Title = $"{service.Title} · {whenLabel}",
            Description = request.Note ?? service.Description,
            AddressText = request.AddressText,
            Latitude = lat,
            Longitude = lng,
            Location = GeoHelper.CreatePoint(lat, lng),
            SearchRadiusMeters = 5000,
            BudgetText = service.BasePriceText,
            Status = ServiceRequestStatus.ProviderSelected,
            Source = ServiceRequestSource.DirectBooking,
            ProviderServiceId = service.Id,
            SelectedProviderId = providerId,
            LastProgressAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = serviceRequest.Id,
            ProviderId = providerId,
            RequesterUserId = userId,
            ScheduledAt = request.ScheduledAt,
            AddressText = request.AddressText,
            Note = request.Note,
            Status = AppointmentStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };

        TryApplyLocation(appointment, request);
        MarkRequestScheduled(serviceRequest);

        if (db is not DbContext efContext)
        {
            throw new InvalidOperationException("Database context must be EF DbContext for transactions.");
        }

        db.ServiceRequests.Add(serviceRequest);
        db.Appointments.Add(appointment);

        var useTransaction = efContext.Database.ProviderName is { } providerName
            && !providerName.Contains("InMemory", StringComparison.Ordinal);

        if (useTransaction)
        {
            await using var transaction = await efContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ClearChangeTracker();
                throw;
            }
        }
        else
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        await NotifyOtherPartyAsync(
            userId, serviceRequest, appointment, NotificationEvents.AppointmentCreated, cancellationToken);
        return appointment;
    }

    /// <summary>Creates or updates the appointment for a request with a selected provider.</summary>
    public async Task<Appointment> CreateAsync(
        Guid userId,
        Guid requestId,
        CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        FieldRules.MaxLength(request.AddressText, 500, "สถานที่");
        FieldRules.MaxLength(request.Note, 2000, "หมายเหตุ");
        var serviceRequest = await db.ServiceRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new AppException("Request not found.", 404);

        if (serviceRequest.RequesterUserId != userId)
        {
            throw new AppException("Only requester can create appointment.", 403);
        }

        if (serviceRequest.SelectedProviderId is null)
        {
            throw new AppException("Select a provider before creating appointment.", 409);
        }

        if (serviceRequest.Status != ServiceRequestStatus.ProviderSelected
            && serviceRequest.Status != ServiceRequestStatus.Scheduled)
        {
            throw new AppException("Cannot create appointment in the current request status.", 409);
        }

        var now = DateTime.UtcNow;
        var existing = await db.Appointments
            .FirstOrDefaultAsync(a => a.ServiceRequestId == requestId, cancellationToken);

        if (existing is not null)
        {
            return await UpsertExistingAppointmentAsync(
                userId, serviceRequest, existing, request, now, cancellationToken);
        }

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ProviderId = serviceRequest.SelectedProviderId!.Value,
            RequesterUserId = userId,
            ScheduledAt = request.ScheduledAt,
            AddressText = request.AddressText,
            Note = request.Note,
            Status = AppointmentStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };

        TryApplyLocation(appointment, request);
        MarkRequestScheduled(serviceRequest);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(cancellationToken);

        await NotifyOtherPartyAsync(
            userId, serviceRequest, appointment, NotificationEvents.AppointmentCreated, cancellationToken);
        return appointment;
    }

    /// <summary>Returns the active (non-cancelled) appointment for a request if the viewer may see it.</summary>
    public async Task<Appointment?> GetByRequestIdAsync(
        Guid userId, Guid requestId, CancellationToken cancellationToken)
    {
        var ctx = (DbContext)(object)db;
        var header = await MarketplaceCompiledQueries.GetRequestAccessHeaderAsync(
            ctx, requestId, cancellationToken)
            ?? throw new AppException("Request not found.", 404);

        await MarketplaceAccess.EnsureServiceRequestViewerAsync(
            db,
            userId,
            header.Id,
            header.RequesterUserId,
            header.SelectedProviderId,
            cancellationToken);

        return await MarketplaceCompiledQueries.GetAppointmentByRequestIdAsync(
            ctx, requestId, cancellationToken);
    }

    /// <summary>Loads an appointment by id for a participant (requester or provider owner).</summary>
    public async Task<Appointment> GetByIdAsync(Guid userId, Guid appointmentId, CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.AsNoTracking()
            .Include(a => a.Provider)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, cancellationToken)
            ?? throw new AppException("ไม่พบหรือไม่มีสิทธิ์เข้าถึง", 404);

        try
        {
            await MarketplaceAccess.EnsureAppointmentParticipantAsync(db, userId, appointment, cancellationToken);
        }
        catch (AppException)
        {
            throw new AppException("ไม่พบหรือไม่มีสิทธิ์เข้าถึง", 404);
        }

        return appointment;
    }

    /// <summary>Updates appointment status; request status follows appointment lifecycle in the same SaveChanges.</summary>
    public async Task<Appointment> UpdateStatusAsync(
        Guid userId,
        Guid appointmentId,
        AppointmentStatus status,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var appointment = await db.Appointments.Include(a => a.ServiceRequest)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, cancellationToken)
            ?? throw new AppException("Appointment not found.", 404);

        await MarketplaceAccess.EnsureAppointmentParticipantAsync(db, userId, appointment, cancellationToken);

        if (appointment.Status == status)
        {
            return appointment;
        }

        var isProvider = await db.Providers.AsNoTracking().AnyAsync(
            p => p.OwnerUserId == userId && p.Id == appointment.ProviderId,
            cancellationToken);
        EnsureTransition(appointment.Status, status, isProvider);
        if (status is AppointmentStatus.InProgress or AppointmentStatus.Completed)
        {
            AppointmentStartWindow.EnsureCanStartOrComplete(appointment.ScheduledAt);
        }

        if (status == AppointmentStatus.Rejected)
        {
            appointment.DeclineReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        }

        appointment.Status = status;
        appointment.UpdatedAt = DateTime.UtcNow;
        await ApplyRequestStatusForAppointmentAsync(appointment, status, cancellationToken);
        ProgressHelper.Touch(appointment.ServiceRequest);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await NotifyOtherPartyAsync(
                userId,
                appointment.ServiceRequest,
                appointment,
                NotificationEvents.AppointmentUpdated,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Appointment {AppointmentId} status saved as {Status} but notification failed", appointmentId, status);
        }

        return appointment;
    }

    private async Task<Appointment> UpsertExistingAppointmentAsync(
        Guid actorUserId,
        ServiceRequest serviceRequest,
        Appointment existing,
        CreateAppointmentRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (existing.Status is AppointmentStatus.Cancelled or AppointmentStatus.Rejected)
        {
            ApplyAppointmentFields(existing, request, merge: false);
            existing.Status = AppointmentStatus.Pending;
            existing.UpdatedAt = now;
            MarkRequestScheduled(serviceRequest);
            await db.SaveChangesAsync(cancellationToken);
            await NotifyOtherPartyAsync(
                actorUserId, serviceRequest, existing, NotificationEvents.AppointmentCreated, cancellationToken);
            return existing;
        }

        if (existing.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
        {
            throw new AppException("Appointment is already in progress or completed.", 409);
        }

        ApplyAppointmentFields(existing, request, merge: true);
        existing.UpdatedAt = now;
        MarkRequestScheduled(serviceRequest);
        await db.SaveChangesAsync(cancellationToken);
        await NotifyOtherPartyAsync(
            actorUserId, serviceRequest, existing, NotificationEvents.AppointmentUpdated, cancellationToken);
        return existing;
    }

    private static void MarkRequestScheduled(ServiceRequest serviceRequest)
    {
        serviceRequest.Status = ServiceRequestStatus.Scheduled;
        ProgressHelper.Touch(serviceRequest);
    }

    private static void ApplyAppointmentFields(Appointment appointment, CreateAppointmentRequest request, bool merge)
    {
        if (merge)
        {
            appointment.ScheduledAt = request.ScheduledAt ?? appointment.ScheduledAt;
            appointment.AddressText = string.IsNullOrWhiteSpace(request.AddressText)
                ? appointment.AddressText
                : request.AddressText;
            appointment.Note = string.IsNullOrWhiteSpace(request.Note) ? appointment.Note : request.Note;
        }
        else
        {
            appointment.ScheduledAt = request.ScheduledAt;
            appointment.AddressText = request.AddressText;
            appointment.Note = request.Note;
        }

        TryApplyLocation(appointment, request);
    }

    private static void TryApplyLocation(Appointment appointment, CreateAppointmentRequest request)
    {
        if (request.Latitude is double lat && request.Longitude is double lng)
        {
            appointment.Location = GeoHelper.CreatePoint(lat, lng);
        }
    }

    private static string FormatThaiDateTime(DateTime utcOrUnspecified)
    {
        var utc = utcOrUnspecified.Kind == DateTimeKind.Utc
            ? utcOrUnspecified
            : DateTime.SpecifyKind(utcOrUnspecified, DateTimeKind.Utc);
        return utc.AddHours(7).ToString("d MMM yyyy HH:mm", new System.Globalization.CultureInfo("th-TH"));
    }

    /// <summary>
    /// Confirmed → in progress → completed is provider-only.
    /// Either participant may cancel before the job starts.
    /// Provider may reject while pending.
    /// </summary>
    private static void EnsureTransition(AppointmentStatus current, AppointmentStatus next, bool isProvider)
    {
        switch (current, next)
        {
            case (AppointmentStatus.Pending, AppointmentStatus.Confirmed):
            case (AppointmentStatus.Confirmed, AppointmentStatus.InProgress):
            case (AppointmentStatus.InProgress, AppointmentStatus.Completed):
                if (!isProvider)
                {
                    throw new AppException("เฉพาะผู้ให้บริการที่เดินงานนี้ต่อได้", 403);
                }

                return;
            case (AppointmentStatus.Pending, AppointmentStatus.Rejected):
                if (!isProvider)
                {
                    throw new AppException("เฉพาะผู้ให้บริการที่ปฏิเสธนัดได้", 403);
                }

                return;
            case (AppointmentStatus.Pending, AppointmentStatus.Cancelled):
            case (AppointmentStatus.Confirmed, AppointmentStatus.Cancelled):
                return;
            case (_, AppointmentStatus.Cancelled):
                throw new AppException(GlossaryMessages.CancelAppointmentInvalidStatus, 409);
            default:
                throw new AppException("ไม่สามารถเปลี่ยนสถานะนัดหมายในขั้นตอนนี้", 409);
        }
    }

    private async Task ApplyRequestStatusForAppointmentAsync(
        Appointment appointment,
        AppointmentStatus status,
        CancellationToken cancellationToken)
    {
        var request = appointment.ServiceRequest;
        if (status == AppointmentStatus.InProgress)
        {
            request.Status = ServiceRequestStatus.InProgress;
            return;
        }

        if (status == AppointmentStatus.Completed)
        {
            request.Status = ServiceRequestStatus.Completed;
            return;
        }

        if (status == AppointmentStatus.Cancelled)
        {
            // Same transaction as appointment.Status = Cancelled (caller SaveChanges).
            request.Status = ServiceRequestStatus.Cancelled;
            return;
        }

        if (status != AppointmentStatus.Rejected)
        {
            return;
        }

        var requestId = request.Id;
        var now = DateTime.UtcNow;
        var selectedBids = await db.Bids
            .Where(b => b.ServiceRequestId == requestId && b.Status == BidStatus.Selected)
            .ToListAsync(cancellationToken);
        foreach (var bid in selectedBids)
        {
            bid.Status = BidStatus.Rejected;
            bid.UpdatedAt = now;
        }

        request.SelectedProviderId = null;
        request.SelectedBidId = null;

        if (request.Source == ServiceRequestSource.DirectBooking)
        {
            request.Status = ServiceRequestStatus.Cancelled;
        }
        else
        {
            var openBids = await db.Bids.AsNoTracking().CountAsync(
                b => b.ServiceRequestId == requestId && b.Status == BidStatus.Submitted,
                cancellationToken);
            request.Status = openBids > 0 ? ServiceRequestStatus.HasOffers : ServiceRequestStatus.Open;
        }
    }

    private async Task NotifyOtherPartyAsync(
        Guid actorUserId,
        ServiceRequest request,
        Appointment appointment,
        string eventType,
        CancellationToken cancellationToken)
    {
        try
        {
            var requester = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == request.RequesterUserId, cancellationToken);
            var provider = await db.Providers.AsNoTracking().Include(p => p.OwnerUser)
                .FirstOrDefaultAsync(p => p.Id == appointment.ProviderId, cancellationToken);

            var categoryName = await db.ServiceCategories.AsNoTracking()
                .Where(c => c.Id == request.CategoryId)
                .Select(c => c.Name)
                .FirstAsync(cancellationToken);

            var payload = new
            {
                requestId = request.Id,
                appointmentId = appointment.Id,
                title = request.Title,
                categoryId = request.CategoryId,
                categoryName,
                status = appointment.Status.ToString(),
                scheduledAt = appointment.ScheduledAt,
                declineReason = appointment.DeclineReason,
            };

            if (requester is not null
                && NotificationRecipients.ShouldNotify(requester.Id, actorUserId))
            {
                await notifications.EnqueueAsync(
                    eventType,
                    requester.Id,
                    requester.LineUserId,
                    payload,
                    cancellationToken);
            }

            if (provider?.OwnerUser is not null
                && NotificationRecipients.ShouldNotify(provider.OwnerUser.Id, actorUserId))
            {
                await notifications.EnqueueAsync(
                    eventType,
                    provider.OwnerUser.Id,
                    provider.OwnerUser.LineUserId,
                    payload,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Appointment saved but notification enqueue failed. RequestId={RequestId} AppointmentId={AppointmentId} EventType={EventType} ActorUserId={ActorUserId}",
                request.Id,
                appointment.Id,
                eventType,
                actorUserId);
        }
    }
}
