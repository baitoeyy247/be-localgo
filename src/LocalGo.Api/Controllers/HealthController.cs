using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalGo.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(HealthCheckService healthCheckService, IMemoryCache cache) : ControllerBase
{
    private static readonly TimeSpan FullHealthCacheTtl = TimeSpan.FromSeconds(5);
    private const string FullHealthCacheKey = "health:full";
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
    /// <summary>Fast liveness for FE warm-up — no DB/redis checks.</summary>
    [HttpGet("lite")]
    public IActionResult GetLite() => Ok(new { status = "Healthy", commit = BuildCommit });

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(FullHealthCacheKey, out CachedHealthPayload? cached) && cached is not null)
        {
            return cached.IsHealthy
                ? Ok(cached.Body)
                : StatusCode(StatusCodes.Status503ServiceUnavailable, cached.Body);
        }

        var report = await healthCheckService.CheckHealthAsync(cancellationToken);

        string Status(HealthStatus status) => status == HealthStatus.Healthy ? "Healthy" : "Unhealthy";

        var database = report.Entries.TryGetValue("postgresql", out var pg)
            ? Status(pg.Status)
            : "Unknown";

        // M-N4: Redis is optional/non-critical; it is reported but never degrades overall status.
        var redis = report.Entries.TryGetValue("redis", out var rd)
            ? (rd.Data.TryGetValue("redis", out var st) ? st?.ToString() ?? "Unknown" : Status(rd.Status))
            : "NotConfigured";

        var apiHealthy = database == "Healthy";
        var overall = apiHealthy ? "Healthy" : "Unhealthy";

        var result = new
        {
            status = overall,
            database,
            redis,
            redisCritical = false,
            commit = BuildCommit,
        };

        cache.Set(
            FullHealthCacheKey,
            new CachedHealthPayload(apiHealthy, result),
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = FullHealthCacheTtl });

        return apiHealthy
            ? Ok(result)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }

    private sealed record CachedHealthPayload(bool IsHealthy, object Body);
}
