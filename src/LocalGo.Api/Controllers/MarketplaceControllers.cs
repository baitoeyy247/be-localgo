using AutoMapper;
using LocalGo.Api.Auth;
using LocalGo.Application.Abstractions.Auth;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Dtos;
using LocalGo.Application.Dtos.Responses;
using LocalGo.Application.Services;
using LocalGo.Domain.Enums;
using LocalGo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Api.Controllers;

[ApiController]
[Route("api/providers")]
[Authorize]
public sealed class ProvidersController(ICurrentUserService currentUser, ProviderAppService providers, IMapper mapper) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProviderRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var provider = await providers.CreateAsync(userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetPublic), new { providerId = provider.Id }, mapper.Map<ProviderResponseDto>(provider));
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var provider = await providers.GetMineAsync(userId, cancellationToken);
        return provider is null ? NotFound() : Ok(mapper.Map<ProviderResponseDto>(provider));
    }

    [AllowAnonymous]
    [HttpGet("{providerId:guid}")]
    public async Task<IActionResult> GetPublic(Guid providerId, CancellationToken cancellationToken)
    {
        var provider = await providers.GetPublicAsync(providerId, cancellationToken);
        if (provider is null)
        {
            return NotFound();
        }

        var dto = mapper.Map<ProviderResponseDto>(provider);
        if (!await providers.ViewerMaySeeContactAsync(currentUser.UserId, providerId, cancellationToken))
        {
            dto.ContactLineId = null;
            dto.ContactPhone = null;
        }

        return Ok(dto);
    }

    [HttpPost("{providerId:guid}/services/{serviceId:guid}/book")]
    public async Task<IActionResult> BookService(
        Guid providerId,
        Guid serviceId,
        [FromBody] CreateAppointmentRequest request,
        AppointmentAppService appointments,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var appointment = await appointments.BookFromProviderServiceAsync(
            userId, providerId, serviceId, request, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            new { appointmentId = appointment.Id, serviceRequestId = appointment.ServiceRequestId });
    }

    [HttpPatch("{providerId:guid}")]
    public async Task<IActionResult> Update(Guid providerId, [FromBody] UpdateProviderRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var provider = await providers.UpdateAsync(userId, providerId, request, cancellationToken);
        return Ok(mapper.Map<ProviderResponseDto>(provider));
    }

    [HttpPost("{providerId:guid}/branches")]
    public async Task<IActionResult> AddBranch(Guid providerId, [FromBody] BranchRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var branch = await providers.AddBranchAsync(userId, providerId, request, cancellationToken);
        return Ok(new
        {
            branch.Id,
            branch.Name,
            branch.AddressText,
            branch.Latitude,
            branch.Longitude,
            branch.ServiceRadiusMeters,
            branch.IsActive,
        });
    }

    [HttpDelete("{providerId:guid}/branches/{branchId:guid}")]
    public async Task<IActionResult> DeleteBranch(Guid providerId, Guid branchId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        await providers.DeleteBranchAsync(userId, providerId, branchId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{providerId:guid}/branches/{branchId:guid}")]
    public async Task<IActionResult> UpdateBranch(Guid providerId, Guid branchId, [FromBody] BranchRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var branch = await providers.UpdateBranchAsync(userId, providerId, branchId, request, cancellationToken);
        return Ok(new
        {
            branch.Id,
            branch.Name,
            branch.AddressText,
            branch.Latitude,
            branch.Longitude,
            branch.ServiceRadiusMeters,
            branch.IsActive,
        });
    }

    [HttpPost("{providerId:guid}/services")]
    public async Task<IActionResult> AddService(Guid providerId, [FromBody] CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var service = await providers.AddServiceAsync(userId, providerId, request, cancellationToken);
        return Ok(new
        {
            service.Id,
            service.CategoryId,
            service.Title,
            service.Description,
            service.BasePriceText,
            service.IsActive,
        });
    }

    [HttpPatch("{providerId:guid}/services/{serviceId:guid}")]
    public async Task<IActionResult> UpdateService(
        Guid providerId, Guid serviceId, [FromBody] UpdateServiceRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var service = await providers.UpdateServiceAsync(userId, providerId, serviceId, request, cancellationToken);
        return Ok(new
        {
            service.Id,
            service.CategoryId,
            service.Title,
            service.Description,
            service.BasePriceText,
            service.IsActive,
        });
    }

    [HttpPut("{providerId:guid}/services/{serviceId:guid}/branches")]
    public async Task<IActionResult> SetServiceBranches(
        Guid providerId, Guid serviceId, [FromBody] IReadOnlyList<Guid> branchIds, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        await providers.SetServiceBranchesAsync(userId, providerId, serviceId, branchIds, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{providerId:guid}/services/{serviceId:guid}")]
    public async Task<IActionResult> DeleteService(Guid providerId, Guid serviceId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        await providers.DeleteServiceAsync(userId, providerId, serviceId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{providerId:guid}/services/{serviceId:guid}/pause")]
    public async Task<IActionResult> PauseService(Guid providerId, Guid serviceId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        await providers.SetServiceActiveAsync(userId, providerId, serviceId, false, cancellationToken);
        return NoContent();
    }

    [HttpPost("{providerId:guid}/services/{serviceId:guid}/activate")]
    public async Task<IActionResult> ActivateService(Guid providerId, Guid serviceId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        await providers.SetServiceActiveAsync(userId, providerId, serviceId, true, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(CategoryAppService categories) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await categories.ListActiveAsync(cancellationToken));
}

[ApiController]
[Route("api/search")]
public sealed class SearchController(SearchAppService search) : ControllerBase
{
    [HttpGet("providers")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchProviders(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] int radiusMeters = 5000,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? keyword = null,
        [FromQuery] decimal? minRating = null,
        CancellationToken cancellationToken = default) =>
        Ok(await search.SearchProvidersAsync(lat, lng, radiusMeters, categoryId, keyword, minRating, cancellationToken));
}

[ApiController]
[Route("api/service-requests")]
[Authorize]
public sealed class ServiceRequestsController(
    ICurrentUserService currentUser,
    ServiceRequestAppService requests,
    BidAppService bids,
    IMapper mapper) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequestDto request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var created = await requests.CreateAsync(userId, request, cancellationToken);
        return Ok(mapper.Map<ServiceRequestResponseDto>(created));
    }

    [HttpPost("{requestId:guid}/publish")]
    public async Task<IActionResult> Publish(Guid requestId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var published = await requests.PublishAsync(userId, requestId, cancellationToken);
        return Ok(mapper.Map<ServiceRequestResponseDto>(published));
    }

    [HttpGet("me")]
    public async Task<IActionResult> ListMine(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        return Ok(await requests.ListMineAsync(userId, cancellationToken));
    }

    [HttpGet("{requestId:guid}")]
    public async Task<IActionResult> Get(Guid requestId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var item = await requests.GetDetailAsync(userId, requestId, currentUser.IsAdmin, cancellationToken);
        return item is null ? NotFound() : Ok(mapper.Map<ServiceRequestResponseDto>(item));
    }

    [HttpPost("{requestId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid requestId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        await requests.CancelAsync(userId, requestId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{requestId:guid}/select-bid")]
    public async Task<IActionResult> SelectBid(Guid requestId, [FromBody] SelectBidRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        await bids.SelectAsync(userId, requestId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{requestId:guid}/clear-selection")]
    public async Task<IActionResult> ClearSelection(Guid requestId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        await bids.ClearSelectionAsync(userId, requestId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{requestId:guid}/select-provider")]
    public async Task<IActionResult> SelectProvider(
        Guid requestId,
        [FromBody] SelectProviderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        await requests.SelectProviderAsync(userId, requestId, request.ProviderId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{requestId:guid}/start")]
    public async Task<IActionResult> Start(Guid requestId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        await requests.StartAsync(userId, requestId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{requestId:guid}/complete")]
    public async Task<IActionResult> Complete(Guid requestId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        await requests.CompleteAsync(userId, requestId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{requestId:guid}/bids")]
    public async Task<IActionResult> SubmitBid(Guid requestId, [FromBody] SubmitBidRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var bid = await bids.SubmitAsync(userId, requestId, request, cancellationToken);
        return Ok(mapper.Map<BidResponseDto>(bid));
    }

    [HttpGet("{requestId:guid}/bids")]
    public async Task<IActionResult> ListBids(Guid requestId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        return Ok(await bids.ListForRequestAsync(userId, requestId, currentUser.IsAdmin, cancellationToken));
    }

    [HttpPost("{requestId:guid}/appointment")]
    public async Task<IActionResult> CreateAppointment(
        Guid requestId,
        [FromBody] CreateAppointmentRequest request,
        AppointmentAppService appointments,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var appointment = await appointments.CreateAsync(userId, requestId, request, cancellationToken);
        return Ok(mapper.Map<AppointmentResponseDto>(appointment));
    }

    [HttpGet("{requestId:guid}/appointment")]
    public async Task<IActionResult> GetAppointment(
        Guid requestId,
        AppointmentAppService appointments,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var appointment = await appointments.GetByRequestIdAsync(userId, requestId, cancellationToken);
        if (appointment is null)
        {
            return NotFound();
        }

        return Ok(mapper.Map<AppointmentResponseDto>(appointment));
    }
}

[ApiController]
[Route("api/provider/service-requests")]
[Authorize]
public sealed class ProviderMatchingController(
    ICurrentUserService currentUser,
    ServiceRequestAppService requests,
    IWebHostEnvironment environment,
    IMapper mapper) : ControllerBase
{
    [HttpGet("/api/provider/summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var pending = await requests.CountPendingConfirmationsAsync(userId, cancellationToken);
        return Ok(new { pendingConfirmCount = pending });
    }

    [HttpGet("matching")]
    public async Task<IActionResult> Matching(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        return Ok(await requests.ListMatchingForProviderAsync(
            userId,
            excludeSmokeTestData: environment.IsDevelopment(),
            includeOwnRequests: environment.IsDevelopment(),
            cancellationToken));
    }

    /// <summary>Deep link for a LINE notification: load the bid form, or explain why this user cannot bid.</summary>
    [HttpGet("{requestId:guid}/bid-form")]
    public async Task<IActionResult> BidForm(Guid requestId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var access = await requests.GetBidFormAsync(userId, requestId, cancellationToken);
        return Ok(new
        {
            allowed = access.Allowed,
            reason = access.Reason,
            reasonCode = access.ReasonCode,
            request = access.Request is null ? null : mapper.Map<ServiceRequestResponseDto>(access.Request),
        });
    }

    /// <summary>Provider view of a specific request — shows request summary without other providers' bids.</summary>
    [HttpGet("{requestId:guid}")]
    public async Task<IActionResult> GetForProvider(Guid requestId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var item = await requests.GetDetailAsync(userId, requestId, isAdmin: false, cancellationToken);
        if (item is null) return NotFound();
        return Ok(mapper.Map<ServiceRequestResponseDto>(item));
    }
}

[ApiController]
[Route("api/bids")]
[Authorize]
public sealed class BidsController(ICurrentUserService currentUser, BidAppService bids, IMapper mapper) : ControllerBase
{
    [HttpPatch("{bidId:guid}")]
    public async Task<IActionResult> Update(Guid bidId, [FromBody] SubmitBidRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var bid = await bids.UpdateAsync(userId, bidId, request, cancellationToken);
        return Ok(mapper.Map<BidResponseDto>(bid));
    }

    [HttpGet("/api/provider/bids")]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        return Ok(await bids.ListMineAsync(userId, cancellationToken));
    }

    [HttpPost("{bidId:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid bidId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        await bids.WithdrawAsync(userId, bidId, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/appointments")]
[Authorize]
public sealed class AppointmentsController(ICurrentUserService currentUser, AppointmentAppService appointments, IMapper mapper) : ControllerBase
{
    [HttpGet("{appointmentId:guid}")]
    public async Task<IActionResult> Get(Guid appointmentId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var appointment = await appointments.GetByIdAsync(userId, appointmentId, cancellationToken);
        return Ok(mapper.Map<AppointmentResponseDto>(appointment));
    }

    [HttpPatch("{appointmentId:guid}")]
    public async Task<IActionResult> Update(
        Guid appointmentId,
        [FromQuery] string status,
        [FromBody] UpdateAppointmentStatusRequest? body,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        if (!Enum.TryParse<AppointmentStatus>(status, true, out var parsed))
        {
            return BadRequest();
        }

        var appointment = await appointments.UpdateStatusAsync(
            userId, appointmentId, parsed, body?.Reason, cancellationToken);
        return Ok(mapper.Map<AppointmentResponseDto>(appointment));
    }
}

[ApiController]
[Route("api/providers/{providerId:guid}/reviews")]
public sealed class ReviewsController(ReviewAppService reviews, IMapper mapper) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(
        Guid providerId, [FromBody] CreateReviewRequest request, ICurrentUserService currentUser, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var created = await reviews.CreateAsync(userId, providerId, request, cancellationToken);
        return Created($"/api/providers/{providerId}/reviews/{created.Id}", new ReviewResponseDto
        {
            Id = created.Id,
            Rating = created.Rating,
            Comment = created.Comment,
            ReviewType = created.ReviewType,
            CreatedAt = created.CreatedAt,
            ReviewerDisplayName = created.ReviewerDisplayName,
            ServiceRequestId = created.ServiceRequestId,
        });
    }

    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> Mine(Guid providerId, [FromQuery] Guid serviceRequestId, ICurrentUserService currentUser, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var review = await reviews.FindMineAsync(userId, providerId, serviceRequestId, cancellationToken);
        return review is null ? NotFound() : Ok(mapper.Map<ReviewResponseDto>(review));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(Guid providerId, CancellationToken cancellationToken)
    {
        var items = await reviews.ListForProviderAsync(providerId, cancellationToken);
        return Ok(mapper.Map<IEnumerable<ReviewResponseDto>>(items));
    }
}

[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController(ICurrentUserService currentUser, ReportAppService reports) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReportRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        return Ok(await reports.CreateAsync(userId, request, cancellationToken));
    }
}

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "Admin")]
[RequireAdmin]
public sealed class AdminController(AdminAppService admin, ILocalGoDbContext db, LocalGoDbContext efDb) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken) =>
        Ok(await admin.GetDashboardAsync(cancellationToken));

    [HttpGet("users")]
    public async Task<IActionResult> Users(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await admin.ListUsersAsync(page, pageSize, cancellationToken));

    [HttpPatch("users/{userId:guid}/status")]
    public async Task<IActionResult> SetUserStatus(
        Guid userId,
        [FromBody] SetUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        await admin.SetUserStatusAsync(userId, request.Status, cancellationToken);
        return NoContent();
    }

    [HttpPost("test-data/reset")]
    public async Task<IActionResult> ResetTestData(CancellationToken cancellationToken)
    {
        await AppointmentSchemaBootstrap.EnsureSitListIndexesAsync(efDb, cancellationToken);
        return Ok(await admin.ResetTestDataAsync(cancellationToken));
    }

    [HttpPost("ratings/recalculate")]
    public async Task<IActionResult> RecalculateRatings(CancellationToken cancellationToken)
    {
        var repair = await admin.RepairSitPlaytestDataAsync(cancellationToken);
        return Ok(repair);
    }

    [HttpPost("test-data/repair")]
    public async Task<IActionResult> RepairTestData(CancellationToken cancellationToken)
    {
        await AppointmentSchemaBootstrap.EnsureSitListIndexesAsync(efDb, cancellationToken);
        return Ok(await admin.RepairSitPlaytestDataAsync(cancellationToken));
    }

    [HttpGet("appointments")]
    public async Task<IActionResult> Appointments(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var query = db.Appointments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<AppointmentStatus>(status, true, out var parsed))
        {
            query = query.Where(a => a.Status == parsed);
        }

        var rows = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(100)
            .Select(a => new
            {
                a.Id,
                a.ServiceRequestId,
                a.ProviderId,
                a.RequesterUserId,
                a.ScheduledAt,
                a.AddressText,
                a.Status,
                a.Note,
                a.DeclineReason,
                a.CreatedAt,
                a.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(a => new
        {
            a.Id,
            a.ServiceRequestId,
            a.ProviderId,
            a.RequesterUserId,
            a.ScheduledAt,
            a.AddressText,
            Status = a.Status.ToString(),
            a.Note,
            a.DeclineReason,
            a.CreatedAt,
            a.UpdatedAt,
        }));
    }

    [HttpGet("providers")]
    public async Task<IActionResult> Providers(CancellationToken cancellationToken)
    {
        var rows = await db.Providers.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(100)
            .Select(p => new
            {
                p.Id,
                p.ProviderType,
                p.Name,
                p.Description,
                p.ContactUrl,
                p.Status,
                p.AverageVerifiedRating,
                p.AveragePublicRating,
                p.VerifiedReviewCount,
                p.PublicReviewCount,
            })
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(p => new ProviderResponseDto
        {
            Id = p.Id,
            ProviderType = p.ProviderType.ToString(),
            Name = p.Name,
            Description = p.Description,
            ContactUrl = p.ContactUrl,
            Status = p.Status.ToString(),
            AverageVerifiedRating = p.AverageVerifiedRating,
            AveragePublicRating = p.AveragePublicRating,
            VerifiedReviewCount = p.VerifiedReviewCount,
            PublicReviewCount = p.PublicReviewCount,
        }));
    }

    [HttpGet("service-requests")]
    public async Task<IActionResult> ServiceRequests(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var query = db.ServiceRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<ServiceRequestStatus>(status, true, out var parsed))
        {
            query = query.Where(r => r.Status == parsed);
        }

        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(100)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.Description,
                r.AddressText,
                r.CategoryId,
                CategoryName = r.Category.Name,
                r.Status,
                r.Latitude,
                r.Longitude,
                r.SearchRadiusMeters,
                r.PreferredStartAt,
                r.PreferredEndAt,
                r.BudgetText,
                r.SelectedProviderId,
                r.SelectedBidId,
                r.Source,
                r.ProviderServiceId,
                r.CreatedAt,
                r.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(r => new ServiceRequestResponseDto
        {
            Id = r.Id,
            Title = r.Title,
            Description = r.Description,
            AddressText = r.AddressText,
            CategoryId = r.CategoryId,
            CategoryName = r.CategoryName,
            Status = r.Status.ToString(),
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            SearchRadiusMeters = r.SearchRadiusMeters,
            PreferredStartAt = r.PreferredStartAt,
            PreferredEndAt = r.PreferredEndAt,
            BudgetText = r.BudgetText,
            SelectedProviderId = r.SelectedProviderId,
            SelectedBidId = r.SelectedBidId,
            Source = r.Source.ToString(),
            ProviderServiceId = r.ProviderServiceId,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
        }));
    }

    [HttpGet("bids")]
    public async Task<IActionResult> BidsList(CancellationToken cancellationToken)
    {
        var rows = await db.Bids.AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Take(100)
            .Select(b => new
            {
                b.Id,
                b.ServiceRequestId,
                b.ProviderId,
                b.Amount,
                b.Currency,
                b.PriceText,
                b.Description,
                b.AvailableAt,
                b.Status,
                b.CreatedAt,
                b.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(b => new BidResponseDto
        {
            Id = b.Id,
            ServiceRequestId = b.ServiceRequestId,
            ProviderId = b.ProviderId,
            Amount = b.Amount,
            Currency = b.Currency,
            PriceText = b.PriceText,
            Description = b.Description,
            AvailableAt = b.AvailableAt,
            Status = b.Status.ToString(),
            CreatedAt = b.CreatedAt,
            UpdatedAt = b.UpdatedAt,
        }));
    }

    [HttpGet("reviews")]
    public async Task<IActionResult> ReviewsList(CancellationToken cancellationToken)
    {
        var rows = await db.Reviews.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Take(100)
            .Select(r => new
            {
                r.Id,
                r.Rating,
                r.Comment,
                r.ReviewType,
                r.CreatedAt,
                ReviewerDisplayName = r.ReviewerUser.DisplayName,
                r.ServiceRequestId,
            })
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(r => new ReviewResponseDto
        {
            Id = r.Id,
            Rating = r.Rating,
            Comment = r.Comment,
            ReviewType = r.ReviewType.ToString(),
            CreatedAt = r.CreatedAt,
            ReviewerDisplayName = r.ReviewerDisplayName,
            ServiceRequestId = r.ServiceRequestId,
        }));
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications(CancellationToken cancellationToken)
    {
        var rows = await db.NotificationLogs.AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .Select(n => new
            {
                n.Id,
                n.EventType,
                n.Status,
                n.CreatedAt,
                n.SentAt,
                n.ErrorMessage,
            })
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(n => new
        {
            n.Id,
            n.EventType,
            type = n.EventType,
            status = n.Status.ToString(),
            n.CreatedAt,
            n.SentAt,
            n.ErrorMessage,
        }));
    }
}
