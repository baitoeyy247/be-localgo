using LocalGo.Domain.Common;

namespace LocalGo.Domain.Entities;

public sealed class ProviderService : AuditableEntity
{
    public Guid ProviderId { get; set; }
    public Guid CategoryId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? BasePriceText { get; set; }
    public bool IsActive { get; set; } = true;

    public Provider Provider { get; set; } = null!;
    public ServiceCategory Category { get; set; } = null!;
    public ICollection<ProviderServiceBranch> BranchLinks { get; set; } = [];
}
