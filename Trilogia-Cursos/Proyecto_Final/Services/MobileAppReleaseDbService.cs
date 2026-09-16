using System.Data;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services
{
    public interface IMobileAppReleaseService
    {
        Task<MobileAppReleaseViewModel?> GetCurrentAsync(CancellationToken cancellationToken = default);
        Task<List<MobileAppReleaseViewModel>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<int> PublishAsync(MobileAppPublishRequest request, int userId, string userName, CancellationToken cancellationToken = default);
        Task<bool> UnpublishAsync(int versionId, CancellationToken cancellationToken = default);
        Task<MobileAppReleaseViewModel?> GetByIdAsync(int versionId, CancellationToken cancellationToken = default);
    }

    // Catálogo de versiones del APK (migraciones 0026 y 0027). Mismo patrón del
    // proyecto: ADO.NET contra procedimientos almacenados.
    public sealed class MobileAppReleaseDbService : IMobileAppReleaseService
    {
        private readonly string _connectionString;

        public MobileAppReleaseDbService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        public async Task<MobileAppReleaseViewModel?> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_AppMovil_GetVigente", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            return new MobileAppReleaseViewModel
            {
                VersionId = reader.GetInt32(0),
                VersionNombre = reader.GetString(1),
                BuildNumero = reader.GetInt32(2),
                MinBuildSoportado = reader.GetInt32(3),
                UrlDescarga = Texto(reader, 4),
                Sha256 = reader.GetString(5),
                TamanoBytes = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                Notas = Texto(reader, 7),
                MensajeObligatorio = Texto(reader, 8),
                FechaPublicacion = reader.GetDateTime(9),
                ArchivoAlmacenado = Texto(reader, 10),
                ArchivoNombre = Texto(reader, 11),
                Publicada = true
            };
        }

        public async Task<List<MobileAppReleaseViewModel>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var releases = new List<MobileAppReleaseViewModel>();

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_AppMovil_List", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                releases.Add(new MobileAppReleaseViewModel
                {
                    VersionId = reader.GetInt32(0),
                    VersionNombre = reader.GetString(1),
                    BuildNumero = reader.GetInt32(2),
                    MinBuildSoportado = reader.GetInt32(3),
                    UrlDescarga = Texto(reader, 4),
                    Sha256 = reader.GetString(5),
                    TamanoBytes = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                    Notas = Texto(reader, 7),
                    Publicada = reader.GetBoolean(8),
                    FechaPublicacion = reader.GetDateTime(9),
                    PublicadaPorNombre = Texto(reader, 10),
                    ArchivoAlmacenado = Texto(reader, 11),
                    ArchivoNombre = Texto(reader, 12)
                });
            }

            return releases;
        }

        public async Task<MobileAppReleaseViewModel?> GetByIdAsync(int versionId, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_AppMovil_GetArchivo", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@VersionId", SqlDbType.Int).Value = versionId;

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            return new MobileAppReleaseViewModel
            {
                VersionId = reader.GetInt32(0),
                ArchivoAlmacenado = Texto(reader, 1),
                ArchivoNombre = Texto(reader, 2),
                VersionNombre = reader.GetString(3),
                BuildNumero = reader.GetInt32(4),
                Publicada = reader.GetBoolean(5)
            };
        }

        public async Task<int> PublishAsync(
            MobileAppPublishRequest request,
            int userId,
            string userName,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_AppMovil_Publicar", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@VersionNombre", SqlDbType.NVarChar, 20).Value = request.VersionNombre.Trim();
            command.Parameters.Add("@BuildNumero", SqlDbType.Int).Value = request.BuildNumero;
            command.Parameters.Add("@MinBuildSoportado", SqlDbType.Int).Value = request.MinBuildSoportado;
            command.Parameters.Add("@Sha256", SqlDbType.Char, 64).Value = request.Sha256.Trim().ToUpperInvariant();
            command.Parameters.Add("@UrlDescarga", SqlDbType.NVarChar, 500).Value = Nulo(request.UrlDescarga);
            command.Parameters.Add("@ArchivoAlmacenado", SqlDbType.NVarChar, 120).Value = Nulo(request.ArchivoAlmacenado);
            command.Parameters.Add("@ArchivoNombre", SqlDbType.NVarChar, 200).Value = Nulo(request.ArchivoNombre);
            command.Parameters.Add("@TamanoBytes", SqlDbType.BigInt).Value =
                request.TamanoBytes.HasValue ? request.TamanoBytes.Value : DBNull.Value;
            command.Parameters.Add("@Notas", SqlDbType.NVarChar, 500).Value = Nulo(request.Notas);
            command.Parameters.Add("@MensajeObligatorio", SqlDbType.NVarChar, 300).Value = Nulo(request.MensajeObligatorio);
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = userId > 0 ? userId : DBNull.Value;
            command.Parameters.Add("@UsuarioNombre", SqlDbType.NVarChar, 150).Value = Nulo(userName);

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? reader.GetInt32(0) : 0;
        }

        public async Task<bool> UnpublishAsync(int versionId, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_AppMovil_Despublicar", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@VersionId", SqlDbType.Int).Value = versionId;

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) && reader.GetInt32(0) > 0;
        }

        private static string Texto(SqlDataReader reader, int ordinal) =>
            reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

        private static object Nulo(string? valor) =>
            string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();
    }
}
