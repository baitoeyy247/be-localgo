using LocalGo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Application.Abstractions.Persistence;

public interface ILocalGoDbContext
{
    DbSet<User> Users { get; }
    DbSet<Provider> Providers { get; }
    DbSet<ProviderBranch> ProviderBranches { get; }
    DbSet<ServiceCategory> ServiceCategories { get; }
    DbSet<ProviderService> ProviderServices { get; }
    DbSet<ProviderServiceBranch> ProviderServiceBranches { get; }
    DbSet<ServiceRequest> ServiceRequests { get; }
    DbSet<Bid> Bids { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<Review> Reviews { get; }
    DbSet<NotificationLog> NotificationLogs { get; }
    DbSet<Report> Reports { get; }
    DbSet<AdminActionLog> AdminActionLogs { get; }
    DbSet<MediaAsset> MediaAssets { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    void ClearChangeTracker();
}
