using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Infrastructure.Persistence;

public sealed class LocalGoDbContext(DbContextOptions<LocalGoDbContext> options)
    : DbContext(options), ILocalGoDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<ProviderBranch> ProviderBranches => Set<ProviderBranch>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<ProviderService> ProviderServices => Set<ProviderService>();
    public DbSet<ProviderServiceBranch> ProviderServiceBranches => Set<ProviderServiceBranch>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<AdminActionLog> AdminActionLogs => Set<AdminActionLog>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    public void ClearChangeTracker() => ChangeTracker.Clear();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocalGoDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    // P-1: attribute DB write time to Server-Timing "save".
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var t0 = LocalGo.Application.Common.ServerTiming.Start();
        try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
        finally { LocalGo.Application.Common.ServerTiming.AddSave(LocalGo.Application.Common.ServerTiming.Since(t0)); }
    }
}
