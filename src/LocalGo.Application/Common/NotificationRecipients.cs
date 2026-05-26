namespace LocalGo.Application.Common;

public static class NotificationRecipients
{
    /// <summary>Returns false when the recipient is the user who performed the action (self-notification).</summary>
    public static bool ShouldNotify(Guid recipientUserId, Guid actorUserId) =>
        recipientUserId != actorUserId;
}
