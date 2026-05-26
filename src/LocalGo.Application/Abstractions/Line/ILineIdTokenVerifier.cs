namespace LocalGo.Application.Abstractions.Line;

public sealed record LineProfile(string LineUserId, string DisplayName, string? PictureUrl);

public interface ILineIdTokenVerifier
{
    Task<LineProfile> VerifyAsync(string idToken, CancellationToken cancellationToken = default);
}
