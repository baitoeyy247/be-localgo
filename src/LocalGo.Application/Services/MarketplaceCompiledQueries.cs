using LocalGo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Application.Services;

/// <summary>EF compiled read queries for hot marketplace paths (QA28 L-5).</summary>
internal static class MarketplaceCompiledQueries
{
    internal sealed record RequestAccessHeader(
        Guid Id,
        Guid RequesterUserId,
        Domain.Enums.ServiceRequestStatus Status,
        Guid? SelectedProviderId);

    private static readonly Func<DbContext, Guid, IAsyncEnumerable<RequestAccessHeader>> RequestAccessHeaderQuery =
        EF.CompileAsyncQuery((DbContext ctx, Guid id) =>
            ctx.Set<ServiceRequest>().AsNoTracking()
                .Where(r => r.Id == id)
                .Select(r => new RequestAccessHeader(
                    r.Id,
                    r.RequesterUserId,
                    r.Status,
                    r.SelectedProviderId)));

    private static readonly Func<DbContext, Guid, IAsyncEnumerable<ServiceRequest>> RequestDetailAllBidsQuery =
        EF.CompileAsyncQuery((DbContext ctx, Guid id) =>
            ctx.Set<ServiceRequest>().AsNoTracking().AsSplitQuery()
                .Include(r => r.Category)
                .Include(r => r.Bids).ThenInclude(b => b.Provider)
                .Where(r => r.Id == id));

    private static readonly Func<DbContext, Guid, Guid, IAsyncEnumerable<ServiceRequest>> RequestDetailProviderBidsQuery =
        EF.CompileAsyncQuery((DbContext ctx, Guid id, Guid providerId) =>
            ctx.Set<ServiceRequest>().AsNoTracking().AsSplitQuery()
                .Include(r => r.Category)
                .Include(r => r.Bids.Where(b => b.ProviderId == providerId)).ThenInclude(b => b.Provider)
                .Where(r => r.Id == id));

    private static readonly Func<DbContext, Guid, IAsyncEnumerable<Appointment>> AppointmentByRequestIdQuery =
        EF.CompileAsyncQuery((DbContext ctx, Guid requestId) =>
            ctx.Set<Appointment>().AsNoTracking()
                .Include(a => a.Provider)
                .Where(a => a.ServiceRequestId == requestId));

    internal static async Task<RequestAccessHeader?> GetRequestAccessHeaderAsync(
        DbContext ctx, Guid requestId, CancellationToken cancellationToken) =>
        await RequestAccessHeaderQuery(ctx, requestId).FirstOrDefaultAsync(cancellationToken);

    internal static async Task<ServiceRequest?> GetRequestDetailAllBidsAsync(
        DbContext ctx, Guid requestId, CancellationToken cancellationToken) =>
        await RequestDetailAllBidsQuery(ctx, requestId).FirstOrDefaultAsync(cancellationToken);

    internal static async Task<ServiceRequest?> GetRequestDetailProviderBidsAsync(
        DbContext ctx, Guid requestId, Guid providerId, CancellationToken cancellationToken) =>
        await RequestDetailProviderBidsQuery(ctx, requestId, providerId).FirstOrDefaultAsync(cancellationToken);

    internal static async Task<Appointment?> GetAppointmentByRequestIdAsync(
        DbContext ctx, Guid requestId, CancellationToken cancellationToken) =>
        await AppointmentByRequestIdQuery(ctx, requestId).FirstOrDefaultAsync(cancellationToken);
}
