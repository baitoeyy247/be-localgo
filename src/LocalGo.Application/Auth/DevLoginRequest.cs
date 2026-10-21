namespace LocalGo.Application.Auth;

public sealed class DevLoginRequest
{
    public string? DisplayName { get; set; }
    public string? ActiveRole { get; set; }
    public bool AsAdmin { get; set; }

    /// <summary>
    /// Stable test persona key (e.g. req-01). When set, login reuses the same user instead of creating a new one.
    /// </summary>
    public string? PersonaKey { get; set; }
}
