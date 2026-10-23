namespace LocalGo.Application.Abstractions.Notifications;

/// <summary>String constants for notification event types used across app services and the notification publisher.</summary>
public static class NotificationEvents
{
    public const string NewMatchingRequest = "NewMatchingRequest";
    public const string NewBidReceived = "NewBidReceived";
    public const string BidSelected = "BidSelected";
    public const string AppointmentCreated = "AppointmentCreated";
    public const string AppointmentUpdated = "AppointmentUpdated";
    public const string RequestExpiringSoon = "RequestExpiringSoon";
    public const string RequestExpired = "RequestExpired";
    public const string RequestCancelled = "RequestCancelled";
    public const string ReviewReceived = "ReviewReceived";
    public const string BidUpdated = "BidUpdated";
    public const string BidWithdrawn = "BidWithdrawn";
    public const string AccountSuspended = "AccountSuspended";
    public const string AccountReactivated = "AccountReactivated";
    /// <summary>R46 D3: requester cleared the selection; sent to the de-selected provider.</summary>
    public const string SelectionCleared = "SelectionCleared";
}
