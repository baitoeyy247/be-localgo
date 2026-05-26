using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Application.Services;

public sealed class SearchAppService(ILocalGoDbContext db)
{
    public async Task<IReadOnlyList<ProviderSummaryDto>> SearchProvidersAsync(
        double lat,
        double lng,
        int radiusMeters,
        Guid? categoryId,
        string? keyword,
        decimal? minRating,
        CancellationToken cancellationToken)
    {
        var point = GeoHelper.CreatePoint(lat, lng);
        var services = await (
            from service in db.ProviderServices.AsNoTracking()
            join provider in db.Providers.AsNoTracking() on service.ProviderId equals provider.Id
            join category in db.ServiceCategories.AsNoTracking() on service.CategoryId equals category.Id
            where provider.Status == ProviderStatus.Active
                  && service.IsActive
            select new
            {
                ServiceId = service.Id,
                service.CategoryId,
                service.Title,
                service.Description,
                Provider = provider,
                Category = category,
            }
        ).ToListAsync(cancellationToken);

        if (categoryId.HasValue)
        {
            services = services.Where(x => x.CategoryId == categoryId.Value).ToList();
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim().ToLower();
            services = services.Where(x =>
                x.Provider.Name.ToLower().Contains(k)
                || x.Title.ToLower().Contains(k)
                || (x.Description != null && x.Description.ToLower().Contains(k))).ToList();
        }

        if (minRating.HasValue)
        {
            services = services.Where(x => x.Provider.AverageVerifiedRating >= minRating.Value).ToList();
        }

        var providerIds = services.Select(x => x.Provider.Id).Distinct().ToHashSet();
        if (providerIds.Count == 0)
        {
            return [];
        }

        var branches = await db.ProviderBranches.AsNoTracking()
            .Where(b => b.IsActive && providerIds.Contains(b.ProviderId))
            .ToListAsync(cancellationToken);

        var links = await db.ProviderServiceBranches.AsNoTracking()
            .Where(l => l.IsActive)
            .Join(
                db.ProviderServices.AsNoTracking().Where(s => s.IsActive && providerIds.Contains(s.ProviderId)),
                l => l.ProviderServiceId,
                s => s.Id,
                (l, s) => new { ServiceId = s.Id, l.ProviderBranchId })
            .ToListAsync(cancellationToken);

        var branchesByProvider = branches
            .GroupBy(b => b.ProviderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var linkBranchIdsByService = links
            .GroupBy(l => l.ServiceId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ProviderBranchId).ToHashSet());

        var candidates = services
            .SelectMany(s =>
            {
                if (!branchesByProvider.TryGetValue(s.Provider.Id, out var providerBranches) || providerBranches.Count == 0)
                {
                    return [];
                }

                if (linkBranchIdsByService.TryGetValue(s.ServiceId, out var linkedBranchIds) && linkedBranchIds.Count > 0)
                {
                    providerBranches = providerBranches.Where(b => linkedBranchIds.Contains(b.Id)).ToList();
                }

                return providerBranches.Select(branch => new
                {
                    provider = s.Provider,
                    serviceId = s.ServiceId,
                    branch,
                    category = s.Category,
                    Distance = GeoHelper.DistanceMeters(branch.Location, point),
                });
            });

        return candidates
            .Where(x => x.Distance <= radiusMeters && x.Distance <= x.branch.ServiceRadiusMeters)
            .OrderBy(x => x.Distance)
            .ThenByDescending(x => x.provider.AverageVerifiedRating)
            .Select(x => new ProviderSummaryDto(
                x.provider.Id,
                x.provider.Name,
                x.branch.Id,
                x.serviceId,
                x.category.Name,
                x.Distance,
                x.provider.AverageVerifiedRating,
                x.provider.AveragePublicRating,
                x.provider.VerifiedReviewCount))
            .Take(100)
            .ToList();
    }
}
