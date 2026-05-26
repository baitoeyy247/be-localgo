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

        if (request.Latitude is not double lat || request.Longitude is not double lng)
        {
            throw new AppException("Location is required for booking.", 400);
        }

        var now = DateTime.UtcNow;
        var serviceRequest = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            RequesterUserId = userId,
            CategoryId = service.CategoryId,
            Title = service.Title,
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

        db.ServiceRequests.Add(serviceRequest);
        await db.SaveChangesAsync(cancellationToken);

        return await CreateAsync(userId, serviceRequest.Id, request, cancellationToken);
    }

    /// <summary>Creates or updates the appointment for a request with a selected provider.</summary>
    public async Task<Appointment> CreateAsync(
        Guid userId,
        Guid requestId,
        CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
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
        var serviceRequest = await db.ServiceRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new AppException("Request not found.", 404);

        await MarketplaceAccess.EnsureServiceRequestViewerAsync(db, userId, serviceRequest, cancellationToken);

        return await db.Appointments.AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.ServiceRequestId == requestId && a.Status != AppointmentStatus.Cancelled,
                cancellationToken);
    }

    /// <summary>Updates appointment status; request status follows appointment lifecycle.</summary>
    public async Task<Appointment> UpdateStatusAsync(
        Guid userId, Guid appointmentId, AppointmentStatus status, CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.Include(a => a.ServiceRequest)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, cancellationToken)
            ?? throw new AppException("Appointment not found.", 404);

        await MarketplaceAccess.EnsureAppointmentParticipantAsync(db, userId, appointment, cancellationToken);

        if (status == AppointmentStatus.Cancelled
            && appointment.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
        {
            throw new AppException("Cannot cancel appointment in the current status.", 409);
        }

        appointment.Status = status;
        appointment.UpdatedAt = DateTime.UtcNow;
        ApplyRequestStatusForAppointment(appointment, status);
        ProgressHelper.Touch(appointment.ServiceRequest);
        await db.SaveChangesAsync(cancellationToken);
        await NotifyOtherPartyAsync(
            userId,
            appointment.ServiceRequest,
            appointment,
            NotificationEvents.AppointmentUpdated,
            cancellationToken);
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
        if (existing.Status == AppointmentStatus.Cancelled)
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

    private static void ApplyRequestStatusForAppointment(Appointment appointment, AppointmentStatus status)
    {
        if (status == AppointmentStatus.InProgress)
        {
            appointment.ServiceRequest.Status = ServiceRequestStatus.InProgress;
        }
        else if (status == AppointmentStatus.Completed)
        {
            appointment.ServiceRequest.Status = ServiceRequestStatus.Completed;
        }
        else if (status == AppointmentStatus.Cancelled)
        {
            appointment.ServiceRequest.Status = ServiceRequestStatus.ProviderSelected;
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
