namespace LocalGo.Application.Abstractions.Auth;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
}
