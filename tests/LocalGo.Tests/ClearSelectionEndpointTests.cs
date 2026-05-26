using System.Reflection;
using LocalGo.Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace LocalGo.Tests;

public sealed class ClearSelectionEndpointTests
{
    [Fact]
    public void ServiceRequestsController_exposes_clear_selection_post_route()
    {
        var controllerRoute = typeof(ServiceRequestsController)
            .GetCustomAttribute<RouteAttribute>()?.Template;
        Assert.Equal("api/service-requests", controllerRoute);

        var method = typeof(ServiceRequestsController).GetMethod(
            "ClearSelection",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.NotNull(method);

        var httpPost = method!.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(httpPost);
        Assert.Equal("{requestId:guid}/clear-selection", httpPost!.Template);
    }
}
