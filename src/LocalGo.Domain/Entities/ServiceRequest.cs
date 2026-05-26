using LocalGo.Domain.Common;
using LocalGo.Domain.Enums;
using NetTopologySuite.Geometries;

namespace LocalGo.Domain.Entities;

public sealed class ServiceRequest : AuditableEntity
{
    public Guid RequesterUserId { get; set; }
    public Guid CategoryId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? AddressText { get; set; }
    public Point Location { get; set; } = null!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int SearchRadiusMeters { get; set; } = 5000;
    public DateTime? PreferredStartAt { get; set; }
    public DateTime? PreferredEndAt { get; set; }
    public string? BudgetText { get; set; }
    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Draft;
    public DateTime? FirstBidAt { get; set; }
    public DateTime? LastProgressAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? SelectedBidId { get; set; }
    public Guid? SelectedProviderId { get; set; }
    public ServiceRequestSource Source { get; set; } = ServiceRequestSource.Marketplace;
    public Guid? ProviderServiceId { get; set; }

    public User RequesterUser { get; set; } = null!;
    public ServiceCategory Category { get; set; } = null!;
    public ProviderService? ProviderService { get; set; }
    public Bid? SelectedBid { get; set; }
    public Provider? SelectedProvider { get; set; }
    public ICollection<Bid> Bids { get; set; } = [];
    public Appointment? Appointment { get; set; }
}
