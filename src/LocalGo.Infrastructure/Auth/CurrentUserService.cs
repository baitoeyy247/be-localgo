using System.Security.Claims;
using LocalGo.Application.Abstractions.Auth;
using LocalGo.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace LocalGo.Infrastructure.Auth;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            var sub = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name)
                ?? httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => UserId.HasValue;

    public bool IsAdmin =>
        httpContextAccessor.HttpContext?.User.IsInRole(SystemRole.Admin.ToString()) == true
        || httpContextAccessor.HttpContext?.User.FindFirstValue("systemRole") == SystemRole.Admin.ToString();
}
