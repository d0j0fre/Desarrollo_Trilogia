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

        // Gráficos de detalle de un indicador del tablero (CU-131-03). Los títulos se definen aquí;
        // el procedimiento solo devuelve Grupo, Etiqueta, Valor y Detalle.
        private static readonly Dictionary<string, (string Titulo, bool Moneda)> DetailGroups = new()
        {
            ["ventas:categoria"] = ("Ventas por categoría", true),
            ["ventas:producto"] = ("Productos más vendidos", true),
            ["ventas:vendedor"] = ("Ventas por vendedor", true),
            ["stock:categoria"] = ("Productos con stock bajo o agotados por categoría", false),
            ["stock:producto"] = ("Productos más cerca de agotarse (existencias)", false),
            ["ruta:ruta"] = ("Pedidos en ruta por ruta", false),
            ["ruta:estado"] = ("Entregas de rutas abiertas por estado", false),
            ["cobros:cliente"] = ("Clientes con mayor saldo por cobrar", true),
            ["cobros:movimiento"] = ("Movimientos de crédito del período", true)
        };

        public async Task<List<DashboardDetailGroupViewModel>> GetDashboardDetailAsync(string indicator, DateTime desde, DateTime hasta)
        {
            var groups = new List<DashboardDetailGroupViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Reportes_DashboardDetalle", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@Indicador", SqlDbType.NVarChar, 20).Value = indicator;
            command.Parameters.Add("@Desde", SqlDbType.Date).Value = desde.Date;
            command.Parameters.Add("@Hasta", SqlDbType.Date).Value = hasta.Date;
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var key = reader.GetString(0);
                var group = groups.FirstOrDefault(g => g.Clave == key);
                if (group is null)
                {
                    var (title, money) = DetailGroups.TryGetValue($"{indicator}:{key}", out var info) ? info : (key, false);
                    group = new DashboardDetailGroupViewModel { Clave = key, Titulo = title, EsMoneda = money };
                    groups.Add(group);
                }

                group.Items.Add(new DashboardDetailItemViewModel
                {
                    Etiqueta = reader.GetString(1),
                    Valor = reader.GetDecimal(2),
                    Detalle = reader.IsDBNull(3) ? null : reader.GetString(3)
                });
            }

            // Un gráfico sin datos también se muestra (con su leyenda), en el orden definido arriba.
            foreach (var entry in DetailGroups.Where(e => e.Key.StartsWith(indicator + ":")))
            {
                var key = entry.Key[(indicator.Length + 1)..];
                if (groups.All(g => g.Clave != key))
                    groups.Add(new DashboardDetailGroupViewModel { Clave = key, Titulo = entry.Value.Titulo, EsMoneda = entry.Value.Moneda });
            }

            return groups
                .OrderBy(g => DetailGroups.Keys.ToList().IndexOf($"{indicator}:{g.Clave}"))
                .ToList();
        }

        public async Task<DetailedSalesReportViewModel> GetSalesReportAsync(
            SalesReportFilterViewModel filter,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Reportes_VentasDetallado", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@Desde", SqlDbType.Date).Value = filter.Desde.Date;
            command.Parameters.Add("@Hasta", SqlDbType.Date).Value = filter.Hasta.Date;
            command.Parameters.Add("@Agrupacion", SqlDbType.NVarChar, 20).Value = filter.Agrupacion;
            command.Parameters.Add("@Categoria", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(filter.Categoria) ? DBNull.Value : filter.Categoria.Trim();
            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            decimal total = 0, average = 0;
            int invoices = 0, orders = 0;
            if (await reader.ReadAsync(cancellationToken))
            {
                total = reader.GetDecimal(reader.GetOrdinal("TotalVentas"));
                invoices = reader.GetInt32(reader.GetOrdinal("Facturas"));
                orders = reader.GetInt32(reader.GetOrdinal("Pedidos"));
                average = reader.GetDecimal(reader.GetOrdinal("TicketPromedio"));
            }

            var rows = new List<SalesReportRowViewModel>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var groupDate = NullableDate(reader, "FechaGrupo");
                    rows.Add(new SalesReportRowViewModel
                    {
                        Clave = reader.GetString(reader.GetOrdinal("GrupoClave")),
                        // Los meses no dependen del idioma del servidor SQL (DATENAME los daba en inglés).
                        Etiqueta = filter.Agrupacion == SalesReportGrouping.Monthly && groupDate.HasValue
                            ? MonthLabel(groupDate.Value)
                            : reader.GetString(reader.GetOrdinal("GrupoNombre")),
                        Fecha = groupDate,
                        Categoria = NullableString(reader, "Categoria"),
                        TotalVentas = reader.GetDecimal(reader.GetOrdinal("TotalVentas")),
                        Facturas = reader.GetInt32(reader.GetOrdinal("Facturas")),
                        Pedidos = reader.GetInt32(reader.GetOrdinal("Pedidos")),
                        TicketPromedio = reader.GetDecimal(reader.GetOrdinal("TicketPromedio")),
                        Unidades = reader.GetInt32(reader.GetOrdinal("Unidades"))
                    });
                }
            }

            var categories = new List<string>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken)) categories.Add(reader.GetString(0));
            }

            return new DetailedSalesReportViewModel
            {
                Filtro = filter,
                TotalVentas = total,
                Facturas = invoices,
                Pedidos = orders,
                TicketPromedio = average,
                Filas = rows,
                Categorias = categories
            };
        }

        public async Task<SellerPerformanceViewModel> GetSellerPerformanceAsync(
            SellerPerformanceFilterViewModel filter,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Reportes_DesempenoVendedores", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@Desde", SqlDbType.Date).Value = filter.Desde.Date;
            command.Parameters.Add("@Hasta", SqlDbType.Date).Value = filter.Hasta.Date;
            command.Parameters.Add("@VendedorUsuarioId", SqlDbType.Int).Value = filter.VendedorUsuarioId.HasValue ? filter.VendedorUsuarioId.Value : DBNull.Value;
            command.Parameters.Add("@Orden", SqlDbType.NVarChar, 30).Value = filter.Orden;
            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = new List<SellerPerformanceRowViewModel>();
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new SellerPerformanceRowViewModel
                {
                    VendedorUsuarioId = reader.GetInt32(reader.GetOrdinal("VendedorUsuarioId")),
                    VendedorNombre = reader.GetString(reader.GetOrdinal("VendedorNombre")),
                    Ventas = reader.GetDecimal(reader.GetOrdinal("Ventas")),
                    Pedidos = reader.GetInt32(reader.GetOrdinal("Pedidos")),
                    Facturas = reader.GetInt32(reader.GetOrdinal("Facturas")),
                    TicketPromedio = reader.GetDecimal(reader.GetOrdinal("TicketPromedio")),
                    ClientesAtendidos = reader.GetInt32(reader.GetOrdinal("ClientesAtendidos")),
                    Meta = NullableDecimal(reader, "Meta"),
                    CumplimientoPorcentual = NullableDecimal(reader, "CumplimientoPorcentual")
                });
            }

            var sellers = new List<VendedorOptionViewModel>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    sellers.Add(new VendedorOptionViewModel
                    {
                        UsuarioId = reader.GetInt32(reader.GetOrdinal("UsuarioId")),
                        NombreCompleto = reader.GetString(reader.GetOrdinal("NombreCompleto")),
                        Correo = reader.IsDBNull(reader.GetOrdinal("Correo")) ? string.Empty : reader.GetString(reader.GetOrdinal("Correo"))
                    });
                }
            }

            return new SellerPerformanceViewModel { Filtro = filter, Filas = rows, Vendedores = sellers };
        }

        private static readonly string[] SpanishMonths =
        {
            "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
            "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
        };

        /// <summary>«Octubre 2026»: nombre del mes en español, sin depender de la cultura del servidor.</summary>
        public static string MonthLabel(DateTime date) => $"{SpanishMonths[date.Month - 1]} {date.Year}";

        private static string? NullableString(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        private static DateTime? NullableDate(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }

        private static decimal? NullableDecimal(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
        }

    }
}
