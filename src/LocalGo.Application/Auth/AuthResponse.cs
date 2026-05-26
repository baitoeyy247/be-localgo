namespace LocalGo.Application.Auth;

public sealed class AuthResponse
{
    public required string AccessToken { get; set; }
    public required UserProfileDto User { get; set; }
}

public sealed class UserProfileDto
{
    public Guid Id { get; set; }
    public required string DisplayName { get; set; }
    public string? PictureUrl { get; set; }
    public required string SystemRole { get; set; }
    public string? ActiveRole { get; set; }
    public bool PrivacyNoticeAccepted { get; set; }
}
