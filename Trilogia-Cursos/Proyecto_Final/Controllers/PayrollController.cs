using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers;

[AdminAuthorize("Planilla", "PLANILLA_VER")]
public sealed class PayrollController : Controller
{
    private readonly IPayrollService _payroll; private readonly ILogger<PayrollController> _logger;
    public PayrollController(IPayrollService payroll, ILogger<PayrollController> logger){_payroll=payroll;_logger=logger;}

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)=>View(new PayrollIndexViewModel{Calculos=await _payroll.ListAsync(cancellationToken)});

    [HttpPost,ValidateAntiForgeryToken,AdminAuthorize("Planilla","PLANILLA_CONFIGURAR")]
    public async Task<IActionResult> ConfigureRule(PayrollRuleFormViewModel model,CancellationToken cancellationToken)=>await ExecuteAsync(model,()=>_payroll.SaveRuleAsync(model,UserId(),UserName(),cancellationToken),"Regla versionada guardada.");

    [HttpPost,ValidateAntiForgeryToken,AdminAuthorize("Planilla","PLANILLA_GESTIONAR")]
    public async Task<IActionResult> CreatePeriod(PayrollPeriodFormViewModel model,CancellationToken cancellationToken)=>await ExecuteAsync(model,()=>_payroll.CreatePeriodAsync(model,UserId(),UserName(),cancellationToken),"Periodo creado.");

    [HttpPost,ValidateAntiForgeryToken,AdminAuthorize("Planilla","PLANILLA_CALCULAR")]
    public async Task<IActionResult> Calculate(PayrollCalculationRequestViewModel model,CancellationToken cancellationToken)=>await ExecuteAsync(model,()=>_payroll.CalculateAsync(model,UserId(),UserName(),cancellationToken),"Planilla calculada de forma idempotente.");

    [HttpPost,ValidateAntiForgeryToken,AdminAuthorize("Planilla","PLANILLA_APROBAR")]
    public Task<IActionResult> Approve(PayrollStateChangeViewModel model,CancellationToken cancellationToken)=>ChangeStateAsync(model,"Aprobada",cancellationToken);

    [HttpPost,ValidateAntiForgeryToken,AdminAuthorize("Planilla","PLANILLA_PAGAR")]
    public Task<IActionResult> Pay(PayrollStateChangeViewModel model,CancellationToken cancellationToken)=>ChangeStateAsync(model,"Pagada",cancellationToken);

    [HttpPost,ValidateAntiForgeryToken,AdminAuthorize("Planilla","PLANILLA_REVERTIR")]
    public Task<IActionResult> Revert(PayrollStateChangeViewModel model,CancellationToken cancellationToken)=>ChangeStateAsync(model,"Revertida",cancellationToken);

    private Task<IActionResult> ChangeStateAsync(PayrollStateChangeViewModel model,string state,CancellationToken cancellationToken)
    {
        model.Estado=state;
        return ExecuteAsync(model,()=>_payroll.ChangeStateAsync(model,UserId(),UserName(),cancellationToken),"Estado de planilla actualizado.");
    }

    private async Task<IActionResult> ExecuteAsync(object model,Func<Task> action,string success)
    {
        if(!ModelState.IsValid){TempData["ErrorMessage"]="La solicitud no es válida.";return RedirectToAction(nameof(Index));}
        try{await action();TempData["SuccessMessage"]=success;}
        catch(Exception exception){_logger.LogWarning(exception,"Operación de planilla rechazada.");TempData["ErrorMessage"]="No fue posible completar la operación de planilla.";}
        return RedirectToAction(nameof(Index));
    }
    private int UserId()=>HttpContext.Session.GetInt32("UserId")??0; private string UserName()=>HttpContext.Session.GetString("UserFullName")??"Usuario";
}
