using LocalGo.Application.Abstractions.Auth;
using LocalGo.Application.Auth;
using LocalGo.Application.Dtos;
using LocalGo.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalGo.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController(ICurrentUserService currentUser, AuthAppService authApp) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var profile = await authApp.GetMeAsync(userId, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPatch("active-role")]
    public async Task<IActionResult> UpdateActiveRole([FromBody] UpdateActiveRoleRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await authApp.UpdateActiveRoleAsync(userId, request, cancellationToken));
    }

    [HttpPost("accept-privacy-notice")]
    public async Task<IActionResult> AcceptPrivacy([FromBody] AcceptPrivacyRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await authApp.AcceptPrivacyAsync(userId, request, cancellationToken));
    }
}
