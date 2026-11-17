using System.Data;
using Microsoft.Data.SqlClient;

namespace Proyecto_Final.Services
{
    /// <summary>
    /// Guarda la foto y la firma de la entrega en la base de datos (dbo.EvidenciaArchivos, 0306).
    ///
    /// El sitio web y la API móvil comparten la base pero, en Azure, no comparten disco: con la
    /// carpeta, la API no tenía dónde guardar y el sitio no habría visto las fotos de la app. Con
    /// este almacenamiento ambos leen y escriben lo mismo. Se activa con
    /// <c>EvidenceStorage:Provider = Database</c>; sin esa configuración se usa la carpeta.
    ///
    /// El ciclo es el mismo que el de la carpeta: preparar (sin confirmar), registrar la evidencia,
    /// confirmar; si algo falla, descartar. Solo una imagen confirmada se puede leer.
    /// </summary>
    public sealed class DatabaseEvidenceStorageService : IEvidenceStorageService
    {
        private readonly string _connectionString;

        public DatabaseEvidenceStorageService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        /// <summary>Indica si la configuración pide guardar la evidencia en la base.</summary>
        public static bool IsSelected(IConfiguration configuration) =>
            string.Equals(configuration["EvidenceStorage:Provider"], "Database", StringComparison.OrdinalIgnoreCase);

        public async Task<StagedEvidence?> StageUploadAsync(IFormFile? file, CancellationToken cancellationToken = default)
        {
            if (file is null || file.Length == 0) return null;
            var (contentType, extension) = EvidenceInput.ValidateUpload(file);

            await using var source = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length > EvidenceInput.MaximumBytes)
                throw new EvidenceValidationException("La imagen supera el tamaño máximo permitido de 5 MB.");

            return await StageBytesAsync(buffer.ToArray(), contentType, extension, cancellationToken);
        }

        public async Task<StagedEvidence?> StageSignatureAsync(string? dataUrl, CancellationToken cancellationToken = default)
        {
            var bytes = EvidenceInput.DecodeSignature(dataUrl);
            if (bytes is null) return null;
            return await StageBytesAsync(bytes, "image/png", ".png", cancellationToken);
        }

        public async Task CommitAsync(StagedEvidence evidence, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = Procedure(connection, "dbo.sp_EvidenciaArchivo_Confirmar");
            command.Parameters.Add("@TemporaryKey", SqlDbType.NVarChar, 40).Value = evidence.TemporaryKey;
            command.Parameters.Add("@StorageKey", SqlDbType.NVarChar, 40).Value = evidence.StorageKey;
            await connection.OpenAsync(cancellationToken);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task DeleteStageAsync(StagedEvidence evidence)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = Procedure(connection, "dbo.sp_EvidenciaArchivo_Descartar");
            command.Parameters.Add("@TemporaryKey", SqlDbType.NVarChar, 40).Value = evidence.TemporaryKey;
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            if (!FileEvidenceStorageService.IsSafeStorageKey(storageKey)) return null;

            await using var connection = new SqlConnection(_connectionString);
            await using var command = Procedure(connection, "dbo.sp_EvidenciaArchivo_Obtener");
            command.Parameters.Add("@StorageKey", SqlDbType.NVarChar, 40).Value = storageKey;
            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            var bytes = (byte[])reader["Contenido"];
            return new MemoryStream(bytes, writable: false);
        }

        private async Task<StagedEvidence> StageBytesAsync(
            byte[] bytes,
            string contentType,
            string extension,
            CancellationToken cancellationToken)
        {
            if (!FileEvidenceStorageService.HasExpectedSignature(bytes.AsSpan(0, Math.Min(12, bytes.Length)), contentType))
                throw new EvidenceValidationException("El contenido del archivo no corresponde a una imagen admitida.");

            var normalizedExtension = extension == ".jpeg" ? ".jpg" : extension;
            var key = $"{Guid.NewGuid():N}{normalizedExtension}";
            var temporaryKey = $"{Guid.NewGuid():N}.stage";

            await using var connection = new SqlConnection(_connectionString);
            await using var command = Procedure(connection, "dbo.sp_EvidenciaArchivo_Preparar");
            command.Parameters.Add("@StorageKey", SqlDbType.NVarChar, 40).Value = key;
            command.Parameters.Add("@TemporaryKey", SqlDbType.NVarChar, 40).Value = temporaryKey;
            command.Parameters.Add("@ContentType", SqlDbType.NVarChar, 30).Value = contentType;
            command.Parameters.Add("@Contenido", SqlDbType.VarBinary, -1).Value = bytes;
            await connection.OpenAsync(cancellationToken);
            await command.ExecuteNonQueryAsync(cancellationToken);

            return new StagedEvidence(temporaryKey, key, contentType, normalizedExtension, bytes.Length);
        }

        private static SqlCommand Procedure(SqlConnection connection, string name) =>
            new(name, connection) { CommandType = CommandType.StoredProcedure };
    }
}
