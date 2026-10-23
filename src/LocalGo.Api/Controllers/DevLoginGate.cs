namespace LocalGo.Api.Controllers;

/// <summary>R45: single switch for every test-only auth/data endpoint. Off when ALLOW_DEV_LOGIN=false or outside Development/Staging.</summary>
public static class DevLoginGate
{
    public const string ConfigKey = "Auth:AllowDevLogin";

    public static bool IsEnabled(IConfiguration config, IWebHostEnvironment env) =>
        (env.IsDevelopment() || env.IsStaging())
        && !string.Equals(config[ConfigKey], "false", StringComparison.OrdinalIgnoreCase);
}
