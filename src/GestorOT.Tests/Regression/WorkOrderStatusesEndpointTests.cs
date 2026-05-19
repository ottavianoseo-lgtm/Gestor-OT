using GestorOT.Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace GestorOT.Tests.Regression;

public class WorkOrderStatusesEndpointTests
{
    [Fact]
    public void WorkOrderStatusesController_Route_IsWorkOrderStatuses()
    {
        var controllerType = typeof(WorkOrderStatusesController);

        var routeAttr = controllerType.GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .FirstOrDefault() as RouteAttribute;

        Assert.NotNull(routeAttr);
        Assert.Equal("api/[controller]", routeAttr.Template);

        // With [controller] token, the effective route for WorkOrderStatusesController
        // is "api/WorkOrderStatuses" (ASP.NET Core strips "Controller" suffix).
        // Confirming the template uses the standard convention.
        Assert.Contains("[controller]", routeAttr.Template);
    }

    [Fact]
    public void WorkOrderStatusesController_UsesApiControllerAttribute()
    {
        var controllerType = typeof(WorkOrderStatusesController);

        var apiAttr = controllerType.GetCustomAttributes(typeof(ApiControllerAttribute), inherit: false)
            .FirstOrDefault();

        Assert.NotNull(apiAttr);
    }
}
