using System.Data;
using Microsoft.Data.SqlClient;

namespace Proyecto_FinalAPI.Services.Mobile
{
    public sealed record AppRelease(
        int VersionId,
        string VersionNombre,
        int BuildNumero,
        int MinBuildSoportado,
        string UrlDescarga,
        string Sha256,
        long? TamanoBytes,
        string? Notas,
        string? MensajeObligatorio,
        DateTime FechaPublicacion);

    public interface IAppReleaseDbService
    {
        Task<AppRelease?> GetCurrentAsync(CancellationToken cancellationToken = default);
    }

    // Versión vigente del APK. Vive en la base y no en configuración para que
    // publicar una versión nueva no requiera desplegar el sistema.
    public sealed class AppReleaseDbService : IAppReleaseDbService
    {
        private readonly string _connectionString;

        public AppReleaseDbService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        public async Task<AppRelease?> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_AppMovil_GetVigente", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            return new AppRelease(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt64(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetDateTime(9));
        }
    }
}
