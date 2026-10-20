using System.Security.Claims;

namespace LocalGo.Infrastructure.Auth;

public static class AdminAccess
{
    public const string RoleValue = "Admin";

    public static bool IsAdmin(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return user.IsInRole(RoleValue)
            || user.HasClaim(ClaimTypes.Role, RoleValue)
            || user.HasClaim("role", RoleValue)
            || user.HasClaim("systemRole", RoleValue);
    }
}
