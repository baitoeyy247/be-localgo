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
    /// <summary>Customer note / booking note (never mixed with provider decline text).</summary>
    public string? Note { get; set; }
    /// <summary>Optional reason when the provider rejects the appointment.</summary>
    public string? DeclineReason { get; set; }

    public ServiceRequest ServiceRequest { get; set; } = null!;
    public Provider Provider { get; set; } = null!;
    public User RequesterUser { get; set; } = null!;
}
