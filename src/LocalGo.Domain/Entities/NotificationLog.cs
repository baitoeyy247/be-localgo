using LocalGo.Domain.Common;
using LocalGo.Domain.Enums;

namespace LocalGo.Domain.Entities;

public sealed class NotificationLog : Entity
{
    public Guid? UserId { get; set; }
    public string? LineUserId { get; set; }
    public required string EventType { get; set; }
    public string? PayloadJson { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
}
