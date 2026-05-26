namespace LocalGo.Application.Dtos.Responses;

public sealed class BidResponseDto
{
    public Guid Id { get; init; }
    public Guid ServiceRequestId { get; init; }
    public Guid ProviderId { get; init; }
    public decimal? Amount { get; init; }
    public string Currency { get; init; } = "THB";
    public required string PriceText { get; init; }
    public string? Description { get; init; }
    public DateTime? AvailableAt { get; init; }
    public required string Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class ServiceRequestBidSummaryResponseDto
{
    public Guid Id { get; init; }
    public Guid ProviderId { get; init; }
    public string? ProviderName { get; init; }
    public required string PriceText { get; init; }
    public string? Description { get; init; }
    public DateTime? AvailableAt { get; init; }
    public required string Status { get; init; }
}

public sealed class ServiceRequestResponseDto
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? AddressText { get; init; }
    public Guid CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public required string Status { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int SearchRadiusMeters { get; init; }
    public string? BudgetText { get; init; }
    public Guid? SelectedProviderId { get; init; }
    public Guid? SelectedBidId { get; init; }
    public required string Source { get; init; }
    public Guid? ProviderServiceId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public IEnumerable<ServiceRequestBidSummaryResponseDto>? Bids { get; init; }
}

public sealed class ProviderBranchResponseDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? AddressText { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int ServiceRadiusMeters { get; init; }
    public bool IsActive { get; init; }
}

public sealed class ProviderServiceResponseDto
{
    public Guid Id { get; init; }
    public Guid CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? BasePriceText { get; init; }
    public bool IsActive { get; init; }
    public IEnumerable<Guid>? BranchIds { get; init; }
}

public sealed class ProviderResponseDto
{
    public Guid Id { get; init; }
    public required string ProviderType { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? ContactLineId { get; init; }
    public string? ContactPhone { get; init; }
    public string? ContactUrl { get; init; }
    public required string Status { get; init; }
    public decimal AverageVerifiedRating { get; init; }
    public decimal AveragePublicRating { get; init; }
    public int VerifiedReviewCount { get; init; }
    public int PublicReviewCount { get; init; }
    public IEnumerable<ProviderBranchResponseDto>? Branches { get; init; }
    public IEnumerable<ProviderServiceResponseDto>? Services { get; init; }
}

public sealed class ReviewResponseDto
{
    public Guid Id { get; init; }
    public int Rating { get; init; }
    public string? Comment { get; init; }
    public required string ReviewType { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class AppointmentResponseDto
{
    public Guid Id { get; init; }
    public Guid ServiceRequestId { get; init; }
    public Guid ProviderId { get; init; }
    public Guid RequesterUserId { get; init; }
    public DateTime? ScheduledAt { get; init; }
    public string? AddressText { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public required string Status { get; init; }
    public string? Note { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
