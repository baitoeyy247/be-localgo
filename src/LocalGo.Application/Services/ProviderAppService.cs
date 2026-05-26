using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace LocalGo.Application.Services;

public sealed class ProviderAppService(ILocalGoDbContext db)
{
    public async Task<Provider> CreateAsync(Guid userId, CreateProviderRequest request, CancellationToken cancellationToken)
    {
        if (await db.Providers.AnyAsync(p => p.OwnerUserId == userId, cancellationToken))
        {
            throw new AppException("Provider profile already exists.", 409);
        }

        if (!Enum.TryParse<ProviderType>(request.ProviderType, true, out var providerType))
        {
            throw new AppException("Invalid provider type.", 400);
        }

        var now = DateTime.UtcNow;
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            ProviderType = providerType,
            Name = request.Name.Trim(),
            Description = request.Description,
            ContactLineId = request.ContactLineId,
            ContactPhone = request.ContactPhone,
            ContactUrl = request.ContactUrl,
            Status = ProviderStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Providers.Add(provider);
        await db.SaveChangesAsync(cancellationToken);
        return provider;
    }

    public async Task<Provider?> GetMineAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Providers
            .Include(p => p.Branches)
            .Include(p => p.Services).ThenInclude(s => s.Category)
            .Include(p => p.Services).ThenInclude(s => s.BranchLinks)
            .FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);

    public async Task<Provider?> GetPublicAsync(Guid providerId, CancellationToken cancellationToken) =>
        await db.Providers.AsNoTracking()
            .Include(p => p.Branches)
            .Include(p => p.Services).ThenInclude(s => s.Category)
            .FirstOrDefaultAsync(p => p.Id == providerId && p.Status == ProviderStatus.Active, cancellationToken);

    public async Task<Provider> UpdateAsync(Guid userId, Guid providerId, UpdateProviderRequest request, CancellationToken cancellationToken)
    {
        var provider = await RequireOwnedProviderAsync(userId, providerId, cancellationToken);
        if (request.Name is not null) provider.Name = request.Name.Trim();
        if (request.Description is not null) provider.Description = request.Description;
        if (request.ContactLineId is not null) provider.ContactLineId = request.ContactLineId;
        if (request.ContactPhone is not null) provider.ContactPhone = request.ContactPhone;
        if (request.ContactUrl is not null) provider.ContactUrl = request.ContactUrl;
        if (request.Status is not null && Enum.TryParse<ProviderStatus>(request.Status, true, out var status))
        {
            provider.Status = status;
        }

        provider.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return provider;
    }

    public async Task<ProviderBranch> AddBranchAsync(Guid userId, Guid providerId, BranchRequest request, CancellationToken cancellationToken)
    {
        _ = await RequireOwnedProviderAsync(userId, providerId, cancellationToken);
        var now = DateTime.UtcNow;
        var branch = new ProviderBranch
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            Name = request.Name.Trim(),
            AddressText = request.AddressText,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Location = GeoHelper.CreatePoint(request.Latitude, request.Longitude),
            ServiceRadiusMeters = request.ServiceRadiusMeters,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.ProviderBranches.Add(branch);
        await db.SaveChangesAsync(cancellationToken);
        return branch;
    }

    public async Task<ProviderBranch> UpdateBranchAsync(
        Guid userId, Guid providerId, Guid branchId, BranchRequest request, CancellationToken cancellationToken)
    {
        _ = await RequireOwnedProviderAsync(userId, providerId, cancellationToken);
        var branch = await db.ProviderBranches.FirstOrDefaultAsync(b => b.Id == branchId && b.ProviderId == providerId, cancellationToken)
            ?? throw new AppException("Branch not found.", 404);

        branch.Name = request.Name.Trim();
        branch.AddressText = request.AddressText;
        branch.Latitude = request.Latitude;
        branch.Longitude = request.Longitude;
        branch.Location = GeoHelper.CreatePoint(request.Latitude, request.Longitude);
        branch.ServiceRadiusMeters = request.ServiceRadiusMeters;
        branch.IsActive = request.IsActive;
        branch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return branch;
    }

    public async Task<ProviderService> AddServiceAsync(
        Guid userId, Guid providerId, CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var provider = await RequireOwnedProviderAsync(userId, providerId, cancellationToken);
        var now = DateTime.UtcNow;
        var service = new ProviderService
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            CategoryId = request.CategoryId,
            Title = request.Title.Trim(),
            Description = request.Description,
            BasePriceText = request.BasePriceText,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.ProviderServices.Add(service);

        var branchIds = request.BranchIds?.Count > 0
            ? request.BranchIds
            : provider.Branches.Where(b => b.IsActive).Select(b => b.Id).ToList();

        foreach (var branchId in branchIds)
        {
            db.ProviderServiceBranches.Add(new ProviderServiceBranch
            {
                Id = Guid.NewGuid(),
                ProviderServiceId = service.Id,
                ProviderBranchId = branchId,
                IsActive = true,
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return service;
    }

    public async Task SetServiceBranchesAsync(
        Guid userId, Guid providerId, Guid serviceId, IReadOnlyList<Guid> branchIds, CancellationToken cancellationToken)
    {
        _ = await RequireOwnedProviderAsync(userId, providerId, cancellationToken);
        var links = await db.ProviderServiceBranches.Where(x => x.ProviderServiceId == serviceId).ToListAsync(cancellationToken);
        db.ProviderServiceBranches.RemoveRange(links);
        var now = DateTime.UtcNow;
        foreach (var branchId in branchIds)
        {
            db.ProviderServiceBranches.Add(new ProviderServiceBranch
            {
                Id = Guid.NewGuid(),
                ProviderServiceId = serviceId,
                ProviderBranchId = branchId,
                IsActive = true,
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProviderService> UpdateServiceAsync(
        Guid userId, Guid providerId, Guid serviceId, UpdateServiceRequest request, CancellationToken cancellationToken)
    {
        _ = await RequireOwnedProviderAsync(userId, providerId, cancellationToken);
        var service = await db.ProviderServices.FirstOrDefaultAsync(
            s => s.Id == serviceId && s.ProviderId == providerId, cancellationToken)
            ?? throw new AppException("Service not found.", 404);

        if (request.CategoryId is not null)
        {
            service.CategoryId = request.CategoryId.Value;
        }

        if (request.Title is not null)
        {
            service.Title = request.Title.Trim();
        }

        if (request.Description is not null)
        {
            service.Description = request.Description;
        }

        if (request.BasePriceText is not null)
        {
            service.BasePriceText = request.BasePriceText;
        }

        service.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (request.BranchIds is not null)
        {
            await SetServiceBranchesAsync(userId, providerId, serviceId, request.BranchIds, cancellationToken);
        }

        return service;
    }

    public async Task SetServiceActiveAsync(Guid userId, Guid providerId, Guid serviceId, bool active, CancellationToken cancellationToken)
    {
        _ = await RequireOwnedProviderAsync(userId, providerId, cancellationToken);
        var service = await db.ProviderServices.FirstOrDefaultAsync(s => s.Id == serviceId && s.ProviderId == providerId, cancellationToken)
            ?? throw new AppException("Service not found.", 404);
        service.IsActive = active;
        service.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Provider> RequireOwnedProviderAsync(Guid userId, Guid providerId, CancellationToken cancellationToken)
    {
        var provider = await db.Providers.Include(p => p.Branches).FirstOrDefaultAsync(
            p => p.Id == providerId && p.OwnerUserId == userId, cancellationToken);
        return provider ?? throw new AppException("Provider not found.", 404);
    }
}
