using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Services;
using Proyecto_FinalAPI.Authorization;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services.Mobile;

namespace Proyecto_FinalAPI.Controllers
{
    /// <summary>
    /// Todo lo que un chofer hace desde la calle.
    ///
    /// Ningún endpoint recibe el identificador del chofer: sale del token. La
    /// pertenencia de rutas, entregas y jornadas se valida además dentro de los
    /// procedimientos almacenados, que rechazan con error ≥ 50000 lo que no le
    /// corresponde. Dos capas, ninguna confía en el teléfono.
    /// </summary>
    [Route("api/mobile/v1/driver")]
    [Authorize(Roles = "Chofer,Administrador")]
    [RequirePermission(MobilePermissions.Access)]
    public sealed class MobileDriverController : MobileControllerBase
    {
        private static readonly HashSet<string> AllowedStatuses =
            new(StringComparer.OrdinalIgnoreCase) { "EnRuta", "Entregado", "Fallido" };

        /// <summary>Mismo tope que la web (6 MB: los 5 MB del archivo más el multipart).</summary>
        private const int MaxEvidenceRequestBytes = 6 * 1024 * 1024;

        private readonly IDriverMobileDbService _driver;
        private readonly IDriverEvidenceService _evidence;
        private readonly IMobileAuditService _audit;
        private readonly ILogger<MobileDriverController> _logger;

        public MobileDriverController(
            IDriverMobileDbService driver,
            IDriverEvidenceService evidence,
            IMobileAuditService audit,
            ILogger<MobileDriverController> logger)
        {
            _driver = driver;
            _evidence = evidence;
            _audit = audit;
            _logger = logger;
        }

        // ── Rutas ───────────────────────────────────────────────────────────

        [HttpGet("routes")]
        [EnableRateLimiting("mobile-read")]
        public async Task<IActionResult> Routes(CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _driver.GetRoutesAsync(CurrentUserId, cancellationToken));
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "No se pudieron leer las rutas del chofer {UserId}.", CurrentUserId);
                return Unavailable();
            }
        }

        [HttpGet("routes/{id:int}")]
        [EnableRateLimiting("mobile-read")]
        public async Task<IActionResult> Route(int id, CancellationToken cancellationToken)
        {
            if (id <= 0) return NotFoundOrForbidden();

            try
            {
                var route = await _driver.GetRouteAsync(id, CurrentUserId, cancellationToken);
                return route is null ? NotFoundOrForbidden() : Ok(route);
            }
            catch (SqlException exception) when (exception.Number >= 50000)
            {
                // El procedimiento rechazó el acceso. Se responde igual que si la
                // ruta no existiera.
                _logger.LogWarning(
                    exception,
                    "Acceso rechazado a la ruta {RouteId} para el chofer {UserId}.",
                    id,
                    CurrentUserId);
                return NotFoundOrForbidden();
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Falló la consulta de la ruta {RouteId}.", id);
                return Unavailable();
            }
        }

        [HttpGet("summary")]
        [EnableRateLimiting("mobile-read")]
        public async Task<IActionResult> Summary(CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _driver.GetDaySummaryAsync(CurrentUserId, cancellationToken));
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "No se pudo armar el resumen del chofer {UserId}.", CurrentUserId);
                return Unavailable();
            }
        }

        // ── Entregas ────────────────────────────────────────────────────────

        /// <summary>
        /// Cambia el estado de una entrega.
        ///
        /// Idempotente por <c>syncGuid</c>: la cola offline del teléfono puede
        /// reintentar sin miedo. El procedimiento detecta el reenvío y devuelve
        /// <c>duplicado = true</c> en vez de aplicar el cambio dos veces.
        /// </summary>
        [HttpPost("deliveries/{routeOrderId:int}/status")]
        [EnableRateLimiting("mobile-write")]
        public async Task<IActionResult> UpdateDeliveryStatus(
            int routeOrderId,
            [FromBody] UpdateDeliveryStatusRequest request,
            CancellationToken cancellationToken)
        {
            if (routeOrderId <= 0) return NotFoundOrForbidden();
            if (request is null) return InvalidRequest("Falta el cuerpo de la solicitud.");

            if (!AllowedStatuses.Contains(request.Estado ?? string.Empty))
            {
                return InvalidRequest("El estado indicado no es válido.");
            }

            if (!Guid.TryParse(request.SyncGuid, out var syncGuid) || syncGuid == Guid.Empty)
            {
                // Sin identificador propio no hay idempotencia, y sin
                // idempotencia un reintento duplica la entrega. Se exige.
                return InvalidRequest("Falta un identificador de sincronización válido.");
            }

            var isFailure = string.Equals(request.Estado, "Fallido", StringComparison.OrdinalIgnoreCase);
            if (isFailure && string.IsNullOrWhiteSpace(request.MotivoFallo))
            {
                return BusinessRule("Una entrega fallida necesita un motivo.");
            }

            try
            {
                var result = await _driver.UpdateDeliveryStatusAsync(
                    routeOrderId,
                    request.Estado!,
                    syncGuid,
                    request.MotivoFallo,
                    CurrentUserId,
                    CurrentUserName,
                    cancellationToken);

                if (result is null) return NotFoundOrForbidden();

                if (!result.Duplicado)
                {
                    await _audit.RecordAsync(
                        CurrentUserId,
                        CurrentUserName,
                        CurrentUserEmail,
                        CurrentRole,
                        "Actualizar entrega",
                        "Entregas",
                        $"Pedido #{result.PedidoId}: estado de entrega {result.EstadoEntrega}.",
                        ClientIp,
                        ClientUserAgent,
                        cancellationToken);
                }

                return Ok(result);
            }
            catch (SqlException exception) when (exception.Number >= 50000)
            {
                _logger.LogWarning(
                    exception,
                    "Cambio de estado rechazado para RutaPedido {RouteOrderId} del chofer {UserId}.",
                    routeOrderId,
                    CurrentUserId);
                return NotFoundOrForbidden();
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Falló el cambio de estado de RutaPedido {RouteOrderId}.", routeOrderId);
                return Unavailable();
            }
        }

        /// <summary>
        /// Sube la foto o la firma de una parada de <b>su propia</b> ruta (G3-04).
        ///
        /// Multipart: <c>tipo</c> (Foto|Firma), <c>syncGuid</c>, <c>archivo</c> y
        /// <c>observaciones</c> opcional. Mismos límites que la web: imagen JPG, PNG o WEBP
        /// de hasta 5 MB (la firma, PNG), con la firma binaria verificada. La parada debe ser
        /// del chofer del token y su ruta estar Despachada (o cerrada hace menos de 48 horas,
        /// para la última parada y el envío sin señal).
        ///
        /// Idempotente por <c>syncGuid</c>, igual que el estado: un reenvío devuelve
        /// <c>duplicado = true</c> sin guardar el archivo otra vez.
        /// </summary>
        [HttpPost("deliveries/{routeOrderId:int}/evidence")]
        [EnableRateLimiting("mobile-write")]
        [RequestSizeLimit(MaxEvidenceRequestBytes)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> RegisterEvidence(
            int routeOrderId,
            [FromForm] RegisterEvidenceRequest request,
            CancellationToken cancellationToken)
        {
            if (routeOrderId <= 0) return NotFoundOrForbidden();
            if (request is null) return InvalidRequest("Falta el cuerpo de la solicitud.");

            if (!TryReadSyncGuid(request.SyncGuid, out var syncGuid))
            {
                return InvalidRequest("Falta un identificador de sincronización válido.");
            }

            try
            {
                var result = await _evidence.RegisterAsync(
                    routeOrderId,
                    CurrentUserId,
                    CurrentUserName,
                    request.Tipo,
                    syncGuid,
                    request.Archivo,
                    request.Observaciones,
                    cancellationToken);

                if (!result.Duplicado)
                {
                    await _audit.RecordAsync(
                        CurrentUserId,
                        CurrentUserName,
                        CurrentUserEmail,
                        CurrentRole,
                        "Registrar evidencia",
                        "Entregas",
                        $"Evidencia ({result.Tipo}) registrada desde la app para la parada {routeOrderId}. ID: {result.EvidenciaId}.",
                        ClientIp,
                        ClientUserAgent,
                        cancellationToken);
                }

                return Ok(new RegisterEvidenceResponse
                {
                    EvidenciaId = result.EvidenciaId,
                    Tipo = result.Tipo,
                    Duplicado = result.Duplicado
                });
            }
            catch (EvidenceValidationException exception)
            {
                // Archivo o tipo inválido: reintentar el mismo archivo no lo arregla.
                return BusinessRule(exception.Message);
            }
            catch (EvidenceStorageNotConfiguredException exception)
            {
                _logger.LogError(exception, "Se rechazó una evidencia porque el almacenamiento compartido no está configurado.");
                return Unavailable();
            }
            catch (SqlException exception) when (exception.Number == 52087)
            {
                return BusinessRule("Esa ruta ya no admite evidencia: solo se puede subir en una ruta despachada o cerrada hace menos de 48 horas.");
            }
            catch (SqlException exception) when (exception.Number == 52088)
            {
                return InvalidRequest("El identificador de sincronización no es válido.");
            }
            catch (SqlException exception) when (exception.Number >= 50000)
            {
                _logger.LogWarning(
                    exception,
                    "Evidencia rechazada para la parada {RouteOrderId} del chofer {UserId}.",
                    routeOrderId,
                    CurrentUserId);
                return NotFoundOrForbidden();
            }
            catch (Exception exception) when (exception is SqlException or IOException)
            {
                _logger.LogError(exception, "Falló el registro de evidencia de la parada {RouteOrderId}.", routeOrderId);
                return Unavailable();
            }
        }

        // ── Kilometraje ─────────────────────────────────────────────────────

        [HttpGet("vehicles")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.OwnMileage)]
        public async Task<IActionResult> Vehicles(CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _driver.GetVehiclesAsync(CurrentUserId, cancellationToken));
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "No se pudieron leer los vehículos del chofer {UserId}.", CurrentUserId);
                return Unavailable();
            }
        }

        [HttpGet("mileage/open")]
        [EnableRateLimiting("mobile-read")]
        [RequirePermission(MobilePermissions.OwnMileage)]
        public async Task<IActionResult> OpenShift(CancellationToken cancellationToken)
        {
            try
            {
                var shift = await _driver.GetOpenShiftAsync(CurrentUserId, cancellationToken);
                // Sin jornada abierta no es un error: es el estado normal de
                // quien todavía no sale a ruta.
                return Ok(shift);
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "No se pudo leer la jornada abierta del chofer {UserId}.", CurrentUserId);
                return Unavailable();
            }
        }

        [HttpPost("mileage/open")]
        [EnableRateLimiting("mobile-write")]
        [RequirePermission(MobilePermissions.OwnMileage)]
        public async Task<IActionResult> OpenMileage(
            [FromBody] OpenMileageRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null) return InvalidRequest("Falta el cuerpo de la solicitud.");
            if (request.VehiculoId <= 0) return InvalidRequest("Indicá el vehículo.");
            if (request.KmInicial < 0) return InvalidRequest("El kilometraje inicial no puede ser negativo.");
            if (!Guid.TryParse(request.SyncGuid, out var syncGuid) || syncGuid == Guid.Empty)
            {
                return InvalidRequest("Falta un identificador de sincronización válido.");
            }

            try
            {
                var result = await _driver.OpenMileageAsync(
                    CurrentUserId,
                    request.VehiculoId,
                    request.KmInicial,
                    syncGuid,
                    request.Observaciones,
                    cancellationToken);

                if (!result.Duplicado)
                {
                    await _audit.RecordAsync(
                        CurrentUserId, CurrentUserName, CurrentUserEmail, CurrentRole,
                        "Abrir jornada", "Flota",
                        $"Jornada #{result.KilometrajeId} abierta en el vehículo {request.VehiculoId} con {request.KmInicial} km.",
                        ClientIp, ClientUserAgent, cancellationToken);
                }

                return Ok(result);
            }
            catch (SqlException exception) when (exception.Number >= 50000)
            {
                _logger.LogWarning(
                    exception,
                    "Apertura de jornada rechazada para el chofer {UserId} en el vehículo {VehicleId}.",
                    CurrentUserId,
                    request.VehiculoId);
                return BusinessRule(MileageMessage(exception.Number));
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Falló la apertura de jornada del chofer {UserId}.", CurrentUserId);
                return Unavailable();
            }
        }

        [HttpPost("mileage/close")]
        [EnableRateLimiting("mobile-write")]
        [RequirePermission(MobilePermissions.OwnMileage)]
        public async Task<IActionResult> CloseMileage(
            [FromBody] CloseMileageRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null) return InvalidRequest("Falta el cuerpo de la solicitud.");
            if (request.KilometrajeId <= 0) return InvalidRequest("Indicá la jornada a cerrar.");
            if (request.KmFinal < 0) return InvalidRequest("El kilometraje final no puede ser negativo.");
            if (!Guid.TryParse(request.SyncGuid, out var syncGuid) || syncGuid == Guid.Empty)
            {
                return InvalidRequest("Falta un identificador de sincronización válido.");
            }

            try
            {
                var result = await _driver.CloseMileageAsync(
                    CurrentUserId,
                    request.KilometrajeId,
                    request.KmFinal,
                    syncGuid,
                    cancellationToken);

                if (!result.Duplicado)
                {
                    await _audit.RecordAsync(
                        CurrentUserId, CurrentUserName, CurrentUserEmail, CurrentRole,
                        "Cerrar jornada", "Flota",
                        $"Jornada #{result.KilometrajeId} cerrada con {result.KmFinal} km ({result.KmRecorridos} km recorridos).",
                        ClientIp, ClientUserAgent, cancellationToken);
                }

                return Ok(result);
            }
            catch (SqlException exception) when (exception.Number >= 50000)
            {
                _logger.LogWarning(
                    exception,
                    "Cierre de jornada {MileageId} rechazado para el chofer {UserId}.",
                    request.KilometrajeId,
                    CurrentUserId);
                return BusinessRule(MileageMessage(exception.Number));
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Falló el cierre de la jornada {MileageId}.", request.KilometrajeId);
                return Unavailable();
            }
        }

        /// <summary>
        /// Traduce el error del procedimiento a algo que el chofer pueda leer y
        /// resolver. No se devuelve el mensaje de SQL: su texto puede cambiar y
        /// puede revelar detalles del esquema.
        /// </summary>
        private static string MileageMessage(int sqlErrorNumber) => sqlErrorNumber switch
        {
            54961 => "El kilometraje inicial no es válido.",
            54963 => "Ese vehículo no está asignado a una ruta activa suya.",
            54964 => "El vehículo tiene una jornada sin cerrar.",
            54966 => "No se encontró la jornada indicada.",
            54967 => "Esa jornada no le pertenece.",
            54968 => "La jornada ya fue cerrada.",
            54969 => "El kilometraje final no puede ser menor al inicial.",
            _ => "No fue posible registrar el kilometraje."
        };
    }
}
