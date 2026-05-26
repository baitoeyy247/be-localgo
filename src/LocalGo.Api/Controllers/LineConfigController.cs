using LocalGo.Infrastructure.Line;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LocalGo.Api.Controllers;

[ApiController]
[Route("api/config")]
public sealed class LineConfigController(
    IOptions<LineSettings> line,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet("line")]
    [AllowAnonymous]
    public IActionResult GetLineConfig()
    {
        var settings = line.Value;
        var liffId = settings.LiffId;
        var hasChannel = !string.IsNullOrWhiteSpace(settings.ChannelId)
            && !settings.ChannelId.StartsWith("YOUR_", StringComparison.Ordinal);
        var webOrigin = configuration["Cors:NgrokPublicUrl"]?.TrimEnd('/');

        return Ok(new
        {
            channelId = settings.ChannelId,
            liffId,
            liffEntryUrl = string.IsNullOrWhiteSpace(liffId) ? null : $"https://liff.line.me/{liffId}/",
            expectedWebOrigin = string.IsNullOrWhiteSpace(webOrigin) ? null : $"{webOrigin}/",
            lineLoginReady = hasChannel && !string.IsNullOrWhiteSpace(liffId),
            messagingReady = !string.IsNullOrWhiteSpace(settings.MessagingAccessToken)
                && !settings.MessagingAccessToken.StartsWith("YOUR_", StringComparison.Ordinal),
        });
    }
}
