using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalGo.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(HealthCheckService healthCheckService) : ControllerBase
{
    private static readonly string BuildCommit = ResolveCommit();

    private static string ResolveCommit()
    {
        foreach (var key in new[] { "LOCALGO_COMMIT_SHA", "RENDER_GIT_COMMIT", "GIT_COMMIT" })
        {
            var fromEnv = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(fromEnv) && fromEnv != "unknown")
            {
                return fromEnv.Trim();
            }
        }

        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?.Split('+')[0]
            ?? "unknown";
    }
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
            commit = BuildCommit,
        };

        return apiHealthy
            ? Ok(result)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }
}
