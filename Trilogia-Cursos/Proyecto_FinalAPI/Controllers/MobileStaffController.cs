using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Proyecto_FinalAPI.Authorization;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services.Mobile;

namespace Proyecto_FinalAPI.Controllers
{
    /// <summary>
    /// Jornadas del personal: cada quien registra las suyas y el supervisor las
    /// aprueba. Mismos procedimientos que el portal web de RRHH, con su regla de
    /// segregación: nadie resuelve su propia jornada.
    /// </summary>
    [Route("api/mobile/v1/staff")]
    [Authorize]
    [RequirePermission(MobilePermissions.Access)]
    public sealed class MobileStaffController : MobileControllerBase
    {
        private static readonly HashSet<string> Decisions = new(StringComparer.Ordinal) { "Aprobada", "Rechazada" };

        private readonly IOfficeMobileDbService _office;
        private readonly IMobileAuditService _audit;
        private readonly TimeProvider _time;
        private readonly ILogger<MobileStaffController> _logger;

        public MobileStaffController(
            IOfficeMobileDbService office,
            IMobileAuditService audit,
            TimeProvider time,
            ILogger<MobileStaffController> logger)
        {
            _office = office;
            _audit = audit;
            _time = time;
            _logger = logger;
        }

        private DateTime Today => _time.GetUtcNow().ToOffset(TimeSpan.FromHours(-6)).Date;

        [HttpGet("attendance")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.AttendanceOwn)]
        public Task<IActionResult> MyAttendance(CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar jornadas propias", async () =>
                Ok(await _office.GetMyAttendanceAsync(CurrentUserId, Today.AddDays(-45), Today, cancellationToken)));

        [HttpPost("attendance")]
        [EnableRateLimiting("mobile-write")]
        [RequirePermission(MobilePermissions.AttendanceRegister)]
        public async Task<IActionResult> SaveAttendance([FromBody] SaveAttendanceRequest request, CancellationToken cancellationToken)
        {
            if (request is null) return InvalidRequest("Falta el cuerpo de la solicitud.");
            if (request.Fecha.Date > Today) return InvalidRequest("No se registran jornadas de días que no han pasado.");
            if (request.Fecha.Date < Today.AddDays(-45)) return InvalidRequest("Solo se registran jornadas de los últimos 45 días.");
            if (request.HorasOrdinarias < 0 || request.HorasExtra < 0 || request.HorasAusencia < 0)
                return InvalidRequest("Las horas no pueden ser negativas.");
            var total = request.HorasOrdinarias + request.HorasExtra + request.HorasAusencia;
            if (total <= 0 || total > 24) return InvalidRequest("El total de horas debe estar entre 0 y 24.");
            if (!TryReadSyncGuid(request.SyncGuid, out var idempotencyKey))
                return InvalidRequest("Falta un identificador de sincronización válido.");

            return await GuardAsync(_logger, "registrar jornada", async () =>
            {
                await _office.SaveMyAttendanceAsync(CurrentUserId, request, idempotencyKey, cancellationToken);
                return Ok(await _office.GetMyAttendanceAsync(CurrentUserId, Today.AddDays(-45), Today, cancellationToken));
            });
        }

        [HttpGet("attendance/pending")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.AttendanceApprove)]
        public Task<IActionResult> PendingAttendance(CancellationToken cancellationToken) =>
            GuardAsync(_logger, "consultar jornadas pendientes", async () =>
                Ok(await _office.GetPendingAttendanceAsync(Today.AddDays(-60), Today, cancellationToken)));

        [HttpPost("attendance/{attendanceId:long}/resolve")]
        [EnableRateLimiting("mobile-write")]
        [RequirePermission(MobilePermissions.AttendanceApprove)]
        public async Task<IActionResult> ResolveAttendance(
            long attendanceId,
            [FromBody] ResolveAttendanceRequest request,
            CancellationToken cancellationToken)
        {
            if (attendanceId <= 0) return NotFoundOrForbidden();
            if (request is null) return InvalidRequest("Falta el cuerpo de la solicitud.");
            if (!Decisions.Contains(request.Decision ?? string.Empty)) return InvalidRequest("La decisión no es válida.");
            if (request.Decision == "Rechazada" && string.IsNullOrWhiteSpace(request.Respuesta))
                return InvalidRequest("Explicá por qué se rechaza la jornada.");

            byte[] version;
            try
            {
                version = Convert.FromBase64String(request.Version ?? string.Empty);
            }
            catch (FormatException)
            {
                return InvalidRequest("La versión de la jornada no es válida. Actualizá la lista.");
            }
            if (version.Length != 8) return InvalidRequest("La versión de la jornada no es válida. Actualizá la lista.");

            return await GuardAsync(_logger, "resolver jornada", async () =>
            {
                await _office.ResolveAttendanceAsync(attendanceId, request.Decision!, request.Respuesta, version,
                    CurrentUserId, CurrentUserName, cancellationToken);

                await _audit.RecordAsync(CurrentUserId, CurrentUserName, CurrentUserEmail, CurrentRole,
                    request.Decision == "Aprobada" ? "Aprobar jornada" : "Rechazar jornada", "RRHH",
                    $"Jornada #{attendanceId} {request.Decision!.ToLowerInvariant()}.",
                    ClientIp, ClientUserAgent, cancellationToken);

                return Ok(await _office.GetPendingAttendanceAsync(Today.AddDays(-60), Today, cancellationToken));
            });
        }
    }
}
