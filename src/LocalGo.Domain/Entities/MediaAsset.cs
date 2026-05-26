using LocalGo.Domain.Common;
using LocalGo.Domain.Enums;

namespace LocalGo.Domain.Entities;

public sealed class MediaAsset : Entity
{
    public MediaOwnerType OwnerType { get; set; }
    public Guid OwnerId { get; set; }
    public required string StorageKey { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
