namespace LocalGo.Application.Dtos;

public sealed record LineLoginRequest(string LineIdToken, string? LineAccessToken);
public sealed record UpdateActiveRoleRequest(string ActiveRole);
public sealed record AcceptPrivacyRequest(string Version);

public sealed record CreateProviderRequest(
    string ProviderType,
    string Name,
    string? Description,
    string? ContactLineId,
    string? ContactPhone,
    string? ContactUrl);

public sealed record UpdateProviderRequest(
    string? Name,
    string? Description,
    string? ContactLineId,
    string? ContactPhone,
    string? ContactUrl,
    string? Status);

public sealed record BranchRequest(
    string Name,
    string? AddressText,
    double Latitude,
    double Longitude,
    int ServiceRadiusMeters,
    bool IsActive = true);

public sealed record CreateServiceRequest(
    Guid CategoryId,
    string Title,
    string? Description,
    string? BasePriceText,
    IReadOnlyList<Guid>? BranchIds);

public sealed record UpdateServiceRequest(
    Guid? CategoryId,
    string? Title,
    string? Description,
    string? BasePriceText,
    IReadOnlyList<Guid>? BranchIds);

public sealed record CreateServiceRequestDto(
    Guid CategoryId,
    string Title,
    string? Description,
    string? AddressText,
    double Latitude,
    double Longitude,
    int SearchRadiusMeters,
    DateTime? PreferredStartAt,
    DateTime? PreferredEndAt,
    string? BudgetText,
    bool Publish);

public sealed record SelectBidRequest(Guid BidId);
public sealed record SelectProviderRequest(Guid ProviderId);
public sealed record SubmitBidRequest(string PriceText, decimal? Amount, string? Currency, string? Description, DateTime? AvailableAt);
public sealed record CreateAppointmentRequest(DateTime? ScheduledAt, string? AddressText, double? Latitude, double? Longitude, string? Note);
public sealed record UpdateAppointmentStatusRequest(string? Reason);
public sealed record CreateReviewRequest(Guid? ServiceRequestId, string ReviewType, int Rating, string? Comment);
public sealed record CreateReportRequest(string TargetType, Guid TargetId, string Reason, string? Description);

public sealed record ProviderSummaryDto(
    Guid ProviderId,
    string ProviderName,
    Guid? MatchedBranchId,
    Guid? MatchedServiceId,
    string? CategoryName,
    double DistanceMeters,
    decimal AverageVerifiedRating,
    decimal AveragePublicRating,
    int VerifiedReviewCount);

public sealed record CategoryDto(Guid Id, string Name, string Slug, string? Description);

public sealed record ServiceRequestSummaryDto(
    Guid Id,
    string Title,
    string Status,
    string? CategoryName,
    DateTime CreatedAt,
    int BidCount,
    string? BudgetText = null,
    double? DistanceMeters = null,
    bool AlreadyBidByMe = false,
    bool NeedsAppointmentConfirm = false,
    bool IsOwnRequest = false,
    bool HasReviewed = false,
    int? MyReviewRating = null,
    string? AppointmentStatus = null);

public sealed record ProviderBidListItemDto(
    Guid Id,
    Guid ServiceRequestId,
    string RequestTitle,
    string PriceText,
    string Status,
    DateTime CreatedAt,
    string? RequestStatus = null,
    string? AppointmentStatus = null,
    DateTime? AppointmentScheduledAt = null);

public sealed record ReviewCreatedDto(
    Guid Id,
    int Rating,
    string? Comment,
    string ReviewType,
    DateTime CreatedAt,
    string ReviewerDisplayName,
    Guid? ServiceRequestId);

public sealed record BidDto(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    string PriceText,
    string? Description,
    DateTime? AvailableAt,
    string Status,
    decimal AverageVerifiedRating = 0,
    int VerifiedReviewCount = 0);

public sealed record ProviderCoverageAreaDto(string Name, int ServiceRadiusMeters);

public sealed record ProviderMatchingInboxDto(
    IReadOnlyList<ServiceRequestSummaryDto> Items,
    bool ExcludesOwnRequests,
    IReadOnlyList<ProviderCoverageAreaDto> Areas,
    IReadOnlyList<ServiceRequestSummaryDto>? MyJobs = null);

public sealed record AdminUserDto(
    Guid Id,
    string DisplayName,
    string LineUserId,
    string SystemRole,
    string Status = "Active",
    bool IsTestUser = false);

public sealed record AdminUsersPageDto(
    IReadOnlyList<AdminUserDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record SetUserStatusRequest(string Status);

public sealed record AdminDashboardDto(
    int Users,
    int Providers,
    int PublishedRequests,
    int CompletedRequests,
    int ExpiredRequests,
    int PendingNotifications);
