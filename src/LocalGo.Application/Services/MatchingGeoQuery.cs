using LocalGo.Application.Common;
using LocalGo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Application.Services;

/// <summary>PostGIS-backed coarse filter for provider matching inbox (QA28 L-5).</summary>
internal static class MatchingGeoQuery
{
    internal readonly record struct BranchRef(
        Guid Id,
        double Latitude,
        double Longitude,
        int ServiceRadiusMeters);

    internal readonly record struct LinkRef(Guid CategoryId, BranchRef Branch);

    internal static bool SupportsDistanceFilter(DbContext ctx) =>
        ctx.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

    internal static async Task<HashSet<Guid>> FilterRequestIdsWithinCoverageAsync(
        DbContext ctx,
        IQueryable<ServiceRequest> baseQuery,
        IReadOnlyList<LinkRef> coverageLinks,
        CancellationToken cancellationToken)
    {
        var distinct = coverageLinks
            .DistinctBy(l => (l.CategoryId, l.Branch.Id))
            .ToList();

        IQueryable<Guid>? union = null;
        foreach (var link in distinct)
        {
            var branchPoint = GeoHelper.CreatePoint(link.Branch.Latitude, link.Branch.Longitude);
            var radius = (double)link.Branch.ServiceRadiusMeters;
            var categoryId = link.CategoryId;
            var part = baseQuery
                .Where(r => r.CategoryId == categoryId
                    && r.Location.Distance(branchPoint) <= radius)
                .Select(r => r.Id);
            union = union is null ? part : union.Union(part);
        }

        if (union is null)
        {
            return [];
        }

        var ids = await union.ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }
}
