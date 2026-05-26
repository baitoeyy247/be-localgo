using LocalGo.Domain.Common;
using LocalGo.Domain.Enums;

namespace LocalGo.Domain.Entities;

public sealed class Provider : AuditableEntity
{
    public Guid OwnerUserId { get; set; }
    public ProviderType ProviderType { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? ContactLineId { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactUrl { get; set; }
    public ProviderStatus Status { get; set; } = ProviderStatus.Draft;
    public decimal AverageVerifiedRating { get; set; }
    public decimal AveragePublicRating { get; set; }
    public int VerifiedReviewCount { get; set; }
    public int PublicReviewCount { get; set; }

    public User OwnerUser { get; set; } = null!;
    public ICollection<ProviderBranch> Branches { get; set; } = [];
    public ICollection<ProviderService> Services { get; set; } = [];
    public ICollection<Bid> Bids { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
}
