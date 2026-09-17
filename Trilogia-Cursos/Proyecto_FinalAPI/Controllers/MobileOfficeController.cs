using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Proyecto_FinalAPI.Authorization;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services.Mobile;

namespace Proyecto_FinalAPI.Controllers
{
    /// <summary>
    /// Consultas de los perfiles de oficina: caja, facturación, crédito,
    /// soporte y auditoría. Casi todo es de lectura a propósito: registrar
    /// cobros, ajustar créditos o anular facturas son operaciones que exigen el
    /// contexto del escritorio. La única escritura es atender una consulta.
    /// </summary>
    [Route("api/mobile/v1/office")]
    [Authorize]
    [RequirePermission(MobilePermissions.Access)]
    public sealed class MobileOfficeController : MobileControllerBase
    {
        private static readonly HashSet<string> ConsultationStatuses = new(StringComparer.Ordinal) { "Pendiente", "Atendida", "Cerrada" };
        private static readonly HashSet<string> CreditFilters =
            new(StringComparer.Ordinal) { "Activo", "Bloqueado", "Inactivo", "ConDeuda", "SinDeuda", "SinCredito" };

        private readonly IOfficeMobileDbService _office;
        private readonly IMobileAuditService _audit;
        private readonly ILogger<MobileOfficeController> _logger;

        public MobileOfficeController(
            IOfficeMobileDbService office,
            IMobileAuditService audit,
            ILogger<MobileOfficeController> logger)
        {
            _office = office;
            _audit = audit;
            _logger = logger;
        }

        // ── Caja ────────────────────────────────────────────────────────────

        [HttpGet("settlements")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.Settlements)]
        public Task<IActionResult> Settlements([FromQuery] string? estado, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar liquidaciones", async () =>
                Ok(await _office.GetSettlementsAsync(estado, cancellationToken)));

        [HttpGet("settlements/{routeId:int}")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.Settlements)]
        public Task<IActionResult> Settlement(int routeId, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar liquidación", async () =>
            {
                var settlement = await _office.GetSettlementAsync(routeId, cancellationToken);
                return settlement is null ? NotFoundOrForbidden() : Ok(settlement);
            });

        // ── Facturación ─────────────────────────────────────────────────────

        [HttpGet("invoices")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.InvoicesView)]
        public Task<IActionResult> Invoices(CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar facturas", async () => Ok(await _office.GetInvoicesAsync(cancellationToken)));

        [HttpGet("invoices/{invoiceId:int}")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.InvoicesView)]
        public Task<IActionResult> Invoice(int invoiceId, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar factura", async () =>
            {
                var invoice = await _office.GetInvoiceAsync(invoiceId, cancellationToken);
                return invoice is null ? NotFoundOrForbidden() : Ok(invoice);
            });

        // ── Crédito ─────────────────────────────────────────────────────────

        [HttpGet("credit")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.CreditView)]
        public Task<IActionResult> Credits([FromQuery] string? buscar, [FromQuery] string? estado, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(estado) && !CreditFilters.Contains(estado))
                return Task.FromResult(InvalidRequest("El filtro indicado no es válido."));

            return GuardAsync(_logger, "consultar créditos", async () =>
                Ok(await _office.GetCreditsAsync(buscar, estado, cancellationToken)));
        }

        [HttpGet("credit/{clientId:int}")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.CreditView)]
        public Task<IActionResult> Credit(int clientId, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar crédito de cliente", async () =>
            {
                var credit = await _office.GetCreditAsync(clientId, cancellationToken);
                return credit is null ? NotFoundOrForbidden() : Ok(credit);
            });

        // ── Soporte ─────────────────────────────────────────────────────────

        [HttpGet("consultations")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.ConsultationsView)]
        public Task<IActionResult> Consultations([FromQuery] string? estado, [FromQuery] string? buscar, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar consultas", async () =>
                Ok(await _office.GetConsultationsAsync(estado, buscar, cancellationToken)));

        [HttpGet("consultations/{consultationId:int}")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.ConsultationsView)]
        public Task<IActionResult> Consultation(int consultationId, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar consulta", async () =>
            {
                var consultation = await _office.GetConsultationAsync(consultationId, cancellationToken);
                return consultation is null ? NotFoundOrForbidden() : Ok(consultation);
            });

        [HttpPost("consultations/{consultationId:int}/status")]
        [EnableRateLimiting("mobile-write")]
        [RequirePermission(MobilePermissions.ConsultationsAttend)]
        public async Task<IActionResult> UpdateConsultation(
            int consultationId,
            [FromBody] UpdateConsultationRequest request,
            CancellationToken cancellationToken)
        {
            if (consultationId <= 0) return NotFoundOrForbidden();
            if (request is null || !ConsultationStatuses.Contains(request.Estado ?? string.Empty))
                return InvalidRequest("El estado indicado no es válido.");
            if (request.RespuestaInterna?.Length > 1000)
                return InvalidRequest("La respuesta interna admite hasta 1000 caracteres.");

            return await GuardAsync(_logger, "atender consulta", async () =>
            {
                await _office.UpdateConsultationAsync(consultationId, request.Estado!, request.RespuestaInterna,
                    CurrentUserId, CurrentUserName, cancellationToken);

                await _audit.RecordAsync(CurrentUserId, CurrentUserName, CurrentUserEmail, CurrentRole,
                    "Actualizar consulta", "Consultas",
                    $"Consulta #{consultationId} marcada como {request.Estado}.",
                    ClientIp, ClientUserAgent, cancellationToken);

                var consultation = await _office.GetConsultationAsync(consultationId, cancellationToken);
                return consultation is null ? NotFoundOrForbidden() : Ok(consultation);
            });
        }

        // ── Auditoría ───────────────────────────────────────────────────────

        [HttpGet("audit")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.AuditView)]
        public Task<IActionResult> Audit([FromQuery] string? modulo, [FromQuery] string? buscar, CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar bitácora", async () =>
                Ok(await _office.GetAuditAsync(modulo, buscar, cancellationToken)));
    }
}
