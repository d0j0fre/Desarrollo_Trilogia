using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers;

[AdminAuthorize("Planilla", "PLANILLA_VER")]
public sealed class PayrollController : Controller
{
    private const string TechnicalFailureMessage = "No fue posible procesar la operación de planilla. El incidente quedó registrado; intente de nuevo en unos minutos.";

    private readonly IPayrollService _payroll; private readonly ILogger<PayrollController> _logger;
    public PayrollController(IPayrollService payroll, ILogger<PayrollController> logger){_payroll=payroll;_logger=logger;}

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            return View(new PayrollIndexViewModel
            {
                Calculos = await _payroll.ListAsync(cancellationToken),
                Reglas = await _payroll.ListRulesAsync(cancellationToken),
                PeriodosAbiertos = await _payroll.ListOpenPeriodsAsync(cancellationToken),
                Empleados = await _payroll.ListActiveEmployeesAsync(cancellationToken)
            });
        }
        catch (Exception exception)
        {
            return ServiceUnavailable(exception, "cargar los cálculos de planilla");
        }
    }

    [HttpPost,ValidateAntiForgeryToken,AdminAuthorize("Planilla","PLANILLA_CONFIGURAR")]
    public async Task<IActionResult> ConfigureRule(PayrollRuleFormViewModel model,CancellationToken cancellationToken)=>await ExecuteAsync(()=>_payroll.SaveRuleAsync(model,UserId(),UserName(),cancellationToken),"Regla versionada guardada.");

    [HttpPost,ValidateAntiForgeryToken,AdminAuthorize("Planilla","PLANILLA_GESTIONAR")]
    public async Task<IActionResult> CreatePeriod(PayrollPeriodFormViewModel model,CancellationToken cancellationToken)=>await ExecuteAsync(()=>_payroll.CreatePeriodAsync(model,UserId(),UserName(),cancellationToken),"Periodo creado.");

    [HttpPost,ValidateAntiForgeryToken,AdminAuthorize("Planilla","PLANILLA_CALCULAR")]
    public async Task<IActionResult> Calculate(PayrollCalculationRequestViewModel model,CancellationToken cancellationToken)=>await ExecuteAsync(()=>_payroll.CalculateAsync(model,UserId(),UserName(),cancellationToken),"Planilla calculada de forma idempotente.");

    [HttpPost,ValidateAntiForgeryToken,AdminAuthorize("Planilla","PLANILLA_APROBAR")]
    public Task<IActionResult> Approve(PayrollStateChangeViewModel model,CancellationToken cancellationToken)=>ChangeStateAsync(model,"Aprobada",cancellationToken);

    [HttpPost,ValidateAntiForgeryToken,AdminAuthorize("Planilla","PLANILLA_PAGAR")]
    public Task<IActionResult> Pay(PayrollStateChangeViewModel model,CancellationToken cancellationToken)=>ChangeStateAsync(model,"Pagada",cancellationToken);

    [HttpPost,ValidateAntiForgeryToken,AdminAuthorize("Planilla","PLANILLA_REVERTIR")]
    public Task<IActionResult> Revert(PayrollStateChangeViewModel model,CancellationToken cancellationToken)=>ChangeStateAsync(model,"Revertida",cancellationToken);

    private Task<IActionResult> ChangeStateAsync(
    PayrollStateChangeViewModel model,
    string state,
    CancellationToken cancellationToken)
    {
        // El estado no debe venir controlado por el navegador.
        // Cada acción determina el estado permitido desde el servidor.
        model.Estado = state;

        // El model binder registra un error porque el formulario no envía Estado.
        // Se elimina únicamente ese error y se conserva el resto de validaciones.
        ModelState.Remove(nameof(PayrollStateChangeViewModel.Estado));

        return ExecuteAsync(
            () => _payroll.ChangeStateAsync(
                model,
                UserId(),
                UserName(),
                cancellationToken
            ),
            "Estado de planilla actualizado."
        );
    }

    /// <summary>
    /// Toda acción de planilla vuelve a la pantalla: con el mensaje verde si funcionó y con uno rojo y legible
    /// si no (G1-04). Las reglas de negocio de los procedimientos (THROW ≥ 50000) se muestran tal cual.
    /// </summary>
    private async Task<IActionResult> ExecuteAsync(Func<Task> action,string success)
    {
        if (!ModelState.IsValid)
        {
            var messages = ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "El valor recibido tiene un formato inválido." : error.ErrorMessage.Trim())
                .Distinct()
                .ToArray();

            _logger.LogWarning("Solicitud de planilla inválida: {ValidationErrors}", string.Join(" | ", messages));
            TempData["ErrorMessage"] = messages.Length == 0
                ? "La solicitud no es válida. Revise los datos e intente de nuevo."
                : "La solicitud no es válida: " + string.Join(" ", messages);
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await action();
            TempData["SuccessMessage"]=success;
        }
        catch(PayrollConfigurationException exception)
        {
            _logger.LogWarning(exception,"Configuración de planilla incompleta. TraceId {TraceId}",HttpContext.TraceIdentifier);
            TempData["ErrorMessage"] = exception.Message;
        }
        catch(Exception exception) when(BusinessErrors.TryGetMessage(exception,out _) || BusinessErrors.IsApplicationRule(exception))
        {
            _logger.LogWarning(exception,"Regla de planilla rechazada. TraceId {TraceId}",HttpContext.TraceIdentifier);
            TempData["ErrorMessage"] = BusinessErrors.TryGetMessage(exception,out var message) ? message : exception.Message;
        }
        catch(Exception exception)
        {
            _logger.LogError(exception,"Falla técnica al procesar la operación de planilla. TraceId {TraceId}",HttpContext.TraceIdentifier);
            TempData["ErrorMessage"] = TechnicalFailureMessage;
        }

        return RedirectToAction(nameof(Index));
    }
    private int UserId()=>HttpContext.Session.GetInt32("UserId")??0; private string UserName()=>HttpContext.Session.GetString("UserFullName")??"Usuario";
    private ViewResult ServiceUnavailable(Exception exception,string operation)
    {
        var traceId=HttpContext.TraceIdentifier;
        _logger.LogError(exception,"Falla técnica al {Operation}. TraceId {TraceId}",operation,traceId);
        Response.StatusCode=StatusCodes.Status503ServiceUnavailable;
        ViewData["TraceId"]=traceId;
        return View("ServiceUnavailable");
    }
}
