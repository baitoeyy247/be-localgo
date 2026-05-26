using LocalGo.Domain.Common;

namespace LocalGo.Domain.Entities;

public sealed class AdminActionLog : Entity
{
    public Guid AdminUserId { get; set; }
    public required string ActionType { get; set; }
    public required string TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTime CreatedAt { get; set; }

    public User AdminUser { get; set; } = null!;
}
