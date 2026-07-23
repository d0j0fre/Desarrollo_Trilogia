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

        // CU-241/242/243 — Trae los 3 reportes de inteligencia en una sola llamada.
        public async Task<InventoryIntelligenceViewModel> GetInventoryIntelligenceAsync()
        {
            var model = new InventoryIntelligenceViewModel();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // CU-241 — Sugerencia de compra según promedio de ventas de los últimos 3 meses.
            await using (var command = new SqlCommand("dbo.sp_Admin_GetPurchaseSuggestions", connection) { CommandType = CommandType.StoredProcedure })
            {
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    model.SugerenciasCompra.Add(new PurchaseSuggestionItem
                    {
                        ProductoId = reader.GetInt32(0),
                        Nombre = reader.GetString(1),
                        StockActual = reader.GetInt32(2),
                        PromedioVentaMensual = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                        CantidadSugerida = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                    });
                }
            }

            // CU-242 — Productos con stock que no ha rotado en los últimos 2 meses.
            await using (var command = new SqlCommand("dbo.sp_Admin_GetSlowMovingProducts", connection) { CommandType = CommandType.StoredProcedure })
            {
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    model.ProductosEstancados.Add(new SlowMovingProductItem
                    {
                        ProductoId = reader.GetInt32(0),
                        Nombre = reader.GetString(1),
                        Stock = reader.GetInt32(2),
                        VendidoUltimosMeses = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                    });
                }
            }

            // CU-243 — Total vendido agrupado por mes del año (todas las fechas históricas).
            await using (var command = new SqlCommand("dbo.sp_Admin_GetSeasonalSalesTrend", connection) { CommandType = CommandType.StoredProcedure })
            {
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    model.TendenciaEstacional.Add(new SeasonalTrendPoint
                    {
                        NumeroMes = reader.GetInt32(0),
                        NombreMes = reader.GetString(1),
                        TotalVendido = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2)
                    });
                }
            }

            return model;
        }
    }
}
