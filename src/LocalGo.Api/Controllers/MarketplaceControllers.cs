using AutoMapper;
using LocalGo.Application.Abstractions.Auth;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Dtos;
using LocalGo.Application.Dtos.Responses;
using LocalGo.Application.Services;
using LocalGo.Domain.Enums;
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
        return provider is null ? NotFound() : Ok(mapper.Map<ProviderResponseDto>(provider));
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
        return Ok(new { appointmentId = appointment.Id, serviceRequestId = appointment.ServiceRequestId });
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
    [HttpPatch("{appointmentId:guid}")]
    public async Task<IActionResult> Update(Guid appointmentId, [FromQuery] string status, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        if (!Enum.TryParse<AppointmentStatus>(status, true, out var parsed))
        {
            return BadRequest();
        }

        var appointment = await appointments.UpdateStatusAsync(userId, appointmentId, parsed, cancellationToken);
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
        return Ok(await reviews.CreateAsync(userId, providerId, request, cancellationToken));
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
public sealed class AdminController(AdminAppService admin, ILocalGoDbContext db) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken) =>
        Ok(await admin.GetDashboardAsync(cancellationToken));

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken cancellationToken) =>
        Ok(await db.Users.AsNoTracking().OrderByDescending(u => u.CreatedAt).Take(100).ToListAsync(cancellationToken));

    [HttpGet("providers")]
    public async Task<IActionResult> Providers(CancellationToken cancellationToken) =>
        Ok(await db.Providers.AsNoTracking().OrderByDescending(p => p.CreatedAt).Take(100).ToListAsync(cancellationToken));

    [HttpGet("service-requests")]
    public async Task<IActionResult> ServiceRequests(CancellationToken cancellationToken) =>
        Ok(await db.ServiceRequests.AsNoTracking().OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync(cancellationToken));

    [HttpGet("bids")]
    public async Task<IActionResult> BidsList(CancellationToken cancellationToken) =>
        Ok(await db.Bids.AsNoTracking().OrderByDescending(b => b.CreatedAt).Take(100).ToListAsync(cancellationToken));

    [HttpGet("reviews")]
    public async Task<IActionResult> ReviewsList(CancellationToken cancellationToken) =>
        Ok(await db.Reviews.AsNoTracking().OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync(cancellationToken));

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications(CancellationToken cancellationToken) =>
        Ok(await db.NotificationLogs.AsNoTracking().OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync(cancellationToken));
}
