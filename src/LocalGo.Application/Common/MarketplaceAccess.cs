using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Application.Common;

/// <summary>Shared authorization and lookup helpers for marketplace flows.</summary>
internal static class MarketplaceAccess
{
    public static async Task<ServiceRequest> RequireOwnedRequestAsync(
        ILocalGoDbContext db,
        Guid userId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        return await db.ServiceRequests.FirstOrDefaultAsync(
                r => r.Id == requestId && r.RequesterUserId == userId,
                cancellationToken)
            ?? throw new AppException("Request not found.", 404);
    }

    public static async Task<ServiceRequest> RequireOwnedRequestWithBidsAsync(
        ILocalGoDbContext db,
        Guid userId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        return await db.ServiceRequests
                .Include(r => r.Bids)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.RequesterUserId == userId, cancellationToken)
            ?? throw new AppException("Request not found.", 404);
    }

    public static async Task<Provider> RequireProviderForUserAsync(
        ILocalGoDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await db.Providers.FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken)
            ?? throw new AppException("Provider profile required.", 404);
    }

    public static async Task EnsureServiceRequestParticipantAsync(
        ILocalGoDbContext db,
        Guid userId,
        ServiceRequest request,
        CancellationToken cancellationToken)
    {
        if (request.RequesterUserId == userId)
        {
            return;
        }

        var provider = await db.Providers.AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.OwnerUserId == userId && p.Id == request.SelectedProviderId,
                cancellationToken);
        if (provider is null)
        {
            throw new AppException("Forbidden.", 403);
        }
    }

    public static async Task EnsureServiceRequestViewerAsync(
        ILocalGoDbContext db,
        Guid userId,
        ServiceRequest serviceRequest,
        CancellationToken cancellationToken)
    {
        if (serviceRequest.RequesterUserId == userId)
        {
            return;
        }

        var provider = await db.Providers.AsNoTracking()
            .FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (provider is null)
        {
            throw new AppException("Forbidden.", 403);
        }

        if (serviceRequest.SelectedProviderId == provider.Id)
        {
            return;
        }

        var hasSelectedBid = await db.Bids.AsNoTracking().AnyAsync(
            b => b.ServiceRequestId == serviceRequest.Id
                && b.ProviderId == provider.Id
                && b.Status == BidStatus.Selected,
            cancellationToken);
        if (hasSelectedBid)
        {
            return;
        }

        var hasAppointment = await db.Appointments.AsNoTracking().AnyAsync(
            a => a.ServiceRequestId == serviceRequest.Id
                && a.ProviderId == provider.Id
                && a.Status != AppointmentStatus.Cancelled,
            cancellationToken);
        if (hasAppointment)
        {
            return;
        }

        throw new AppException("Forbidden.", 403);
    }

    public static async Task EnsureAppointmentParticipantAsync(
        ILocalGoDbContext db,
        Guid userId,
        Appointment appointment,
        CancellationToken cancellationToken)
    {
        if (appointment.RequesterUserId == userId)
        {
            return;
        }

        var provider = await db.Providers.AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.OwnerUserId == userId && p.Id == appointment.ProviderId,
                cancellationToken);
        if (provider is null)
        {
            throw new AppException("Forbidden.", 403);
        }
    }
}
