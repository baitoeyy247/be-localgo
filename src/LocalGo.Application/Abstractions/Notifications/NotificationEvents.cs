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
    public const string ReviewReceived = "ReviewReceived";
}
