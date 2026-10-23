namespace LocalGo.Api.Controllers;

/// <summary>
/// R45/R47: single switch for every test-only auth/data endpoint (dev login, test-data reset/repair).
/// Production-safe defaults: Production is always off; Staging (SIT) needs an explicit ALLOW_DEV_LOGIN=true;
/// Development is on unless ALLOW_DEV_LOGIN=false. A missing env var never enables test features outside Development.
/// </summary>
public static class DevLoginGate
{
    public const string ConfigKey = "Auth:AllowDevLogin";

    public static bool IsEnabled(IConfiguration config, IWebHostEnvironment env) => IsEnabled(config[ConfigKey], env);

    public static bool IsEnabled(string? value, IHostEnvironment env)
    {
        if (env.IsDevelopment())
            return !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
        if (env.IsStaging())
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        return false;
    }

    /// <summary>R47: Server-Timing diagnostics only outside Production.</summary>
    public static bool DiagnosticsEnabled(IHostEnvironment env) => !env.IsProduction();
}
