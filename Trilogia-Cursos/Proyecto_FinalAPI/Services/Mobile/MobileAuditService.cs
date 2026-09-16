using System.Data;
using Microsoft.Data.SqlClient;

namespace Proyecto_FinalAPI.Services.Mobile
{
    public interface IMobileAuditService
    {
        Task RecordAsync(
            int userId,
            string userName,
            string userEmail,
            string role,
            string action,
            string module,
            string description,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Auditoría de las acciones que entran desde la aplicación móvil.
    ///
    /// Usa el mismo <c>sp_Admin_CreateAuditLog</c> que el MVC, pero antepone el
    /// canal a la descripción: en el módulo de auditoría hay que poder
    /// distinguir de un vistazo lo que hizo alguien desde el teléfono en la
    /// calle de lo que se hizo desde una computadora en la oficina.
    ///
    /// Igual que en el MVC, una falla de auditoría nunca tumba la acción
    /// principal: el chofer no puede quedarse sin marcar una entrega porque la
    /// bitácora esté lenta.
    /// </summary>
    public sealed class MobileAuditService : IMobileAuditService
    {
        public const string Channel = "Móvil";

        private readonly string _connectionString;
        private readonly ILogger<MobileAuditService> _logger;

        public MobileAuditService(IConfiguration configuration, ILogger<MobileAuditService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
            _logger = logger;
        }

        public async Task RecordAsync(
            int userId,
            string userName,
            string userEmail,
            string role,
            string action,
            string module,
            string description,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await using var command = new SqlCommand("dbo.sp_Admin_CreateAuditLog", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = userId > 0 ? userId : DBNull.Value;
                command.Parameters.Add("@UsuarioNombre", SqlDbType.NVarChar, 150).Value =
                    string.IsNullOrWhiteSpace(userName) ? "Usuario no identificado" : userName.Trim();
                command.Parameters.Add("@UsuarioCorreo", SqlDbType.NVarChar, 150).Value =
                    string.IsNullOrWhiteSpace(userEmail) ? "No disponible" : userEmail.Trim();
                command.Parameters.Add("@Rol", SqlDbType.NVarChar, 50).Value =
                    string.IsNullOrWhiteSpace(role) ? "No disponible" : role.Trim();
                command.Parameters.Add("@Accion", SqlDbType.NVarChar, 80).Value = Truncate(action, 80);
                command.Parameters.Add("@Modulo", SqlDbType.NVarChar, 80).Value = Truncate(module, 80);
                command.Parameters.Add("@Descripcion", SqlDbType.NVarChar, 500).Value =
                    Truncate($"[{Channel}] {description}".Trim(), 500);
                command.Parameters.Add("@DireccionIp", SqlDbType.NVarChar, 80).Value =
                    string.IsNullOrWhiteSpace(ipAddress) ? DBNull.Value : Truncate(ipAddress, 80);
                command.Parameters.Add("@UserAgent", SqlDbType.NVarChar, 300).Value =
                    string.IsNullOrWhiteSpace(userAgent) ? DBNull.Value : Truncate(userAgent, 300);

                await connection.OpenAsync(cancellationToken);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "No se pudo registrar la auditoría móvil de la acción {Accion}.", action);
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            var trimmed = (value ?? string.Empty).Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }
    }
}
