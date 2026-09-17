using System.Data;
using Microsoft.Data.SqlClient;
using Proyecto_FinalAPI.Models;

namespace Proyecto_FinalAPI.Services
{
    public interface IMobileAuthDbService
    {
        Task<IReadOnlyList<string>> GetPermissionsForRoleAsync(string? role, CancellationToken cancellationToken = default);
        Task<bool> HasPermissionAsync(string? role, string permissionCode, CancellationToken cancellationToken = default);
        Task<ApiUser?> GetActiveUserAsync(int userId, CancellationToken cancellationToken = default);
        Task<Guid> IssueRefreshTokenAsync(RefreshTokenIssueRequest request, CancellationToken cancellationToken = default);
        Task<RefreshExchangeRow> ExchangeRefreshTokenAsync(RefreshTokenExchangeRequest request, CancellationToken cancellationToken = default);
        Task RevokeChainAsync(string tokenHash, string? reason, CancellationToken cancellationToken = default);
    }

    public sealed record RefreshTokenIssueRequest(
        int UserId,
        string TokenHash,
        DateTime ExpiresUtc,
        Guid? ChainId,
        string? DeviceId,
        string? DeviceName,
        string? IpAddress);

    public sealed record RefreshTokenExchangeRequest(
        string PresentedTokenHash,
        string NewTokenHash,
        DateTime NewExpiresUtc,
        string? IpAddress);

    public sealed record RefreshExchangeRow(string Outcome, int? UserId, Guid? ChainId);

    // Acceso a datos de la autorización móvil. Mismo patrón que el resto del
    // proyecto: ADO.NET contra procedimientos almacenados, sin EF.
    // Los procedimientos los crea database/migrations/0024_mobile_auth_jwt.sql.
    public sealed class MobileAuthDbService : IMobileAuthDbService
    {
        private readonly string _connectionString;
        private readonly ILogger<MobileAuthDbService> _logger;

        public MobileAuthDbService(IConfiguration configuration, ILogger<MobileAuthDbService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
            _logger = logger;
        }

        public async Task<IReadOnlyList<string>> GetPermissionsForRoleAsync(
            string? role,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(role)) return Array.Empty<string>();

            var permissions = new List<string>();

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Auth_GetPermisosPorPerfil", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@NombreRol", SqlDbType.NVarChar, 100).Value = role.Trim();

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0)) permissions.Add(reader.GetString(0));
            }

            return permissions;
        }

        public async Task<bool> HasPermissionAsync(
            string? role,
            string permissionCode,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(permissionCode)) return false;

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Admin_HasPermissionByCode", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@NombreRol", SqlDbType.NVarChar, 100).Value = role.Trim();
            command.Parameters.Add("@Codigo", SqlDbType.NVarChar, 100).Value = permissionCode.Trim();

            await connection.OpenAsync(cancellationToken);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null && result != DBNull.Value && Convert.ToBoolean(result);
        }

        public async Task<ApiUser?> GetActiveUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0) return null;

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Auth_GetUsuarioActivo", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = userId;

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            return new ApiUser
            {
                UsuarioId = reader.GetInt32(reader.GetOrdinal("UsuarioId")),
                NombreCompleto = ReadString(reader, "NombreCompleto"),
                Correo = ReadString(reader, "Correo"),
                PerfilNombre = ReadString(reader, "PerfilNombre"),
                Activo = reader.GetBoolean(reader.GetOrdinal("Activo"))
            };
        }

        public async Task<Guid> IssueRefreshTokenAsync(
            RefreshTokenIssueRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Auth_EmitirTokenRefresco", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = request.UserId;
            command.Parameters.Add("@TokenHash", SqlDbType.Char, 64).Value = request.TokenHash;
            command.Parameters.Add("@ExpiraUtc", SqlDbType.DateTime2).Value = request.ExpiresUtc;
            command.Parameters.Add("@CadenaId", SqlDbType.UniqueIdentifier).Value =
                request.ChainId.HasValue ? request.ChainId.Value : DBNull.Value;
            command.Parameters.Add("@DispositivoId", SqlDbType.NVarChar, 100).Value =
                Nullable(request.DeviceId);
            command.Parameters.Add("@DispositivoNombre", SqlDbType.NVarChar, 150).Value =
                Nullable(request.DeviceName);
            command.Parameters.Add("@IpOrigen", SqlDbType.NVarChar, 45).Value =
                Nullable(request.IpAddress);

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("sp_Auth_EmitirTokenRefresco no devolvió la cadena emitida.");
            }

            return reader.GetGuid(reader.GetOrdinal("CadenaId"));
        }

        public async Task<RefreshExchangeRow> ExchangeRefreshTokenAsync(
            RefreshTokenExchangeRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Auth_CanjearTokenRefresco", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@TokenHash", SqlDbType.Char, 64).Value = request.PresentedTokenHash;
            command.Parameters.Add("@NuevoTokenHash", SqlDbType.Char, 64).Value = request.NewTokenHash;
            command.Parameters.Add("@NuevaExpiraUtc", SqlDbType.DateTime2).Value = request.NewExpiresUtc;
            command.Parameters.Add("@IpOrigen", SqlDbType.NVarChar, 45).Value = Nullable(request.IpAddress);

            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return new RefreshExchangeRow("Invalido", null, null);
            }

            var outcomeOrdinal = reader.GetOrdinal("Resultado");
            var userOrdinal = reader.GetOrdinal("UsuarioId");
            var chainOrdinal = reader.GetOrdinal("CadenaId");

            return new RefreshExchangeRow(
                reader.IsDBNull(outcomeOrdinal) ? "Invalido" : reader.GetString(outcomeOrdinal),
                reader.IsDBNull(userOrdinal) ? null : reader.GetInt32(userOrdinal),
                reader.IsDBNull(chainOrdinal) ? null : reader.GetGuid(chainOrdinal));
        }

        public async Task RevokeChainAsync(
            string tokenHash,
            string? reason,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tokenHash)) return;

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await using var command = new SqlCommand("dbo.sp_Auth_RevocarCadenaTokenRefresco", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                command.Parameters.Add("@TokenHash", SqlDbType.Char, 64).Value = tokenHash;
                command.Parameters.Add("@Motivo", SqlDbType.NVarChar, 200).Value = Nullable(reason);

                await connection.OpenAsync(cancellationToken);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException exception)
            {
                // El cierre de sesión no debe fallarle al usuario: el access token
                // caduca solo en minutos. Se registra y se sigue.
                _logger.LogWarning(exception, "No se pudo revocar la cadena del token de refresco.");
            }
        }

        private static object Nullable(string? value) =>
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

        private static string ReadString(SqlDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }
    }
}
