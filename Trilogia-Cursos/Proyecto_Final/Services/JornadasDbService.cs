using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;
using System.Data;

namespace Proyecto_Final.Services
{
    public class JornadasDbService
    {
        private readonly string _connectionString;

        public JornadasDbService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        public async Task<List<JornadaListItemViewModel>> GetMisJornadasAsync(int usuarioId, DateTime desde, DateTime hasta)
        {
            var jornadas = new List<JornadaListItemViewModel>();

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_RRHH_ListarMisJornadas", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
            command.Parameters.Add("@Desde", SqlDbType.Date).Value = desde;
            command.Parameters.Add("@Hasta", SqlDbType.Date).Value = hasta;

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                jornadas.Add(ReadJornada(reader));
            }
            return jornadas;
        }

        public async Task<List<JornadaListItemViewModel>> GetJornadasPendientesAsync(DateTime desde, DateTime hasta)
        {
            var jornadas = new List<JornadaListItemViewModel>();

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_RRHH_ListarJornadasPendientes", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@Desde", SqlDbType.Date).Value = desde;
            command.Parameters.Add("@Hasta", SqlDbType.Date).Value = hasta;

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                jornadas.Add(ReadJornada(reader));
            }
            return jornadas;
        }

        // sp_RRHH_GuardarMiJornada no devuelve el Id nuevo (no tiene OUTPUT ni SELECT final);
        // se deja así para no modificar un procedimiento ya usado por Danny.
        public async Task GuardarMiJornadaAsync(int usuarioId, JornadaFormViewModel model)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_RRHH_GuardarMiJornada", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
            command.Parameters.Add("@JornadaId", SqlDbType.BigInt).Value = model.JornadaId.HasValue ? model.JornadaId.Value : DBNull.Value;
            command.Parameters.Add("@Fecha", SqlDbType.Date).Value = model.Fecha;
            command.Parameters.Add("@HorasOrdinarias", SqlDbType.Decimal).Value = model.HorasOrdinarias;
            command.Parameters.Add("@HorasExtra", SqlDbType.Decimal).Value = model.HorasExtra;
            command.Parameters.Add("@HorasAusencia", SqlDbType.Decimal).Value = model.HorasAusencia;
            command.Parameters.Add("@Observaciones", SqlDbType.NVarChar, 500).Value = string.IsNullOrWhiteSpace(model.Observaciones) ? DBNull.Value : model.Observaciones;
            command.Parameters.Add("@Enviar", SqlDbType.Bit).Value = model.Enviar;
            command.Parameters.Add("@IdempotencyKey", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
            command.Parameters.Add("@VersionFila", SqlDbType.Binary, 8).Value =
                string.IsNullOrEmpty(model.VersionFilaBase64) ? DBNull.Value : Convert.FromBase64String(model.VersionFilaBase64);

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task ResolverJornadaAsync(int supervisorUsuarioId, string supervisorNombre, JornadaDecisionViewModel model)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_RRHH_ResolverJornada", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.Add("@JornadaId", SqlDbType.BigInt).Value = model.JornadaId;
            command.Parameters.Add("@Decision", SqlDbType.NVarChar, 20).Value = model.Decision;
            command.Parameters.Add("@RespuestaSupervisor", SqlDbType.NVarChar, 500).Value =
                string.IsNullOrWhiteSpace(model.RespuestaSupervisor) ? DBNull.Value : model.RespuestaSupervisor;
            command.Parameters.Add("@SupervisorUsuarioId", SqlDbType.Int).Value = supervisorUsuarioId;
            command.Parameters.Add("@SupervisorNombre", SqlDbType.NVarChar, 150).Value = supervisorNombre;
            command.Parameters.Add("@VersionFila", SqlDbType.Binary, 8).Value = Convert.FromBase64String(model.VersionFilaBase64);

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        private static JornadaListItemViewModel ReadJornada(SqlDataReader reader)
        {
            return new JornadaListItemViewModel
            {
                JornadaId = reader.GetInt64(0),
                EmpleadoId = reader.GetInt32(1),
                NombreCompleto = reader.GetString(2),
                Fecha = reader.GetDateTime(3),
                HorasOrdinarias = reader.GetDecimal(4),
                HorasExtra = reader.GetDecimal(5),
                HorasAusencia = reader.GetDecimal(6),
                Observaciones = reader.IsDBNull(7) ? null : reader.GetString(7),
                Estado = reader.GetString(8),
                RespuestaSupervisor = reader.IsDBNull(9) ? null : reader.GetString(9),
                VersionFilaBase64 = Convert.ToBase64String((byte[])reader["VersionFila"])
            };
        }
    }
}
