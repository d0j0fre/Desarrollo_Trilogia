using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;
using System.Data;

namespace Proyecto_Final.Services
{
    // CU-131 — Tablero gerencial de indicadores (solo lectura).
    public class ReportsDbService
    {
        private readonly string _connectionString;

        public ReportsDbService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        public async Task<ManagementDashboardViewModel> GetDashboardAsync(DateTime desde, DateTime hasta, string rango)
        {
            var model = new ManagementDashboardViewModel
            {
                Desde = desde,
                Hasta = hasta,
                Rango = rango
            };

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using (var command = new SqlCommand("dbo.sp_Reportes_DashboardKpis", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.Add("@Desde", SqlDbType.Date).Value = desde.Date;
                command.Parameters.Add("@Hasta", SqlDbType.Date).Value = hasta.Date;
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    model.Desde = reader.IsDBNull(0) ? desde : reader.GetDateTime(0);
                    model.Hasta = reader.IsDBNull(1) ? hasta : reader.GetDateTime(1);
                    model.VentasPeriodo = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                    model.FacturasPeriodo = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                    model.PedidosPeriodo = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                    model.TicketPromedio = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5);
                    model.StockBajo = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);
                    model.ProductosAgotados = reader.IsDBNull(7) ? 0 : reader.GetInt32(7);
                    model.PedidosEnRuta = reader.IsDBNull(8) ? 0 : reader.GetInt32(8);
                    model.CobrosPendientes = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9);
                    model.HayDatos = !reader.IsDBNull(10) && reader.GetBoolean(10);
                }
            }

            await using (var command = new SqlCommand("dbo.sp_Reportes_DashboardVentasSerie", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.Add("@Desde", SqlDbType.Date).Value = desde.Date;
                command.Parameters.Add("@Hasta", SqlDbType.Date).Value = hasta.Date;
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    model.Serie.Add(new SalesSeriePointViewModel
                    {
                        Dia = reader.GetDateTime(0),
                        Total = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1),
                        Facturas = reader.IsDBNull(2) ? 0 : reader.GetInt32(2)
                    });
                }
            }

            return model;
        }
    }
}
