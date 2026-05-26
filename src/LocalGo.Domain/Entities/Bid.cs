using LocalGo.Domain.Common;
using LocalGo.Domain.Enums;

namespace LocalGo.Domain.Entities;

public sealed class Bid : AuditableEntity
{
    public Guid ServiceRequestId { get; set; }
    public Guid ProviderId { get; set; }
    public decimal? Amount { get; set; }
    public string Currency { get; set; } = "THB";
    public required string PriceText { get; set; }
    public string? Description { get; set; }
    public DateTime? AvailableAt { get; set; }
    public BidStatus Status { get; set; } = BidStatus.Submitted;

    public ServiceRequest ServiceRequest { get; set; } = null!;
    public Provider Provider { get; set; } = null!;
}
