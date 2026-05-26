using LocalGo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalGo.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.LineUserId).IsUnique();
        builder.Property(x => x.LineUserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.PictureUrl).HasMaxLength(500);
        builder.Property(x => x.PhoneNumber).HasMaxLength(32);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.PrivacyNoticeVersion).HasMaxLength(32);
        builder.HasOne(x => x.Provider).WithOne(x => x.OwnerUser).HasForeignKey<Provider>(x => x.OwnerUserId);
    }
}

internal sealed class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.ToTable("providers");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.OwnerUserId).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.ContactLineId).HasMaxLength(64);
        builder.Property(x => x.ContactPhone).HasMaxLength(32);
        builder.Property(x => x.ContactUrl).HasMaxLength(500);
        builder.Property(x => x.AverageVerifiedRating).HasPrecision(3, 2);
        builder.Property(x => x.AveragePublicRating).HasPrecision(3, 2);
    }
}

internal sealed class ProviderBranchConfiguration : IEntityTypeConfiguration<ProviderBranch>
{
    public void Configure(EntityTypeBuilder<ProviderBranch> builder)
    {
        builder.ToTable("provider_branches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.AddressText).HasMaxLength(500);
        builder.Property(x => x.Location).HasColumnType("geography (Point,4326)");
        builder.HasIndex(x => x.Location).HasMethod("GIST");
    }
}

internal sealed class ServiceCategoryConfiguration : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<ServiceCategory> builder)
    {
        builder.ToTable("service_categories");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        builder.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProviderServiceConfiguration : IEntityTypeConfiguration<ProviderService>
{
    public void Configure(EntityTypeBuilder<ProviderService> builder)
    {
        builder.ToTable("provider_services");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.BasePriceText).HasMaxLength(100);
    }
}

internal sealed class ProviderServiceBranchConfiguration : IEntityTypeConfiguration<ProviderServiceBranch>
{
    public void Configure(EntityTypeBuilder<ProviderServiceBranch> builder)
    {
        builder.ToTable("provider_service_branches");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ProviderServiceId, x.ProviderBranchId }).IsUnique();
    }
}

internal sealed class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> builder)
    {
        builder.ToTable("service_requests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.AddressText).HasMaxLength(500);
        builder.Property(x => x.BudgetText).HasMaxLength(100);
        builder.Property(x => x.Location).HasColumnType("geography (Point,4326)");
        builder.HasIndex(x => x.Location).HasMethod("GIST");
        builder.HasIndex(x => new { x.Status, x.LastProgressAt });
        builder.HasOne(x => x.SelectedBid).WithMany().HasForeignKey(x => x.SelectedBidId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.SelectedProvider).WithMany().HasForeignKey(x => x.SelectedProviderId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.ProviderService).WithMany().HasForeignKey(x => x.ProviderServiceId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.Source);
        builder.HasOne(x => x.Appointment).WithOne(x => x.ServiceRequest).HasForeignKey<Appointment>(x => x.ServiceRequestId);
    }
}

internal sealed class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> builder)
    {
        builder.ToTable("bids");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.ServiceRequest).WithMany(x => x.Bids).HasForeignKey(x => x.ServiceRequestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ServiceRequestId, x.ProviderId }).IsUnique();
        builder.Property(x => x.Currency).HasMaxLength(8);
        builder.Property(x => x.PriceText).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Amount).HasPrecision(12, 2);
    }
}

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.ServiceRequestId).IsUnique();
        builder.Property(x => x.AddressText).HasMaxLength(500);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.Location).HasColumnType("geography (Point,4326)");
    }
}

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Comment).HasMaxLength(2000);
    }
}

internal sealed class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("notification_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.LineUserId).HasMaxLength(64);
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}

internal sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(500);
    }
}

internal sealed class AdminActionLogConfiguration : IEntityTypeConfiguration<AdminActionLog>
{
    public void Configure(EntityTypeBuilder<AdminActionLog> builder)
    {
        builder.ToTable("admin_action_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActionType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.TargetType).HasMaxLength(80).IsRequired();
    }
}

internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("media_assets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.OwnerType, x.OwnerId });
    }
}
