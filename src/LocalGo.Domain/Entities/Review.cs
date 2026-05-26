using LocalGo.Domain.Common;
using LocalGo.Domain.Enums;

namespace LocalGo.Domain.Entities;

public sealed class Review : AuditableEntity
{
    public Guid ReviewerUserId { get; set; }
    public Guid ProviderId { get; set; }
    public Guid? ServiceRequestId { get; set; }
    public ReviewType ReviewType { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public ReviewStatus Status { get; set; } = ReviewStatus.Published;

    public User ReviewerUser { get; set; } = null!;
    public Provider Provider { get; set; } = null!;
    public ServiceRequest? ServiceRequest { get; set; }
}
