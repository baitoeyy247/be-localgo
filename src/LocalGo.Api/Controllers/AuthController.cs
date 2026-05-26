using LocalGo.Application.Auth;
using LocalGo.Application.Dtos;
using LocalGo.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalGo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    DevAuthService devAuth,
    AuthAppService authApp,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("line/login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> LineLogin([FromBody] LineLoginRequest request, CancellationToken cancellationToken)
    {
        var response = await authApp.LineLoginAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("dev/login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DevLogin([FromBody] DevLoginRequest request, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var response = await devAuth.LoginAsync(request, cancellationToken);
        return Ok(response);
    }
}
