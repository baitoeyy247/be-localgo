using LocalGo.Domain.Common;
using NetTopologySuite.Geometries;

namespace LocalGo.Domain.Entities;

public sealed class ProviderBranch : AuditableEntity
{
    public Guid ProviderId { get; set; }
    public required string Name { get; set; }
    public string? AddressText { get; set; }
    public Point Location { get; set; } = null!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int ServiceRadiusMeters { get; set; } = 5000;
    public bool IsActive { get; set; } = true;

    public Provider Provider { get; set; } = null!;
    public ICollection<ProviderServiceBranch> ServiceLinks { get; set; } = [];
}
