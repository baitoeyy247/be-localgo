using LocalGo.Domain.Common;

namespace LocalGo.Domain.Entities;

public sealed class ServiceCategory : AuditableEntity
{
    public Guid? ParentId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ServiceCategory? Parent { get; set; }
    public ICollection<ServiceCategory> Children { get; set; } = [];
    public ICollection<ProviderService> ProviderServices { get; set; } = [];
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
}
