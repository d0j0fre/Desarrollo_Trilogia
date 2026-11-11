using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;
using System.Data;
using System.Text.Json;

namespace Proyecto_Final.Services
{
    // CU-105 / CU-106 — Liquidación de cobros y de mercadería de una ruta.
    // Acceso a datos con ADO.NET + stored procedures (migraciones 0285 y 0286).
    public class FinanceDbService
    {
        private readonly string _connectionString;

        public FinanceDbService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        // ── CU-106 Liquidación financiera de cobros de ruta ──
        public async Task<CashSettlementPrepareViewModel?> PrepareCashSettlementAsync(int rutaId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_LiquidacionCobros_Preparar", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@RutaId", SqlDbType.Int).Value = rutaId;
            await connection.OpenAsync();
            SqlDataReader reader;
            try
            {
                reader = await command.ExecuteReaderAsync();
            }
            catch (SqlException ex) when (ex.Number == 53070)
            {
                return null;   // La ruta no existe.
            }

            await using (reader)
            {
                if (!await reader.ReadAsync()) return null;

                var model = new CashSettlementPrepareViewModel
                {
                    RutaId = reader.GetInt32(0),
                    RutaCodigo = reader.GetString(1),
                    EstadoRuta = reader.GetString(2),
                    Liquidada = reader.GetBoolean(3),
                    YaLiquidadaFinanciera = reader.GetInt32(4) > 0,
                    EsperadoEfectivo = reader.GetDecimal(5),
                    EsperadoOtros = reader.GetDecimal(6),
                    Zona = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    ChoferNombre = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    TotalPedidos = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                    Entregados = reader.IsDBNull(10) ? 0 : reader.GetInt32(10)
                };

                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        model.Pedidos.Add(new CashSettlementOrderLine
                        {
                            PedidoId = reader.GetInt32(0),
                            Total = reader.GetDecimal(1),
                            MetodoPago = reader.GetString(2),
                            EstadoPago = reader.GetString(3),
                            Cliente = reader.GetString(4)
                        });
                    }
                }
                return model;
            }
        }

        /// <summary>Detalle de comprobantes en el JSON que espera el procedimiento (solo filas con algo escrito).</summary>
        public static string BuildVouchersJson(IEnumerable<CashSettlementVoucher>? comprobantes)
        {
            var filas = (comprobantes ?? Enumerable.Empty<CashSettlementVoucher>())
                .Where(c => c.Monto != 0 || !string.IsNullOrWhiteSpace(c.Tipo) || !string.IsNullOrWhiteSpace(c.Referencia))
                .Select(c => new { Tipo = c.Tipo?.Trim(), Referencia = c.Referencia?.Trim(), c.Monto })
                .ToList();
            return filas.Count == 0 ? string.Empty : JsonSerializer.Serialize(filas);
        }

        public async Task<CashSettlementResult> RegisterCashSettlementAsync(
            CashSettlementFormViewModel model, string comprobantesJson, int usuarioId, string usuarioNombre)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_LiquidacionCobros_Registrar", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@RutaId", SqlDbType.Int).Value = model.RutaId;
            command.Parameters.Add("@MontoEfectivoRecibido", SqlDbType.Decimal).Value = model.MontoEfectivoRecibido;
            command.Parameters["@MontoEfectivoRecibido"].Precision = 18;
            command.Parameters["@MontoEfectivoRecibido"].Scale = 2;
            command.Parameters.Add("@MontoComprobantes", SqlDbType.Decimal).Value = model.MontoComprobantes;
            command.Parameters["@MontoComprobantes"].Precision = 18;
            command.Parameters["@MontoComprobantes"].Scale = 2;
            command.Parameters.Add("@Observaciones", SqlDbType.NVarChar, 400).Value = string.IsNullOrWhiteSpace(model.Observaciones) ? DBNull.Value : model.Observaciones.Trim();
            command.Parameters.Add("@ComprobantesJson", SqlDbType.NVarChar, -1).Value = string.IsNullOrWhiteSpace(comprobantesJson) ? DBNull.Value : comprobantesJson;
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId > 0 ? usuarioId : DBNull.Value;
            command.Parameters.Add("@Nombre", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(usuarioNombre) ? DBNull.Value : usuarioNombre.Trim();
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new CashSettlementResult(
                    reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2),
                    reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    reader.IsDBNull(4) ? 0m : reader.GetDecimal(4));
            }
            return new CashSettlementResult(0, string.Empty, 0m, string.Empty, 0m);
        }

        public async Task<List<CashSettlementListItemViewModel>> GetCashSettlementsAsync(string? estado)
        {
            var lista = new List<CashSettlementListItemViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_LiquidacionCobros_List", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@Estado", SqlDbType.NVarChar, 20).Value = string.IsNullOrWhiteSpace(estado) ? DBNull.Value : estado.Trim();
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new CashSettlementListItemViewModel
                {
                    LiquidacionCobrosId = reader.GetInt32(0),
                    RutaId = reader.GetInt32(1),
                    RutaCodigo = reader.GetString(2),
                    MontoEsperadoEfectivo = reader.GetDecimal(3),
                    MontoEsperadoOtros = reader.GetDecimal(4),
                    MontoEfectivoRecibido = reader.GetDecimal(5),
                    MontoComprobantes = reader.GetDecimal(6),
                    Diferencia = reader.GetDecimal(7),
                    Estado = reader.GetString(8),
                    LiquidadoPorNombre = reader.GetString(9),
                    FechaLiquidacion = reader.GetDateTime(10),
                    ChoferNombre = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                    Anulada = !reader.IsDBNull(12) && reader.GetBoolean(12),
                    MotivoAnulacion = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                    FechaAnulacion = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
                    AnuladaPorNombre = reader.IsDBNull(15) ? string.Empty : reader.GetString(15)
                });
            }
            return lista;
        }

        /// <summary>G2-37: anula una liquidación con motivo. Devuelve el código de la ruta.</summary>
        public async Task<string> CancelCashSettlementAsync(int liquidacionId, string motivo, int usuarioId, string usuarioNombre)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_LiquidacionCobros_Anular", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@LiquidacionCobrosId", SqlDbType.Int).Value = liquidacionId;
            command.Parameters.Add("@Motivo", SqlDbType.NVarChar, 300).Value = string.IsNullOrWhiteSpace(motivo) ? DBNull.Value : motivo.Trim();
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId > 0 ? usuarioId : DBNull.Value;
            command.Parameters.Add("@Nombre", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(usuarioNombre) ? DBNull.Value : usuarioNombre.Trim();
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() && !reader.IsDBNull(1) ? reader.GetString(1) : string.Empty;
        }

        /// <summary>BUG-10-05: faltantes a cargo de los choferes.</summary>
        public async Task<List<DriverShortageViewModel>> GetDriverShortagesAsync(string? estado)
        {
            var lista = new List<DriverShortageViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_ChoferFaltantes_List", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@Estado", SqlDbType.NVarChar, 20).Value = string.IsNullOrWhiteSpace(estado) ? DBNull.Value : estado.Trim();
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new DriverShortageViewModel
                {
                    FaltanteId = reader.GetInt32(0),
                    ChoferUsuarioId = reader.GetInt32(1),
                    ChoferNombre = reader.GetString(2),
                    RutaId = reader.GetInt32(3),
                    RutaCodigo = reader.GetString(4),
                    LiquidacionCobrosId = reader.GetInt32(5),
                    Monto = reader.GetDecimal(6),
                    Estado = reader.GetString(7),
                    FechaRegistro = reader.GetDateTime(8)
                });
            }
            return lista;
        }

        /// <summary>BUG-10-03: datos del recibo; null si la liquidación no existe.</summary>
        public async Task<CashSettlementReceiptViewModel?> GetCashSettlementReceiptAsync(int liquidacionId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_LiquidacionCobros_GetRecibo", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@LiquidacionCobrosId", SqlDbType.Int).Value = liquidacionId;
            await connection.OpenAsync();
            SqlDataReader reader;
            try
            {
                reader = await command.ExecuteReaderAsync();
            }
            catch (SqlException ex) when (ex.Number == 53403)
            {
                return null;
            }

            await using (reader)
            {
                if (!await reader.ReadAsync()) return null;
                var model = new CashSettlementReceiptViewModel
                {
                    LiquidacionCobrosId = reader.GetInt32(0),
                    RutaId = reader.GetInt32(1),
                    RutaCodigo = reader.GetString(2),
                    Zona = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    ChoferNombre = reader.GetString(4),
                    VehiculoPlaca = reader.GetString(5),
                    VehiculoDescripcion = reader.GetString(6),
                    MontoEsperadoEfectivo = reader.GetDecimal(7),
                    MontoEsperadoOtros = reader.GetDecimal(8),
                    MontoEfectivoRecibido = reader.GetDecimal(9),
                    MontoComprobantes = reader.GetDecimal(10),
                    Diferencia = reader.GetDecimal(11),
                    Estado = reader.GetString(12),
                    Observaciones = reader.GetString(13),
                    LiquidadoPorNombre = reader.GetString(14),
                    FechaLiquidacion = reader.GetDateTime(15),
                    Anulada = reader.GetBoolean(16),
                    MotivoAnulacion = reader.GetString(17)
                };

                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        model.Comprobantes.Add(new CashSettlementVoucher
                        {
                            Tipo = reader.GetString(0),
                            Referencia = reader.GetString(1),
                            Monto = reader.GetDecimal(2)
                        });
                    }
                }
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        model.Pedidos.Add(new CashSettlementReceiptOrder
                        {
                            PedidoId = reader.GetInt32(0),
                            Cliente = reader.GetString(1),
                            MetodoPago = reader.GetString(2),
                            Total = reader.GetDecimal(3)
                        });
                    }
                }
                return model;
            }
        }

        // ── CU-105 Liquidación de mercadería de una ruta completada (BUG-10-07) ──
        public async Task<List<RouteMerchandiseListItemViewModel>> GetMerchandiseRoutesAsync()
        {
            var lista = new List<RouteMerchandiseListItemViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_RutaMercaderia_List", connection) { CommandType = CommandType.StoredProcedure };
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new RouteMerchandiseListItemViewModel
                {
                    RutaId = reader.GetInt32(0),
                    Codigo = reader.GetString(1),
                    Zona = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    ChoferNombre = reader.GetString(3),
                    FechaCierre = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    PedidosNoEntregados = reader.GetInt32(5),
                    Unidades = reader.GetInt32(6),
                    Pendiente = reader.GetBoolean(7),
                    FechaLiquidacion = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                    LiquidadoPorNombre = reader.GetString(9)
                });
            }
            return lista;
        }

        public async Task<RouteMerchandisePrepareViewModel?> PrepareMerchandiseAsync(int rutaId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_RutaMercaderia_Preparar", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@RutaId", SqlDbType.Int).Value = rutaId;
            await connection.OpenAsync();
            SqlDataReader reader;
            try
            {
                reader = await command.ExecuteReaderAsync();
            }
            catch (SqlException ex) when (ex.Number == 53410)
            {
                return null;
            }

            await using (reader)
            {
                if (!await reader.ReadAsync()) return null;
                var model = new RouteMerchandisePrepareViewModel
                {
                    RutaId = reader.GetInt32(0),
                    RutaCodigo = reader.GetString(1),
                    EstadoRuta = reader.GetString(2),
                    Liquidada = reader.GetBoolean(3),
                    YaRegistrada = reader.GetInt32(4) > 0,
                    Zona = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    ChoferNombre = reader.GetString(6),
                    PedidosNoEntregados = reader.GetInt32(7)
                };
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        model.Lineas.Add(new RouteMerchandiseLine
                        {
                            PedidoId = reader.GetInt32(0),
                            ProductoId = reader.GetInt32(1),
                            ProductoNombre = reader.GetString(2),
                            Cantidad = reader.GetInt32(3)
                        });
                    }
                }
                return model;
            }
        }

        public async Task<RouteMerchandiseResult> RegisterMerchandiseAsync(RouteMerchandiseFormViewModel model, int usuarioId, string usuarioNombre)
        {
            var detalle = model.Lineas.Select(l => new { l.PedidoId, l.ProductoId, Cantidad = l.Contado });
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_RutaMercaderia_Registrar", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@RutaId", SqlDbType.Int).Value = model.RutaId;
            command.Parameters.Add("@DetalleJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(detalle);
            command.Parameters.Add("@Observaciones", SqlDbType.NVarChar, 400).Value = string.IsNullOrWhiteSpace(model.Observaciones) ? DBNull.Value : model.Observaciones.Trim();
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
            command.Parameters.Add("@Nombre", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(usuarioNombre) ? DBNull.Value : usuarioNombre.Trim();
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new RouteMerchandiseResult(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
            }
            return new RouteMerchandiseResult(0, 0, 0);
        }
    }
}
