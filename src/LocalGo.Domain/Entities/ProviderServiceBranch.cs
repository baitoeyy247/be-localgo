using LocalGo.Domain.Common;

namespace LocalGo.Domain.Entities;

public sealed class ProviderServiceBranch : Entity
{
    public Guid ProviderServiceId { get; set; }
    public Guid ProviderBranchId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ProviderService ProviderService { get; set; } = null!;
    public ProviderBranch ProviderBranch { get; set; } = null!;
}
