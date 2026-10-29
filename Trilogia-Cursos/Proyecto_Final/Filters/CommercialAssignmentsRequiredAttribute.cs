using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class CommercialAssignmentsRequiredAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // The feature switch enables a verified SQL contract; every resource still
        // requires its live original login, capability and canonical relationship.
        // An empty relationship list never becomes a blanket authorization.
        var settings = context.HttpContext.RequestServices?.GetService<IConfiguration>();
        if (settings?.GetValue<bool>("Commerce:Enabled") != true || settings.GetValue<bool>("Commerce:OrdersEnabled") != true)
        {
            context.Result = new ViewResult
            {
                ViewName = "~/Views/Shared/CommercialAssignmentsUnavailable.cshtml",
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
        }
    }
}
