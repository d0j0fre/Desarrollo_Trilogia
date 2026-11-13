using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;
using System.Text;

namespace Proyecto_Final.Controllers;

[SessionAuthorize]
public sealed class MyPaySlipsController : Controller
{
    private readonly IPaySlipService _service;
    private readonly IEmployeesService _employees;
    public MyPaySlipsController(IPaySlipService service, IEmployeesService employees){_service=service;_employees=employees;}

    // Las boletas son del personal con expediente de empleado: quien no lo tiene no abre estas
    // pantallas ni escribiendo la dirección (el menú tampoco se las ofrece).
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var portal = await _employees.GetEmployeePortalAsync(UserId());
        if (portal.Perfil is null)
        {
            context.Result = AuthorizationResults.AccessDenied();
            return;
        }
        await next();
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)=>View(await _service.ListAsync(UserId(),false,cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Details(long id,CancellationToken cancellationToken)
    {
        var model=await _service.GetAsync(id,UserId(),false,cancellationToken);
        return model is null?NotFound():View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Download(long id,CancellationToken cancellationToken)
    {
        var model=await _service.GetAsync(id,UserId(),false,cancellationToken);
        if(model is null)return NotFound();
        Response.Headers.CacheControl = "no-store";
        return File(PaySlipPdf.Build(model),"application/pdf",$"mi-boleta-{id}.pdf");
    }

    private int UserId()=>HttpContext.Session.GetInt32("UserId")??0;
}
