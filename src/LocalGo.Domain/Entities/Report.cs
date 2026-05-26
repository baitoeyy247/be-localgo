using LocalGo.Domain.Common;
using LocalGo.Domain.Enums;

namespace LocalGo.Domain.Entities;

public sealed class Report : Entity
{
    public Guid ReporterUserId { get; set; }
    public ReportTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public ReportReason Reason { get; set; }
    public string? Description { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public User ReporterUser { get; set; } = null!;
}
