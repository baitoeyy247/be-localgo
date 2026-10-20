using LocalGo.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LocalGo.Api.Auth;

public sealed class RequireAdminAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!AdminAccess.IsAdmin(context.HttpContext.User))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        }

        return Task.CompletedTask;
    }
}
