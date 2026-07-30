using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;
using System.Data;

namespace Proyecto_Final.Services
{
    // CU-101 — Alta, edición, baja y listado de proveedores.
    public class PurchasingDbService
    {
        private readonly string _connectionString;

        public PurchasingDbService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        // ── CU-101 Proveedores ──────────────────────────────
        public async Task<List<SupplierListItemViewModel>> GetSuppliersAsync(bool? soloActivos, string? filtro)
        {
            var lista = new List<SupplierListItemViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Compras_ListarProveedores", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@SoloActivos", SqlDbType.Bit).Value = soloActivos.HasValue ? soloActivos.Value : DBNull.Value;
            command.Parameters.Add("@Filtro", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(filtro) ? DBNull.Value : filtro.Trim();
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new SupplierListItemViewModel
                {
                    ProveedorId = reader.GetInt32(0),
                    Nombre = reader.GetString(1),
                    Contacto = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Telefono = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Email = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Activo = reader.GetBoolean(5),
                    FechaCreacion = reader.GetDateTime(6)
                });
            }
            return lista;
        }

        // Crea o actualiza según ProveedorId (0 = nuevo, siguiendo el patrón de SaveAccount).
        public async Task<int> UpsertSupplierAsync(SupplierFormViewModel model)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            if (model.ProveedorId > 0)
            {
                await using var update = new SqlCommand("dbo.sp_Compras_ActualizarProveedor", connection) { CommandType = CommandType.StoredProcedure };
                update.Parameters.Add("@ProveedorId", SqlDbType.Int).Value = model.ProveedorId;
                update.Parameters.Add("@Nombre", SqlDbType.NVarChar, 150).Value = model.Nombre.Trim();
                update.Parameters.Add("@Contacto", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(model.Contacto) ? DBNull.Value : model.Contacto.Trim();
                update.Parameters.Add("@Telefono", SqlDbType.NVarChar, 30).Value = string.IsNullOrWhiteSpace(model.Telefono) ? DBNull.Value : model.Telefono.Trim();
                update.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(model.Email) ? DBNull.Value : model.Email.Trim();
                update.Parameters.Add("@Activo", SqlDbType.Bit).Value = model.Activo;
                await update.ExecuteNonQueryAsync();
                return model.ProveedorId;
            }

            await using var insert = new SqlCommand("dbo.sp_Compras_CrearProveedor", connection) { CommandType = CommandType.StoredProcedure };
            insert.Parameters.Add("@Nombre", SqlDbType.NVarChar, 150).Value = model.Nombre.Trim();
            insert.Parameters.Add("@Contacto", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(model.Contacto) ? DBNull.Value : model.Contacto.Trim();
            insert.Parameters.Add("@Telefono", SqlDbType.NVarChar, 30).Value = string.IsNullOrWhiteSpace(model.Telefono) ? DBNull.Value : model.Telefono.Trim();
            insert.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(model.Email) ? DBNull.Value : model.Email.Trim();
            insert.Parameters.Add("@Activo", SqlDbType.Bit).Value = model.Activo;
            var outputId = insert.Parameters.Add("@NuevoProveedorId", SqlDbType.Int);
            outputId.Direction = ParameterDirection.Output;
            await insert.ExecuteNonQueryAsync();
            return (int)outputId.Value;
        }

        public async Task DeactivateSupplierAsync(int proveedorId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Compras_DesactivarProveedor", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@ProveedorId", SqlDbType.Int).Value = proveedorId;
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        // ── CU-102 Órdenes de compra ─────────────────────────
        public async Task<List<PurchaseOrderListItemViewModel>> GetPurchaseOrdersAsync(string? estado, int? proveedorId)
        {
            var lista = new List<PurchaseOrderListItemViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Compras_ListarOrdenes", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@Estado", SqlDbType.NVarChar, 20).Value = string.IsNullOrWhiteSpace(estado) ? DBNull.Value : estado.Trim();
            command.Parameters.Add("@ProveedorId", SqlDbType.Int).Value = proveedorId.HasValue ? proveedorId.Value : DBNull.Value;
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new PurchaseOrderListItemViewModel
                {
                    OrdenCompraId = reader.GetInt32(0),
                    ProveedorId = reader.GetInt32(1),
                    ProveedorNombre = reader.GetString(2),
                    Estado = reader.GetString(3),
                    Notas = reader.IsDBNull(4) ? null : reader.GetString(4),
                    FechaCreacion = reader.GetDateTime(5),
                    FechaRecepcion = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    MontoTotal = reader.GetDecimal(7),
                    TotalOrdenado = reader.GetInt32(8),
                    TotalRecibido = reader.GetInt32(9)
                });
            }
            return lista;
        }

        public async Task<PurchaseOrderDetailViewModel?> GetPurchaseOrderDetailAsync(int ordenCompraId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Compras_ObtenerOrdenDetalle", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@OrdenCompraId", SqlDbType.Int).Value = ordenCompraId;
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync()) return null;

            var detalle = new PurchaseOrderDetailViewModel
            {
                OrdenCompraId = reader.GetInt32(0),
                ProveedorId = reader.GetInt32(1),
                ProveedorNombre = reader.GetString(2),
                Estado = reader.GetString(3),
                Notas = reader.IsDBNull(4) ? null : reader.GetString(4),
                FechaCreacion = reader.GetDateTime(5),
                FechaRecepcion = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
            };

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                detalle.Lineas.Add(new PurchaseOrderDetailLineViewModel
                {
                    DetalleOrdenCompraId = reader.GetInt32(0),
                    ProductoId = reader.GetInt32(2),
                    ProductoNombre = reader.GetString(3),
                    CantidadOrdenada = reader.GetInt32(4),
                    CantidadRecibida = reader.GetInt32(5),
                    PrecioUnitario = reader.GetDecimal(6)
                });
            }
            return detalle;
        }

        // Crea el encabezado y las líneas en una sola transacción (mismo patrón que CreateComboAsync).
        public async Task<int> CreatePurchaseOrderAsync(PurchaseOrderFormViewModel model, int usuarioId, string usuarioNombre)
        {
            var seleccionados = model.Productos.Where(p => p.Seleccionado).ToList();
            if (seleccionados.Count == 0)
            {
                throw new InvalidOperationException("Debe seleccionar al menos un producto para la orden.");
            }

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                int nuevaOrdenId;
                await using (var command = new SqlCommand("dbo.sp_Compras_CrearOrden", connection, (SqlTransaction)transaction))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("@ProveedorId", SqlDbType.Int).Value = model.ProveedorId;
                    command.Parameters.Add("@Notas", SqlDbType.NVarChar, 300).Value = string.IsNullOrWhiteSpace(model.Notas) ? DBNull.Value : model.Notas.Trim();
                    command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
                    command.Parameters.Add("@UsuarioNombre", SqlDbType.NVarChar, 150).Value = usuarioNombre;
                    var outputId = command.Parameters.Add("@NuevaOrdenId", SqlDbType.Int);
                    outputId.Direction = ParameterDirection.Output;
                    await command.ExecuteNonQueryAsync();
                    nuevaOrdenId = (int)outputId.Value;
                }

                foreach (var linea in seleccionados)
                {
                    await using var detalle = new SqlCommand("dbo.sp_Compras_AgregarDetalleOrden", connection, (SqlTransaction)transaction);
                    detalle.CommandType = CommandType.StoredProcedure;
                    detalle.Parameters.Add("@OrdenCompraId", SqlDbType.Int).Value = nuevaOrdenId;
                    detalle.Parameters.Add("@ProductoId", SqlDbType.Int).Value = linea.ProductoId;
                    detalle.Parameters.Add("@Cantidad", SqlDbType.Int).Value = linea.Cantidad;
                    detalle.Parameters.Add("@PrecioUnitario", SqlDbType.Decimal).Value = linea.PrecioUnitario;
                    await detalle.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return nuevaOrdenId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Confirma recepción de una línea (total o parcial); el SP suma el stock de forma atómica.
        public async Task ReceivePurchaseOrderLineAsync(int detalleOrdenCompraId, int cantidadRecibidaAhora, int usuarioId, string usuarioNombre)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Compras_RecibirDetalle", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@DetalleOrdenCompraId", SqlDbType.Int).Value = detalleOrdenCompraId;
            command.Parameters.Add("@CantidadRecibidaAhora", SqlDbType.Int).Value = cantidadRecibidaAhora;
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
            command.Parameters.Add("@UsuarioNombre", SqlDbType.NVarChar, 150).Value = usuarioNombre;
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        // ── CU-103 Sugerencias automáticas de compra ─────────
        // Reutiliza sp_Admin_GetPurchaseSuggestions (ya existente, de CU-241).
        public async Task<List<PurchaseOrderLineSelectionViewModel>> GetPurchaseSuggestionsAsync(int mesesRecientes, int mesesCobertura)
        {
            var sugerencias = new List<PurchaseOrderLineSelectionViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Admin_GetPurchaseSuggestions", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@MesesRecientes", SqlDbType.Int).Value = mesesRecientes;
            command.Parameters.Add("@MesesCobertura", SqlDbType.Int).Value = mesesCobertura;
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // Columnas: ProductoId, Nombre, StockActual, StockMinimo, UnidadesVendidasVentana,
                //           PromedioVentaMensual, MesesCobertura, CantidadSugerida, DatosInsuficientes
                sugerencias.Add(new PurchaseOrderLineSelectionViewModel
                {
                    ProductoId = reader.GetInt32(0),
                    Nombre = reader.GetString(1),
                    StockActual = reader.GetInt32(2),
                    PromedioVentaMensual = reader.GetDecimal(5),
                    Cantidad = Math.Max(reader.GetInt32(7), 1),
                    Seleccionado = true,
                    PrecioUnitario = 1.00m
                });
            }
            return sugerencias;
        }

        // ── CU-104 Histórico de precios ──────────────────────
        public async Task<List<PriceHistoryLineViewModel>> GetPriceHistoryAsync(int productoId)
        {
            var historial = new List<PriceHistoryLineViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Compras_HistoricoPrecios", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@ProductoId", SqlDbType.Int).Value = productoId;
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                historial.Add(new PriceHistoryLineViewModel
                {
                    OrdenCompraId = reader.GetInt32(0),
                    ProveedorId = reader.GetInt32(1),
                    ProveedorNombre = reader.GetString(2),
                    PrecioUnitario = reader.GetDecimal(3),
                    Estado = reader.GetString(4),
                    FechaCreacion = reader.GetDateTime(5)
                });
            }
            return historial;
        }

        // Diccionario ProductoId -> último precio pagado, para mostrarlo como alerta en Crear orden.
        public async Task<Dictionary<int, decimal>> GetLastPurchasePricesAsync()
        {
            var precios = new Dictionary<int, decimal>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Compras_UltimosPrecios", connection) { CommandType = CommandType.StoredProcedure };
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                precios[reader.GetInt32(0)] = reader.GetDecimal(1);
            }
            return precios;
        }
    }
}
