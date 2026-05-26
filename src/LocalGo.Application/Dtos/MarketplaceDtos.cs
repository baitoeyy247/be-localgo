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
    bool NeedsAppointmentConfirm = false);

public sealed record BidDto(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    string PriceText,
    string? Description,
    DateTime? AvailableAt,
    string Status);

public sealed record AdminDashboardDto(
    int Users,
    int Providers,
    int PublishedRequests,
    int CompletedRequests,
    int ExpiredRequests,
    int PendingNotifications);
