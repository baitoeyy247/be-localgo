namespace LocalGo.Domain.Enums;

public enum SystemRole
{
    User = 0,
    Admin = 1,
}

public enum AppRole
{
    Requester = 0,
    Provider = 1,
}

public enum UserStatus
{
    Active = 0,
    Suspended = 1,
    Deleted = 2,
}

public enum ProviderType
{
    Individual = 0,
    Shop = 1,
    Company = 2,
    Freelancer = 3,
    Other = 4,
}

public enum ProviderStatus
{
    Draft = 0,
    Active = 1,
    Paused = 2,
    Suspended = 3,
}

public enum ServiceRequestStatus
{
    Draft = 0,
    Open = 1,
    HasOffers = 2,
    ProviderSelected = 3,
    Scheduled = 4,
    InProgress = 5,
    Completed = 6,
    Cancelled = 7,
    Expired = 8,
}

/// <summary>How the service request was created (marketplace post vs direct booking from provider listing).</summary>
public enum ServiceRequestSource
{
    Marketplace = 0,
    DirectBooking = 1,
}

public enum BidStatus
{
    Submitted = 0,
    Selected = 1,
    Rejected = 2,
    Withdrawn = 3,
}

public enum AppointmentStatus
{
    Pending = 0,
    Confirmed = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
}

public enum ReviewType
{
    Verified = 0,
    Public = 1,
}

public enum ReviewStatus
{
    Published = 0,
    Hidden = 1,
    Reported = 2,
    Removed = 3,
}

public enum NotificationStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Skipped = 3,
}

public enum ReportTargetType
{
    Provider = 0,
    ServiceRequest = 1,
    Bid = 2,
    Review = 3,
}

public enum ReportStatus
{
    Open = 0,
    InReview = 1,
    Resolved = 2,
    Dismissed = 3,
}

public enum ReportReason
{
    MisleadingInformation = 0,
    Spam = 1,
    Harassment = 2,
    InappropriateContent = 3,
    Other = 4,
}

public enum MediaOwnerType
{
    Provider = 0,
    ServiceRequest = 1,
}
