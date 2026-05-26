using LocalGo.Domain.Common;
using LocalGo.Domain.Enums;

namespace LocalGo.Domain.Entities;

public sealed class User : AuditableEntity
{
    public required string LineUserId { get; set; }
    public required string DisplayName { get; set; }
    public string? PictureUrl { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public SystemRole SystemRole { get; set; } = SystemRole.User;
    public AppRole? ActiveRole { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime? PrivacyNoticeAcceptedAt { get; set; }
    public string? PrivacyNoticeVersion { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public Provider? Provider { get; set; }
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
    public ICollection<NotificationLog> NotificationLogs { get; set; } = [];
}
