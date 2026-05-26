namespace LocalGo.Infrastructure.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "LocalGo";
    public string Audience { get; set; } = "LocalGo";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiresDays { get; set; } = 7;
}
