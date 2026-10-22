using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace LocalGo.Api.Health;

/// <summary>
/// M-N4: Redis is optional (only used for health reporting). Never reports Unhealthy, so it can't
/// degrade /api/health or spam HealthCheckService[103]. Connection attempts are throttled to once per minute.
/// </summary>
public sealed class OptionalRedisHealthCheck(string connectionString, ILogger<OptionalRedisHealthCheck> logger) : IHealthCheck
{
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromMinutes(1);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime _lastProbeUtc = DateTime.MinValue;
    private static string _state = "Unknown";
    private static ConnectionMultiplexer? _mux;

    public static string State => _state;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (DateTime.UtcNow - _lastProbeUtc >= ProbeInterval && await Gate.WaitAsync(0, cancellationToken))
        {
            try
            {
                _lastProbeUtc = DateTime.UtcNow;
                if (_mux is { IsConnected: true })
                {
                    await _mux.GetDatabase().PingAsync();
                    _state = "Healthy";
                }
                else
                {
                    var options = ConfigurationOptions.Parse(connectionString);
                    options.AbortOnConnectFail = true;
                    options.ConnectTimeout = 2000;
                    options.ConnectRetry = 0;
                    _mux?.Dispose();
                    _mux = await ConnectionMultiplexer.ConnectAsync(options);
                    await _mux.GetDatabase().PingAsync();
                    _state = "Healthy";
                }
            }
            catch (Exception ex)
            {
                if (_state != "Unavailable")
                {
                    logger.LogWarning("Redis optional health probe failed ({Type}); reporting Unavailable (non-critical)", ex.GetType().Name);
                }
                _state = "Unavailable";
                _mux?.Dispose();
                _mux = null;
            }
            finally
            {
                Gate.Release();
            }
        }

        return HealthCheckResult.Healthy(
            _state == "Healthy" ? "Redis connected" : "Redis unavailable (optional, non-critical)",
            new Dictionary<string, object> { ["redis"] = _state });
    }
}
