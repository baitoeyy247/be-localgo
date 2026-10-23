namespace LocalGo.Application.Services;

public static class StartupMaintenance
{
    /// <summary>Startup seed/repair runs only with an explicit opt-in flag and never in Production.</summary>
    public static bool ShouldRunStartupRepair(string? environmentName, string? flag) =>
        !string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase)
        && string.Equals(flag?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
}
