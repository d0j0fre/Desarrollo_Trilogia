using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;
using System.Data;

namespace Proyecto_Final.Services
{
    // BUG-08-01 / BUG-10-08 — Bandeja de avisos del usuario (migración 0221).
    // Todos los procedimientos filtran por el usuario de la sesión.
    public class NotificationsDbService
    {
        private readonly string _connectionString;

        public NotificationsDbService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        public async Task<List<UserNotificationViewModel>> GetAsync(int usuarioId)
        {
            var lista = new List<UserNotificationViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Notificaciones_Listar", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new UserNotificationViewModel
                {
                    NotificacionId = reader.GetInt32(reader.GetOrdinal("NotificacionId")),
                    Tipo = reader.GetString(reader.GetOrdinal("Tipo")),
                    Titulo = reader.GetString(reader.GetOrdinal("Titulo")),
                    Mensaje = reader.GetString(reader.GetOrdinal("Mensaje")),
                    EntidadTipo = reader.IsDBNull(reader.GetOrdinal("EntidadTipo")) ? string.Empty : reader.GetString(reader.GetOrdinal("EntidadTipo")),
                    EntidadId = reader.IsDBNull(reader.GetOrdinal("EntidadId")) ? null : reader.GetInt32(reader.GetOrdinal("EntidadId")),
                    Leida = reader.GetBoolean(reader.GetOrdinal("Leida")),
                    FechaCreacion = reader.GetDateTime(reader.GetOrdinal("FechaCreacion"))
                });
            }
            return lista;
        }

        public async Task<int> CountUnreadAsync(int usuarioId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Notificaciones_ContarNoLeidas", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
            await connection.OpenAsync();
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        public async Task MarkReadAsync(int usuarioId, int? notificacionId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Notificaciones_MarcarLeidas", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
            command.Parameters.Add("@NotificacionId", SqlDbType.Int).Value = notificacionId.HasValue ? notificacionId.Value : DBNull.Value;
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }
    }
}
