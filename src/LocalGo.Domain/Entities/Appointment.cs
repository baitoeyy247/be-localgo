using LocalGo.Domain.Common;
using LocalGo.Domain.Enums;
using NetTopologySuite.Geometries;

namespace LocalGo.Domain.Entities;

public sealed class Appointment : AuditableEntity
{
    public Guid ServiceRequestId { get; set; }
    public Guid ProviderId { get; set; }
    public Guid RequesterUserId { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? AddressText { get; set; }
    public Point? Location { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public string? Note { get; set; }

    public ServiceRequest ServiceRequest { get; set; } = null!;
    public Provider Provider { get; set; } = null!;
    public User RequesterUser { get; set; } = null!;
}
