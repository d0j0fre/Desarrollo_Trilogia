using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Proyecto_Final.Services.ChatV2;

/// <summary>Preserves authorized history while preventing dual writes during the explicit V2 opt-in.</summary>
public sealed class LegacyChatWriteGuard(IConfiguration configuration) : IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (!configuration.GetValue<bool>("ChatV2:PersonalSqlEnabled")
            && !configuration.GetValue<bool>("ChatV2:LegacyReadOnly")) return;
        var request = context.HttpContext.Request;
        // Only the canonical personal archive is available during the P transition.
        // Old department/search readers accept an administrative override and lack the
        // approved D/C authorization source. Keep those routes closed as well as writers.
        if ((HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            && context.ActionDescriptor is ControllerActionDescriptor { ActionName: "GetMessages" }) return;
        context.Result = new ObjectResult(new { code = "legacy_write_disabled", message = "Esta operación de chat no está disponible." })
        { StatusCode = StatusCodes.Status503ServiceUnavailable };
    }
    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
