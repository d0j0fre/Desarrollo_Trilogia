using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;
using System.Data;

namespace Proyecto_Final.Services
{
    public class PayrollDbService
    {
        private readonly string _connectionString;

        public PayrollDbService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        public async Task<int> CrearPeriodoAsync(PayrollPeriodFormViewModel model)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Planilla_CrearPeriodo", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@TipoPeriodo", SqlDbType.NVarChar, 20).Value = model.TipoPeriodo;
            command.Parameters.Add("@FechaInicio", SqlDbType.Date).Value = model.FechaInicio;
            command.Parameters.Add("@FechaFin", SqlDbType.Date).Value = model.FechaFin;

            await connection.OpenAsync();
            var resultado = await command.ExecuteScalarAsync();
            return Convert.ToInt32(resultado);
        }

        public async Task<List<PayrollPeriodListItemViewModel>> GetPeriodosAsync()
        {
            var periodos = new List<PayrollPeriodListItemViewModel>();

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(
                @"SELECT PeriodoPlanillaId, TipoPeriodo, FechaInicio, FechaFin, Estado, FechaCalculo, FechaAprobacion, FechaPago
                  FROM dbo.PeriodosPlanilla
                  ORDER BY FechaInicio DESC", connection);

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                periodos.Add(new PayrollPeriodListItemViewModel
                {
                    PeriodoPlanillaId = reader.GetInt32(0),
                    TipoPeriodo = reader.GetString(1),
                    FechaInicio = reader.GetDateTime(2),
                    FechaFin = reader.GetDateTime(3),
                    Estado = reader.GetString(4),
                    FechaCalculo = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    FechaAprobacion = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    FechaPago = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
                });
            }
            return periodos;
        }

        // El SP no expone nombre/correo en su SELECT final; tras calcular, se recarga con GetDetalleByPeriodoAsync
        public async Task CalcularPeriodoAsync(int periodoId, int usuarioId, string usuarioNombre)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Planilla_CalcularPeriodo", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@PeriodoPlanillaId", SqlDbType.Int).Value = periodoId;
            command.Parameters.Add("@UsuarioCalculoId", SqlDbType.Int).Value = usuarioId;
            command.Parameters.Add("@UsuarioCalculoNombre", SqlDbType.NVarChar, 150).Value = usuarioNombre;

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<PayrollDetailListItemViewModel>> GetDetalleByPeriodoAsync(int periodoId)
        {
            var detalles = new List<PayrollDetailListItemViewModel>();

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(
                @"SELECT pd.PlanillaDetalleId, pd.EmpleadoId, u.NombreCompleto, u.Correo,
                         pd.SalarioBase, pd.HorasExtraPagadas, pd.MontoHorasExtra, pd.MontoComisiones,
                         pd.SalarioBruto, pd.DeduccionCcss, pd.DeduccionRenta, pd.TotalDeducciones, pd.SalarioNeto
                  FROM dbo.PlanillaDetalle pd
                  INNER JOIN dbo.Empleados e ON e.EmpleadoId = pd.EmpleadoId
                  INNER JOIN dbo.Usuarios u ON u.UsuarioId = e.UsuarioId
                  WHERE pd.PeriodoPlanillaId = @PeriodoPlanillaId
                  ORDER BY u.NombreCompleto", connection);
            command.Parameters.Add("@PeriodoPlanillaId", SqlDbType.Int).Value = periodoId;

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                detalles.Add(ReadDetalle(reader));
            }
            return detalles;
        }

        public async Task AprobarPeriodoAsync(int periodoId, int usuarioId, string usuarioNombre)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Planilla_AprobarPeriodo", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@PeriodoPlanillaId", SqlDbType.Int).Value = periodoId;
            command.Parameters.Add("@UsuarioAprobacionId", SqlDbType.Int).Value = usuarioId;
            command.Parameters.Add("@UsuarioAprobacionNombre", SqlDbType.NVarChar, 150).Value = usuarioNombre;

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task MarcarPagadoAsync(int periodoId, int usuarioId, string usuarioNombre)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Planilla_MarcarPagado", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@PeriodoPlanillaId", SqlDbType.Int).Value = periodoId;
            command.Parameters.Add("@UsuarioPagoId", SqlDbType.Int).Value = usuarioId;
            command.Parameters.Add("@UsuarioPagoNombre", SqlDbType.NVarChar, 150).Value = usuarioNombre;

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task RevertirPeriodoAsync(int periodoId, int usuarioId, string usuarioNombre, string motivo)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Planilla_RevertirPeriodo", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@PeriodoPlanillaId", SqlDbType.Int).Value = periodoId;
            command.Parameters.Add("@UsuarioReversionId", SqlDbType.Int).Value = usuarioId;
            command.Parameters.Add("@UsuarioReversionNombre", SqlDbType.NVarChar, 150).Value = usuarioNombre;
            command.Parameters.Add("@MotivoReversion", SqlDbType.NVarChar, 500).Value = motivo;

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task<PayrollReceiptViewModel?> ObtenerDetalleEmpleadoAsync(int planillaDetalleId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Planilla_ObtenerDetalleEmpleado", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@PlanillaDetalleId", SqlDbType.Int).Value = planillaDetalleId;

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new PayrollReceiptViewModel
            {
                PlanillaDetalleId = reader.GetInt32(reader.GetOrdinal("PlanillaDetalleId")),
                EmpleadoId = reader.GetInt32(reader.GetOrdinal("EmpleadoId")),
                SalarioBase = reader.GetDecimal(reader.GetOrdinal("SalarioBase")),
                HorasExtraPagadas = reader.GetDecimal(reader.GetOrdinal("HorasExtraPagadas")),
                MontoHorasExtra = reader.GetDecimal(reader.GetOrdinal("MontoHorasExtra")),
                MontoComisiones = reader.GetDecimal(reader.GetOrdinal("MontoComisiones")),
                SalarioBruto = reader.GetDecimal(reader.GetOrdinal("SalarioBruto")),
                DeduccionCcss = reader.GetDecimal(reader.GetOrdinal("DeduccionCcss")),
                DeduccionRenta = reader.GetDecimal(reader.GetOrdinal("DeduccionRenta")),
                TotalDeducciones = reader.GetDecimal(reader.GetOrdinal("TotalDeducciones")),
                SalarioNeto = reader.GetDecimal(reader.GetOrdinal("SalarioNeto")),
                NombreCompleto = reader.GetString(reader.GetOrdinal("NombreCompleto")),
                Correo = reader.GetString(reader.GetOrdinal("Correo")),
                TipoPeriodo = reader.GetString(reader.GetOrdinal("TipoPeriodo")),
                FechaInicio = reader.GetDateTime(reader.GetOrdinal("FechaInicio")),
                FechaFin = reader.GetDateTime(reader.GetOrdinal("FechaFin"))
            };
        }

        public async Task RegistrarEnvioComprobanteAsync(int planillaDetalleId, string correo, bool exitoso, string? mensajeError)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Planilla_MarcarComprobanteEnviado", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@PlanillaDetalleId", SqlDbType.Int).Value = planillaDetalleId;
            command.Parameters.Add("@CorreoDestino", SqlDbType.NVarChar, 300).Value = correo;
            command.Parameters.Add("@Exitoso", SqlDbType.Bit).Value = exitoso;
            command.Parameters.Add("@MensajeError", SqlDbType.NVarChar, 1000).Value =
                string.IsNullOrWhiteSpace(mensajeError) ? DBNull.Value : mensajeError;

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        private static PayrollDetailListItemViewModel ReadDetalle(SqlDataReader reader)
        {
            return new PayrollDetailListItemViewModel
            {
                PlanillaDetalleId = reader.GetInt32(reader.GetOrdinal("PlanillaDetalleId")),
                EmpleadoId = reader.GetInt32(reader.GetOrdinal("EmpleadoId")),
                NombreCompleto = reader.GetString(reader.GetOrdinal("NombreCompleto")),
                Correo = reader.GetString(reader.GetOrdinal("Correo")),
                SalarioBase = reader.GetDecimal(reader.GetOrdinal("SalarioBase")),
                HorasExtraPagadas = reader.GetDecimal(reader.GetOrdinal("HorasExtraPagadas")),
                MontoHorasExtra = reader.GetDecimal(reader.GetOrdinal("MontoHorasExtra")),
                MontoComisiones = reader.GetDecimal(reader.GetOrdinal("MontoComisiones")),
                SalarioBruto = reader.GetDecimal(reader.GetOrdinal("SalarioBruto")),
                DeduccionCcss = reader.GetDecimal(reader.GetOrdinal("DeduccionCcss")),
                DeduccionRenta = reader.GetDecimal(reader.GetOrdinal("DeduccionRenta")),
                TotalDeducciones = reader.GetDecimal(reader.GetOrdinal("TotalDeducciones")),
                SalarioNeto = reader.GetDecimal(reader.GetOrdinal("SalarioNeto"))
            };
        }
    }
}
