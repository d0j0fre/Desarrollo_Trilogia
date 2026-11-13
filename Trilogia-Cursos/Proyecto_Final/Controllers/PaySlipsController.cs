using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;
using System.Text;

namespace Proyecto_Final.Controllers;

[AdminAuthorize("Planilla", "PLANILLA_BOLETAS_GESTIONAR")]
public sealed class PaySlipsController : Controller
{
    private readonly IPaySlipService _service;
    private readonly IPaySlipDeliveryCoordinator _delivery;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaySlipsController> _logger;

    public PaySlipsController(IPaySlipService service, IPaySlipDeliveryCoordinator delivery, IConfiguration configuration, ILogger<PaySlipsController> logger)
    {
        _service=service; _delivery=delivery; _configuration=configuration; _logger=logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await _service.ListAsync(UserId(), true, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Details(long id,CancellationToken cancellationToken)
    {
        var model=await _service.GetAsync(id,UserId(),true,cancellationToken);
        return model is null?NotFound():View("~/Views/MyPaySlips/Details.cshtml",model);
    }

    [HttpGet]
    public async Task<IActionResult> Download(long id,CancellationToken cancellationToken)
    {
        var model=await _service.GetAsync(id,UserId(),true,cancellationToken);
        if(model is null)return NotFound();
        Response.Headers.CacheControl = "no-store";
        return File(PaySlipPdf.Build(model),"application/pdf",$"boleta-{id}.pdf");
    }

    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(PaySlipSendViewModel model,CancellationToken cancellationToken)
    {
        if(model.CalculoId<=0||model.IdempotencyKey==Guid.Empty)return BadRequest();
        // G2-27: si falta la configuración se explica el motivo, sin registrar un intento de envío fallido.
        var problem=PaySlipDeliveryDiagnostics.GetConfigurationProblem(_configuration);
        if(problem is not null)
        {
            _logger.LogWarning("No se notificó la boleta {CalculationId}: {Problem}",model.CalculoId,problem);
            TempData["ErrorMessage"]=problem;
            return RedirectToAction(nameof(Index));
        }
        try
        {
            var baseUri=new Uri(_configuration["PaySlips:PublicBaseUrl"]!.Trim());
            var relative=Url.Action("Details","MyPaySlips",new{id=model.CalculoId})??throw new InvalidOperationException("No se pudo generar el enlace de boleta.");
            await _delivery.SendAsync(model.CalculoId,model.IdempotencyKey,UserId(),UserName(),new Uri(baseUri,relative),cancellationToken);
            TempData["SuccessMessage"]="Notificación de boleta procesada.";
        }
        catch(Exception exception)
        {
            _logger.LogWarning(exception,"Falló la notificación de la boleta {CalculationId}.",model.CalculoId);
            TempData["ErrorMessage"]="La boleta permanece disponible, pero no fue posible confirmar el envío del correo.";
        }
        return RedirectToAction(nameof(Index));
    }

    private int UserId()=>HttpContext.Session.GetInt32("UserId")??0;
    private string UserName()=>HttpContext.Session.GetString("UserFullName")??"Usuario";
}
