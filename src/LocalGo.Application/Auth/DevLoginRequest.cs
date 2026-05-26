namespace LocalGo.Application.Auth;

public sealed class DevLoginRequest
{
    public string? DisplayName { get; set; }
    public string? ActiveRole { get; set; }
    public bool AsAdmin { get; set; }
}
