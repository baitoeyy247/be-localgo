using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LocalGo.Application.Services;

public sealed class BidAppService(ILocalGoDbContext db, INotificationPublisher notifications, ILogger<BidAppService> logger)
{
    /// <summary>Submits or re-submits a provider bid on an open marketplace request.</summary>
    public async Task<Bid> SubmitAsync(Guid userId, Guid requestId, SubmitBidRequest request, CancellationToken cancellationToken)
    {
        var provider = await MarketplaceAccess.RequireProviderForUserAsync(db, userId, cancellationToken);

        var serviceRequest = await db.ServiceRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new AppException("Request not found.", 404);

        if (serviceRequest.Status is not ServiceRequestStatus.Open and not ServiceRequestStatus.HasOffers)
        {
            throw new AppException("Request is not open for bids.", 409);
        }

        var now = DateTime.UtcNow;
        var bid = await ResolveBidForSubmitAsync(provider.Id, requestId, request, now, cancellationToken);

        MarkRequestAfterBid(serviceRequest, now);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateBidForRequest(ex))
        {
            // Stale read or race: unique (request, provider) — reload and update instead of insert.
            db.ClearChangeTracker();
            serviceRequest = await db.ServiceRequests.FirstAsync(r => r.Id == requestId, cancellationToken);
            bid = await ResolveBidForSubmitAsync(provider.Id, requestId, request, now, cancellationToken);
            MarkRequestAfterBid(serviceRequest, now);
            await db.SaveChangesAsync(cancellationToken);
        }

        var requester = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == serviceRequest.RequesterUserId, cancellationToken);
        if (requester is not null)
        {
            var categoryName = await db.ServiceCategories.AsNoTracking()
                .Where(c => c.Id == serviceRequest.CategoryId)
                .Select(c => c.Name)
                .FirstAsync(cancellationToken);

            await NotificationEnqueue.TryExcludingActorAsync(
                logger,
                notifications,
                NotificationEvents.NewBidReceived,
                requester.Id,
                userId,
                requester.LineUserId,
                new
                {
                    requestId,
                    bidId = bid.Id,
                    title = serviceRequest.Title,
                    categoryId = serviceRequest.CategoryId,
                    categoryName,
                    providerName = provider.Name,
                    priceText = bid.PriceText,
                },
                cancellationToken,
                "Bid submitted but notification enqueue failed. RequestId={RequestId} BidId={BidId} RequesterId={RequesterId}",
                requestId,
                bid.Id,
                requester.Id);
        }

        return bid;
    }

    /// <summary>Lists non-withdrawn bids on a request; requester and admin see all active provider bids.</summary>
    public async Task<IReadOnlyList<BidDto>> ListForRequestAsync(
        Guid viewerUserId, Guid requestId, bool isAdmin, CancellationToken cancellationToken)
    {
        var request = await db.ServiceRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new AppException("Request not found.", 404);

        IQueryable<Bid> query = db.Bids.AsNoTracking().Include(b => b.Provider)
            .Where(b => b.ServiceRequestId == requestId && b.Status != BidStatus.Withdrawn);

        if (!isAdmin && request.RequesterUserId != viewerUserId)
        {
            var provider = await db.Providers.AsNoTracking()
                .FirstOrDefaultAsync(p => p.OwnerUserId == viewerUserId, cancellationToken);
            if (provider is null)
            {
                throw new AppException("Forbidden.", 403);
            }

            query = query.Where(b => b.ProviderId == provider.Id);
        }

        return await query
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BidDto(b.Id, b.ProviderId, b.Provider.Name, b.PriceText, b.Description, b.AvailableAt, b.Status.ToString()))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Selects one submitted bid and rejects other submitted bids on the same request.</summary>
    public async Task SelectAsync(Guid userId, Guid requestId, SelectBidRequest request, CancellationToken cancellationToken)
    {
        var serviceRequest = await MarketplaceAccess.RequireOwnedRequestWithBidsAsync(db, userId, requestId, cancellationToken);

        var selected = serviceRequest.Bids.FirstOrDefault(b => b.Id == request.BidId)
            ?? throw new AppException("Bid not found.", 404);

        if (selected.Status == BidStatus.Selected)
        {
            if (serviceRequest.SelectedBidId != selected.Id)
            {
                throw new AppException("Bid is not active for this request.", 409);
            }

            if (serviceRequest.SelectedProviderId is null)
            {
                ApplyBidSelection(serviceRequest, selected);
                await db.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        if (selected.Status != BidStatus.Submitted)
        {
            throw new AppException("Only submitted bids can be selected.", 409);
        }

        var now = DateTime.UtcNow;
        foreach (var bid in serviceRequest.Bids.Where(b => b.Status == BidStatus.Submitted))
        {
            bid.Status = bid.Id == selected.Id ? BidStatus.Selected : BidStatus.Rejected;
            bid.UpdatedAt = now;
        }

        ApplyBidSelection(serviceRequest, selected);
        await db.SaveChangesAsync(cancellationToken);

        var provider = await db.Providers.AsNoTracking().Include(p => p.OwnerUser)
            .FirstOrDefaultAsync(p => p.Id == selected.ProviderId, cancellationToken);
        if (provider?.OwnerUser is not null)
        {
            var categoryName = await db.ServiceCategories.AsNoTracking()
                .Where(c => c.Id == serviceRequest.CategoryId)
                .Select(c => c.Name)
                .FirstAsync(cancellationToken);

            await NotificationEnqueue.TryExcludingActorAsync(
                logger,
                notifications,
                NotificationEvents.BidSelected,
                provider.OwnerUser.Id,
                userId,
                provider.OwnerUser.LineUserId,
                new
                {
                    requestId,
                    title = serviceRequest.Title,
                    categoryId = serviceRequest.CategoryId,
                    categoryName,
                },
                cancellationToken,
                "Bid selected but notification enqueue failed. RequestId={RequestId} BidId={BidId}",
                requestId,
                selected.Id);
        }
    }

    /// <summary>Updates a submitted bid owned by the current provider.</summary>
    public async Task<Bid> UpdateAsync(Guid userId, Guid bidId, SubmitBidRequest request, CancellationToken cancellationToken)
    {
        var provider = await MarketplaceAccess.RequireProviderForUserAsync(db, userId, cancellationToken);
        var bid = await db.Bids.FirstOrDefaultAsync(b => b.Id == bidId && b.ProviderId == provider.Id, cancellationToken)
            ?? throw new AppException("Bid not found.", 404);
        if (bid.Status != BidStatus.Submitted)
        {
            throw new AppException("Cannot update bid after selection.", 409);
        }

        ApplyEditableBidFields(bid, request, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return bid;
    }

    /// <summary>Clears marketplace bid selection and reopens the request for offers.</summary>
    public async Task ClearSelectionAsync(Guid userId, Guid requestId, CancellationToken cancellationToken)
    {
        var serviceRequest = await MarketplaceAccess.RequireOwnedRequestWithBidsAsync(db, userId, requestId, cancellationToken);
        ValidateClearSelection(serviceRequest);

        var appointment = await db.Appointments
            .FirstOrDefaultAsync(a => a.ServiceRequestId == requestId, cancellationToken);
        CancelAppointmentForClearSelection(appointment);

        RevertBidSelection(serviceRequest);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Withdraws a submitted bid owned by the current provider.</summary>
    public async Task WithdrawAsync(Guid userId, Guid bidId, CancellationToken cancellationToken)
    {
        var provider = await MarketplaceAccess.RequireProviderForUserAsync(db, userId, cancellationToken);
        var bid = await db.Bids.FirstOrDefaultAsync(b => b.Id == bidId && b.ProviderId == provider.Id, cancellationToken)
            ?? throw new AppException("Bid not found.", 404);
        if (bid.Status == BidStatus.Withdrawn)
        {
            return;
        }

        if (bid.Status != BidStatus.Submitted)
        {
            throw new AppException(
                $"Cannot withdraw bid with status '{bid.Status}'. Only submitted bids can be withdrawn.",
                409);
        }

        bid.Status = BidStatus.Withdrawn;
        bid.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Bid> ResolveBidForSubmitAsync(
        Guid providerId,
        Guid requestId,
        SubmitBidRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = await db.Bids.FirstOrDefaultAsync(
            b => b.ServiceRequestId == requestId && b.ProviderId == providerId,
            cancellationToken);

        if (existing is not null)
        {
            ApplySubmitToExistingBid(existing, request, now);
            return existing;
        }

        var bid = new Bid
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ProviderId = providerId,
            Amount = request.Amount,
            Currency = request.Currency ?? "THB",
            PriceText = request.PriceText.Trim(),
            Description = request.Description,
            AvailableAt = request.AvailableAt,
            Status = BidStatus.Submitted,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Bids.Add(bid);
        return bid;
    }

    private static void MarkRequestAfterBid(ServiceRequest serviceRequest, DateTime now)
    {
        if (serviceRequest.FirstBidAt is null)
        {
            serviceRequest.FirstBidAt = now;
        }

        if (serviceRequest.Status == ServiceRequestStatus.Open)
        {
            serviceRequest.Status = ServiceRequestStatus.HasOffers;
        }

        ProgressHelper.Touch(serviceRequest);
    }

    private static void ApplyBidSelection(ServiceRequest serviceRequest, Bid selected)
    {
        serviceRequest.SelectedBidId = selected.Id;
        serviceRequest.SelectedProviderId = selected.ProviderId;
        serviceRequest.Status = ServiceRequestStatus.ProviderSelected;
        ProgressHelper.Touch(serviceRequest);
    }

    private static void ApplyEditableBidFields(Bid bid, SubmitBidRequest request, DateTime now)
    {
        bid.PriceText = request.PriceText.Trim();
        if (request.Amount.HasValue)
        {
            bid.Amount = request.Amount;
        }

        if (request.Description is not null)
        {
            bid.Description = request.Description;
        }

        if (request.AvailableAt.HasValue)
        {
            bid.AvailableAt = request.AvailableAt;
        }

        bid.UpdatedAt = now;
    }

    private static void ApplySubmitToExistingBid(Bid existing, SubmitBidRequest request, DateTime now)
    {
        switch (existing.Status)
        {
            case BidStatus.Submitted:
                throw new AppException("คุณส่งข้อเสนอราคางานนี้แล้ว — ใช้ปุ่มอัปเดตข้อเสนอแทน", 409);
            case BidStatus.Withdrawn:
                ApplyEditableBidFields(existing, request, now);
                if (!string.IsNullOrWhiteSpace(request.Currency))
                {
                    existing.Currency = request.Currency;
                }

                existing.Status = BidStatus.Submitted;
                return;
            case BidStatus.Rejected:
                throw new AppException("งานนี้ปฏิเสธข้อเสนอของคุณแล้ว ไม่สามารถส่งใหม่ได้", 409);
            case BidStatus.Selected:
                throw new AppException("ข้อเสนอของคุณถูกเลือกแล้ว ไม่สามารถส่งใหม่ได้", 409);
            default:
                throw new AppException("ไม่สามารถส่งข้อเสนอในสถานะปัจจุบันได้", 409);
        }
    }

    private static void ValidateClearSelection(ServiceRequest serviceRequest)
    {
        if (serviceRequest.Source != ServiceRequestSource.Marketplace)
        {
            throw new AppException("Cannot clear bid selection for this request type.", 409);
        }

        if (serviceRequest.Status is not ServiceRequestStatus.ProviderSelected
            and not ServiceRequestStatus.Scheduled)
        {
            throw new AppException("Cannot clear selection in the current request status.", 409);
        }

        if (serviceRequest.SelectedBidId is null)
        {
            throw new AppException("No bid is selected for this request.", 409);
        }
    }

    private static void CancelAppointmentForClearSelection(Appointment? appointment)
    {
        if (appointment is null)
        {
            return;
        }

        if (appointment.Status is AppointmentStatus.InProgress or AppointmentStatus.Completed)
        {
            throw new AppException("Cannot clear selection while the appointment is in progress or completed.", 409);
        }

        if (appointment.Status == AppointmentStatus.Confirmed)
        {
            throw new AppException("ยกเลิกการเลือกไม่ได้เมื่อผู้ให้บริการยืนยันนัดแล้ว — กรุณายกเลิกนัดก่อน", 409);
        }

        if (appointment.Status == AppointmentStatus.Pending)
        {
            appointment.Status = AppointmentStatus.Cancelled;
            appointment.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static void RevertBidSelection(ServiceRequest serviceRequest)
    {
        var now = DateTime.UtcNow;
        foreach (var bid in serviceRequest.Bids)
        {
            if (bid.Status is BidStatus.Selected or BidStatus.Rejected)
            {
                bid.Status = BidStatus.Submitted;
                bid.UpdatedAt = now;
            }
        }

        serviceRequest.SelectedBidId = null;
        serviceRequest.SelectedProviderId = null;

        var submittedCount = serviceRequest.Bids.Count(b => b.Status == BidStatus.Submitted);
        serviceRequest.Status = submittedCount > 0
            ? ServiceRequestStatus.HasOffers
            : ServiceRequestStatus.Open;

        ProgressHelper.Touch(serviceRequest);
    }

    private static bool IsDuplicateBidForRequest(DbUpdateException ex)
    {
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            var message = inner.Message;
            if (message.Contains("IX_bids_ServiceRequestId_ProviderId", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || message.Contains("23505", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
