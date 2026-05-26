using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalGo.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(HealthCheckService healthCheckService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var report = await healthCheckService.CheckHealthAsync(cancellationToken);

        string Status(HealthStatus status) => status == HealthStatus.Healthy ? "Healthy" : "Unhealthy";

        var database = report.Entries.TryGetValue("postgresql", out var pg)
            ? Status(pg.Status)
            : "Unknown";

        var redis = report.Entries.TryGetValue("redis", out var rd)
            ? Status(rd.Status)
            : "Unknown";

        var apiHealthy = database == "Healthy";
        var overall = apiHealthy ? (redis == "Healthy" ? "Healthy" : "Degraded") : "Unhealthy";

        var result = new
        {
            status = overall,
            database,
            redis,
        };

        return apiHealthy
            ? Ok(result)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }
}
