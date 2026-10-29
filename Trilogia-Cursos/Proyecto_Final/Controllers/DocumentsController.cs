using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers;

[SessionAuthorize]
[DocumentAccessExceptionFilter]
public sealed class DocumentsController : Controller
{
    private static readonly string[] AllowedStatuses = ["Vigente", "Suspendido", "Archivado"];
    private readonly IDocumentManagementDbService _documents;
    private readonly IPrivateFileStorageService _storage;
    private readonly IDocumentAlertService _alerts;
    private readonly AdminDbService _admin;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(IDocumentManagementDbService documents, IPrivateFileStorageService storage,
        IDocumentAlertService alerts, AdminDbService admin, ILogger<DocumentsController> logger)
    {
        _documents = documents; _storage = storage; _alerts = alerts; _admin = admin; _logger = logger;
    }

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        // Per-document actions come from SQL rights, never from a caller role or this navigation flag.
        ViewData["DocumentAccessScope"] = context.Exception is null && context.Result is ViewResult
            && HttpContext.Items[OriginalLoginSession.ValidatedContextItem] is OriginalLoginContext { HasCanonicalLifetime: true };
        base.OnActionExecuted(context);
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] DocumentFilterViewModel filter)
    {
        var businessDate = _alerts.CurrentBusinessDate();
        var options = await _documents.GetOptionsAsync();
        var summary = await _documents.GetSummaryAsync(businessDate, _alerts.DefaultWarningDays);
        return View(new DocumentIndexViewModel
        {
            Filter = filter, Documents = await _documents.ListAsync(filter, businessDate, _alerts.DefaultWarningDays),
            Types = options.Types, Departments = options.Departments,
            CurrentCount = summary.Current, WarningCount = summary.Warning, ExpiredCount = summary.Expired, NoExpirationCount = summary.NoExpiration
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0) return NotFound();
        var model = await _documents.GetDetailsAsync(id, _alerts.CurrentBusinessDate(), _alerts.DefaultWarningDays);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        if (id <= 0) return NotFound();
        var model = await _documents.GetDetailsAsync(id, _alerts.CurrentBusinessDate(), _alerts.DefaultWarningDays);
        return model is null ? NotFound() : View("Details", model);
    }

    [HttpGet]
    public async Task<IActionResult> History(int id)
    {
        if (id <= 0) return NotFound();
        var model = await _documents.GetDetailsAsync(id, _alerts.CurrentBusinessDate(), _alerts.DefaultWarningDays);
        if (model is null) return NotFound();
        ViewData["FocusHistory"] = true;
        return View("Details", model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadOptionsAsync();
        return View(new DocumentFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("private-file-upload")]
    public async Task<IActionResult> Create(DocumentFormViewModel model, CancellationToken cancellationToken)
    {
        ValidateDocument(model, requireFile: true);
        if (!ModelState.IsValid) { await LoadOptionsAsync(); return InvalidDocumentForm(model); }
        StagedPrivateFile? staged = null;
        var documentId = 0;
        var committed = false;
        var ready = false;
        var publicationAttempted = false;
        try
        {
            staged = await _storage.StageAsync(model.File, PrivateStorageArea.Documents, cancellationToken)
                ?? throw new PrivateFileValidationException("Seleccione un archivo.");
            documentId = await _documents.CreatePendingAsync(model, staged, UserId(), UserName());
            await _storage.CommitAsync(staged, cancellationToken);
            committed = true;
            publicationAttempted = true;
            await _documents.MarkDocumentReadyAsync(documentId, UserId(), UserName());
            ready = true;
            TempData["SuccessMessage"] = "Documento almacenado de forma privada.";
            return RedirectToAction(nameof(Details), new { id = documentId });
        }
        catch (DocumentPendingCommitUnconfirmedException exception)
        {
            return await RecoverPendingCommitAsync(exception, staged, null, cancellationToken);
        }
        catch (PrivateFileValidationException exception)
        {
            ModelState.AddModelError(nameof(model.File), exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "No se pudo crear el documento privado.");
            ModelState.AddModelError(string.Empty, "No fue posible guardar el documento.");
        }
        if (staged is not null && committed && publicationAttempted && !ready)
        {
            var confirmed = !cancellationToken.IsCancellationRequested
                && await PublicationConfirmedAsync(documentId, null, staged);
            TempData[confirmed ? "SuccessMessage" : "ErrorMessage"] = confirmed
                ? "Documento conservado; su publicación quedó confirmada."
                : "La confirmación de publicación sigue pendiente. El archivo se conservó; revisá el historial antes de repetir la carga.";
            if (!confirmed) await PreserveUploadAsync(staged, documentId, null);
            return RedirectToAction(nameof(Details), new { id = documentId });
        }
        if (staged is not null && documentId > 0 && !ready)
        {
            await PreserveUploadAsync(staged, documentId, null);
            TempData["ErrorMessage"] = "El archivo se conservó para revisar una carga pendiente. No repitás la carga hasta comprobar su estado.";
            return RedirectToAction(nameof(Details), new { id = documentId });
        }
        if (staged is not null && !ready)
        {
            if (committed) await _storage.DeleteCommittedAsync(PrivateStorageArea.Documents, staged.StorageKey);
            else await _storage.DeleteStageAsync(staged);
        }
        await LoadOptionsAsync();
        return InvalidDocumentForm(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await _documents.GetFormAsync(id);
        if (model is null) return NotFound();
        await LoadOptionsAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DocumentFormViewModel model)
    {
        ValidateDocument(model, requireFile: false);
        if (!ModelState.IsValid) { await LoadOptionsAsync(); return InvalidDocumentForm(model); }
        try
        {
            await _documents.UpdateMetadataAsync(model, UserId(), UserName());
            TempData["SuccessMessage"] = "Documento actualizado.";
            return RedirectToAction(nameof(Details), new { id = model.DocumentId });
        }
        catch (Exception exception)
        {
            HandleDatabaseError(exception, "actualizar el documento");
            await LoadOptionsAsync();
            return InvalidDocumentForm(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("private-file-upload")]
    public async Task<IActionResult> ReplaceFile(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        StagedPrivateFile? staged = null;
        var versionId = 0;
        var committed = false;
        var ready = false;
        var publicationAttempted = false;
        try
        {
            staged = await _storage.StageAsync(file, PrivateStorageArea.Documents, cancellationToken)
                ?? throw new PrivateFileValidationException("Seleccione un archivo.");
            versionId = await _documents.ReplaceFilePendingAsync(id, staged, UserId(), UserName());
            await _storage.CommitAsync(staged, cancellationToken);
            committed = true;
            publicationAttempted = true;
            await _documents.MarkVersionReadyAsync(id, versionId, UserId(), UserName());
            ready = true;
            TempData["SuccessMessage"] = "Nueva versión almacenada.";
        }
        catch (DocumentPendingCommitUnconfirmedException exception)
        {
            return await RecoverPendingCommitAsync(exception, staged, id, cancellationToken);
        }
        catch (PrivateFileValidationException exception) { TempData["ErrorMessage"] = exception.Message; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "No se pudo reemplazar el archivo del documento {DocumentId}.", id);
            TempData["ErrorMessage"] = "No fue posible reemplazar el archivo.";
        }
        if (staged is not null && committed && publicationAttempted && !ready)
        {
            var confirmed = !cancellationToken.IsCancellationRequested
                && await PublicationConfirmedAsync(id, versionId, staged);
            TempData[confirmed ? "SuccessMessage" : "ErrorMessage"] = confirmed
                ? "Nueva versión conservada; su publicación quedó confirmada."
                : "La confirmación de la versión sigue pendiente. El archivo se conservó; revisá el historial antes de repetir la carga.";
            if (!confirmed) await PreserveUploadAsync(staged, id, versionId);
            return RedirectToAction(nameof(Details), new { id });
        }
        if (staged is not null && versionId > 0 && !ready)
        {
            await PreserveUploadAsync(staged, id, versionId);
            TempData["ErrorMessage"] = "La versión pendiente y el archivo se conservaron para revisión. No repitás la carga hasta comprobar su estado.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (staged is not null && !ready)
        {
            if (committed) await _storage.DeleteCommittedAsync(PrivateStorageArea.Documents, staged.StorageKey);
            else await _storage.DeleteStageAsync(staged);
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    [EnableRateLimiting("sensitive-read")]
    public async Task<IActionResult> Download(int id, int? versionId, CancellationToken cancellationToken)
    {
        var metadata = await _documents.GetAuthorizedFileAsync(id, versionId, UserId(), await CanManageAsync());
        if (metadata is null) return NotFound();
        var stream = await _storage.OpenReadAsync(PrivateStorageArea.Documents, metadata.StorageKey, cancellationToken);
        if (stream is null) { _logger.LogWarning("Archivo privado ausente para documento {DocumentId}.", id); return NotFound(); }
        Response.Headers.CacheControl = "private, no-store, max-age=0";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentDisposition = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(metadata.OriginalName)}";
        return File(stream, metadata.MimeType);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int id, bool active)
    {
        try
        {
            await _documents.SetActiveAsync(id, active, UserId(), UserName());
            TempData["SuccessMessage"] = active ? "Documento reactivado." : "Documento desactivado sin borrar su historial.";
        }
        catch (Exception exception) { HandleDatabaseError(exception, "cambiar el estado del documento"); }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Alerts([FromQuery] DocumentAlertFilterViewModel filter)
    {
        var date = _alerts.CurrentBusinessDate();
        var options = await _documents.GetOptionsAsync();
        var summary = await _documents.GetAlertSummaryAsync(date);
        return View(new DocumentAlertsIndexViewModel
        {
            Filter = filter, Alerts = await _documents.ListAlertsAsync(filter, date), Departments = options.Departments,
            ActiveCount = summary.Active, ExpiredCount = summary.Expired
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("document-alert-generation")]
    public async Task<IActionResult> GenerateAlerts(CancellationToken cancellationToken)
    {
        try
        {
            var count = await _alerts.GenerateAsync(UserId(), cancellationToken);
            TempData["SuccessMessage"] = $"Proceso completado: {count} alerta(s) nueva(s) o pendiente(s) de notificación.";
        }
        catch (Exception exception) { HandleDatabaseError(exception, "generar alertas"); }
        return RedirectToAction(nameof(Alerts));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAlertHandled(int id)
    {
        try { await _documents.MarkAlertHandledAsync(id, UserId()); TempData["SuccessMessage"] = "Alerta marcada como atendida."; }
        catch (Exception exception) { HandleDatabaseError(exception, "atender la alerta"); }
        return RedirectToAction(nameof(Alerts));
    }

    private void ValidateDocument(DocumentFormViewModel model, bool requireFile)
    {
        if (requireFile && (model.File is null || model.File.Length == 0)) ModelState.AddModelError(nameof(model.File), "Seleccione un archivo.");
        if (!AllowedStatuses.Contains(model.Status)) ModelState.AddModelError(nameof(model.Status), "El estado no es válido.");
        if (!model.DoesNotExpire && !model.ExpirationDate.HasValue) ModelState.AddModelError(nameof(model.ExpirationDate), "Indique la fecha de vencimiento o marque que no vence.");
        if (model.IssueDate.HasValue && model.ExpirationDate.HasValue && model.ExpirationDate.Value.Date < model.IssueDate.Value.Date)
            ModelState.AddModelError(nameof(model.ExpirationDate), "La fecha de vencimiento no puede ser anterior a la emisión.");
    }

    private async Task LoadOptionsAsync()
    {
        var options = await _documents.GetOptionsAsync(); ViewBag.Types = options.Types; ViewBag.Departments = options.Departments;
    }
    private int UserId() => HttpContext.Session.GetInt32("UserId") ?? 0;
    private string UserName() => HttpContext.Session.GetString("UserFullName") ?? "Usuario";
    private static Task<bool> CanManageAsync() => Task.FromResult(false); // A caller flag never grants ownership.
    private async Task<IActionResult> RecoverPendingCommitAsync(DocumentPendingCommitUnconfirmedException outcome,
        StagedPrivateFile? staged, int? expectedDocument, CancellationToken cancellationToken)
    {
        // Only a server-captured result can enter this path; never replay the metadata INSERT.
        var sameAttempt = staged is not null && staged.StorageKey == outcome.StorageKey
            && (expectedDocument is null ? outcome.VersionId is null
                : expectedDocument == outcome.DocumentId && outcome.VersionId is > 0);
        if (sameAttempt && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _storage.CommitAsync(staged!, cancellationToken);
                try
                {
                    if (outcome.VersionId is int version)
                        await _documents.MarkVersionReadyAsync(outcome.DocumentId, version, UserId(), UserName());
                    else await _documents.MarkDocumentReadyAsync(outcome.DocumentId, UserId(), UserName());
                }
                catch (Exception exception)
                { _logger.LogWarning("Publicación documental pendiente; tipo {Type}.", exception.GetType().Name); }
                if (!cancellationToken.IsCancellationRequested
                    && await PublicationConfirmedAsync(outcome.DocumentId, outcome.VersionId, staged!))
                {
                    TempData["SuccessMessage"] = "El archivo se conservó y su publicación quedó confirmada, sin repetir la carga.";
                    return RedirectToAction(nameof(Details), new { id = outcome.DocumentId });
                }
            }
            catch (Exception exception)
            { _logger.LogWarning("Resultado de carga documental sin confirmar; tipo {Type}.", exception.GetType().Name); }
        }
        if (staged is not null && sameAttempt)
        {
            await PreserveUploadAsync(staged, outcome.DocumentId, outcome.VersionId);
        }
        TempData["ErrorMessage"] = "No se confirmó el resultado de la carga. El archivo se conservó; no repitás la carga hasta revisar su estado.";
        return expectedDocument is int id ? RedirectToAction(nameof(Details), new { id }) : RedirectToAction(nameof(Index));
    }
    private async Task PreserveUploadAsync(StagedPrivateFile staged, int documentId, int? versionId)
    {
        try { await _storage.PreserveForReconciliationAsync(staged, documentId, versionId, UserId()); }
        catch (Exception exception)
        { _logger.LogWarning("No se pudo registrar conciliación documental; tipo {Type}. Se conservan los archivos.", exception.GetType().Name); }
    }
    private async Task<bool> PublicationConfirmedAsync(int documentId, int? versionId, StagedPrivateFile staged)
    {
        try
        {
            // This receipt returns only Ready versions published by this actor, under
            // current ownership/version permission. It does not authorize a download. A lost
            // acknowledgement is never permission to delete committed storage.
            var receipt = await _documents.GetPublicationReceiptAsync(documentId, versionId, UserId(), staged.StorageKey);
            return receipt is not null && receipt.OwnerId == documentId
                && string.Equals(receipt.StorageKey, staged.StorageKey, StringComparison.Ordinal)
                && (!versionId.HasValue || receipt.VersionId == versionId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("No se pudo acreditar la publicación del documento {DocumentId}; tipo {ExceptionType}. Se conservan archivo y metadata.",
                documentId, exception.GetType().Name);
            return false;
        }
    }
    private Task AuditAsync(string action, string detail) => _admin.CreateAuditLogAsync(UserId(), UserName(),
        HttpContext.Session.GetString("UserEmail"), HttpContext.Session.GetString("UserRole"), action, "Documentos", detail,
        HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    private void HandleDatabaseError(Exception exception, string operation)
    {
        if (exception is SqlException sql && sql.Number >= 50000) _logger.LogWarning(exception, "Regla de negocio al {Operation}.", operation);
        else _logger.LogError(exception, "Error al {Operation}.", operation);
        TempData["ErrorMessage"] = $"No fue posible {operation}.";
    }

    private ViewResult InvalidDocumentForm(DocumentFormViewModel model)
    {
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return View(model);
    }
}
