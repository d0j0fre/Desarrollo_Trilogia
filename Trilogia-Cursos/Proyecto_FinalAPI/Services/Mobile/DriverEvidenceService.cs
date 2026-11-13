using Proyecto_Final.Services;

namespace Proyecto_FinalAPI.Services.Mobile
{
    public sealed record DriverEvidenceResult(int EvidenciaId, string Tipo, bool Duplicado);

    /// <summary>El almacenamiento compartido de evidencias no está configurado en este servidor.</summary>
    public sealed class EvidenceStorageNotConfiguredException : Exception
    {
        public EvidenceStorageNotConfiguredException()
            : base("EvidenceStorage:RootPath no está configurado en la API.") { }
    }

    public interface IDriverEvidenceService
    {
        Task<DriverEvidenceResult> RegisterAsync(
            int routeOrderId,
            int driverUserId,
            string driverName,
            string? type,
            Guid syncGuid,
            IFormFile? file,
            string? notes,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Registra la foto o la firma de una parada desde la app del chofer.
    ///
    /// No tiene lógica propia de archivos ni de reglas: usa el mismo
    /// <see cref="IEvidenceStorageService"/> (límite de 5 MB, JPG/PNG/WEBP, firma binaria
    /// verificada, clave aleatoria) y los mismos procedimientos que el sitio web
    /// (<c>sp_Entrega_RegisterEvidence</c> y compañía). Lo único que hace es el orden de
    /// la web: archivo temporal → fila pendiente → archivo definitivo → fila lista, con la
    /// misma compensación si algo falla a medias.
    ///
    /// La autorización va primero y en SQL (<c>sp_Chofer_PrepararEvidencia</c>): una parada
    /// ajena no llega a tocar el disco.
    /// </summary>
    public sealed class DriverEvidenceService : IDriverEvidenceService
    {
        private readonly IDriverMobileDbService _driver;
        private readonly IEvidenceStorageService _storage;
        private readonly ILogger<DriverEvidenceService> _logger;

        public DriverEvidenceService(
            IDriverMobileDbService driver,
            IEvidenceStorageService storage,
            ILogger<DriverEvidenceService> logger)
        {
            _driver = driver;
            _storage = storage;
            _logger = logger;
        }

        public async Task<DriverEvidenceResult> RegisterAsync(
            int routeOrderId,
            int driverUserId,
            string driverName,
            string? type,
            Guid syncGuid,
            IFormFile? file,
            string? notes,
            CancellationToken cancellationToken = default)
        {
            var normalizedType = NormalizeType(type);
            ValidateFile(normalizedType, file);

            var target = await _driver.PrepareEvidenceAsync(routeOrderId, driverUserId, syncGuid, cancellationToken);
            if (target.ExistingEvidenceId is { } existing)
            {
                // La cola reenvió algo que ya quedó registrado: se confirma sin guardar de nuevo.
                return new DriverEvidenceResult(existing, normalizedType, Duplicado: true);
            }

            StagedEvidence? staged = null;
            var evidenceId = 0;
            var committed = false;

            try
            {
                staged = await _storage.StageUploadAsync(file, cancellationToken)
                         ?? throw new EvidenceValidationException("Debe adjuntar una imagen válida.");

                evidenceId = await _driver.RegisterEvidenceAsync(
                    target.PedidoId,
                    target.RutaId,
                    normalizedType,
                    staged.StorageKey,
                    staged.ContentType,
                    notes,
                    driverUserId,
                    driverName,
                    syncGuid,
                    cancellationToken);

                await _storage.CommitAsync(staged, cancellationToken);
                committed = true;
                await _driver.MarkEvidenceReadyAsync(evidenceId, driverUserId, cancellationToken);

                return new DriverEvidenceResult(evidenceId, normalizedType, Duplicado: false);
            }
            finally
            {
                if (staged is not null && !committed)
                {
                    try { await _storage.DeleteStageAsync(staged); }
                    catch (Exception exception) { _logger.LogWarning(exception, "No se pudo limpiar un archivo temporal de evidencia."); }
                }

                if (evidenceId > 0 && !committed)
                {
                    try { await _driver.DeletePendingEvidenceAsync(evidenceId, driverUserId, CancellationToken.None); }
                    catch (Exception exception) { _logger.LogWarning(exception, "No se pudo compensar la evidencia pendiente {EvidenceId}.", evidenceId); }
                }
            }
        }

        private static string NormalizeType(string? type) => type?.Trim().ToLowerInvariant() switch
        {
            "foto" => "Foto",
            "firma" => "Firma",
            _ => throw new EvidenceValidationException("El tipo de evidencia debe ser Foto o Firma.")
        };

        private static void ValidateFile(string type, IFormFile? file)
        {
            if (file is null || file.Length == 0)
                throw new EvidenceValidationException("Debe adjuntar una imagen.");

            // La firma es siempre un PNG: es lo que genera el lienzo (y lo único que acepta la web).
            if (type == "Firma" &&
                !string.Equals(Path.GetExtension(file.FileName), ".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new EvidenceValidationException("La firma debe ser una imagen PNG válida.");
            }
        }
    }

    /// <summary>
    /// Almacenamiento de reemplazo cuando <c>EvidenceStorage:RootPath</c> no está configurado.
    ///
    /// Si la API guardara en su propia carpeta, la foto quedaría donde el sitio web no la
    /// puede leer y se perdería en silencio (docs/plan-app-movil-flutter.md §9.1). Es
    /// preferible rechazar la subida con un aviso claro y que la cola de la app la
    /// conserve en el teléfono.
    /// </summary>
    public sealed class UnconfiguredEvidenceStorageService : IEvidenceStorageService
    {
        public Task<StagedEvidence?> StageUploadAsync(IFormFile? file, CancellationToken cancellationToken = default) =>
            throw new EvidenceStorageNotConfiguredException();

        public Task<StagedEvidence?> StageSignatureAsync(string? dataUrl, CancellationToken cancellationToken = default) =>
            throw new EvidenceStorageNotConfiguredException();

        public Task CommitAsync(StagedEvidence evidence, CancellationToken cancellationToken = default) =>
            throw new EvidenceStorageNotConfiguredException();

        public Task DeleteStageAsync(StagedEvidence evidence) => Task.CompletedTask;

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            throw new EvidenceStorageNotConfiguredException();
    }
}
