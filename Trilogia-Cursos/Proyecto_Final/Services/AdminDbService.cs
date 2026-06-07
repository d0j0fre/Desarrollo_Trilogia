using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Models.Store;
using System.Data;

namespace Proyecto_Final.Services
{
    public class AdminDbService
    {
        private readonly string _connectionString;

        public AdminDbService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontrÃ‡Ã¼ la cadena de conexiÃ‡Ã¼n DefaultConnection.");
        }

        public async Task<DashboardSummaryViewModel> GetDashboardSummaryAsync()
        {
            var model = new DashboardSummaryViewModel();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using (var command = new SqlCommand("dbo.sp_Admin_DashboardSummary", connection))
            {
                command.CommandType = CommandType.StoredProcedure;

                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    model.TotalProductos = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    model.ProductosActivos = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    model.StockBajo = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    model.ProductosAgotados = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                    model.TotalPedidos = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                    model.PedidosPendientes = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                    model.PedidosEnProceso = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);
                    model.PedidosEntregados = reader.IsDBNull(7) ? 0 : reader.GetInt32(7);
                    model.VentasTotales = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8);
                    model.VentasMesActual = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9);
                }
            }

            await using (var command = new SqlCommand("dbo.sp_Admin_DashboardLowStock", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    model.ProductosCriticos.Add(new LowStockProductViewModel
                    {
                        ProductoId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        Nombre = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        Categoria = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Stock = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                        EstadoStock = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
                    });
                }
            }

            await using (var command = new SqlCommand("dbo.sp_Admin_DashboardRecentOrders", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    model.PedidosRecientes.Add(new RecentOrderViewModel
                    {
                        PedidoId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        Cliente = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        FechaPedido = reader.IsDBNull(2) ? DateTime.MinValue : reader.GetDateTime(2),
                        Estado = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        Total = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4)
                    });
                }
            }

            return model;
        }

        public async Task<List<ProductAdminViewModel>> GetProductsAsync(string? filtro)
        {
            var products = new List<ProductAdminViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Admin_GetProducts", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@Filtro", SqlDbType.NVarChar, 200).Value = string.IsNullOrWhiteSpace(filtro) ? DBNull.Value : filtro.Trim();
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                products.Add(new ProductAdminViewModel
                {
                    ProductoId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    Nombre = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Categoria = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Descripcion = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Precio = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                    Stock = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    EstadoStock = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Activo = !reader.IsDBNull(7) && reader.GetBoolean(7),
                    FechaCreacion = reader.IsDBNull(8) ? DateTime.MinValue : reader.GetDateTime(8),
                    ImagenUrl = reader.FieldCount > 9 && !reader.IsDBNull(9) ? reader.GetString(9) : string.Empty,
                    EsDestacado = reader.FieldCount > 10 && !reader.IsDBNull(10) && reader.GetBoolean(10)
                });
            }
            return products;
        }

        public async Task<List<ProductAdminViewModel>> GetActiveProductsForSelectAsync()
        {
            var products = new List<ProductAdminViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Admin_GetActiveProductsForSelect", connection);
            command.CommandType = CommandType.StoredProcedure;
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                products.Add(new ProductAdminViewModel
                {
                    ProductoId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    Nombre = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Categoria = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Descripcion = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Precio = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                    Stock = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    EstadoStock = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Activo = !reader.IsDBNull(7) && reader.GetBoolean(7),
                    FechaCreacion = reader.IsDBNull(8) ? DateTime.MinValue : reader.GetDateTime(8),
                    ImagenUrl = reader.FieldCount > 9 && !reader.IsDBNull(9) ? reader.GetString(9) : string.Empty,
                    EsDestacado = reader.FieldCount > 10 && !reader.IsDBNull(10) && reader.GetBoolean(10)
                });
            }
            return products;
        }

        public async Task<ProductFormViewModel?> GetProductByIdAsync(int productoId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Admin_GetProductById", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ProductoId", productoId);
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return new ProductFormViewModel
            {
                ProductoId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                Nombre = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Categoria = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Descripcion = reader.IsDBNull(3) ? null : reader.GetString(3),
                Precio = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                Stock = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                Activo = !reader.IsDBNull(6) && reader.GetBoolean(6),
                ImagenUrl = reader.FieldCount > 7 && !reader.IsDBNull(7) ? reader.GetString(7) : string.Empty,
                EsDestacado = reader.FieldCount > 8 && !reader.IsDBNull(8) && reader.GetBoolean(8)
            };
        }

        public async Task CreateProductAsync(ProductFormViewModel model, int usuarioId, string usuarioNombre)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                int productoId;
                await using (var command = new SqlCommand("dbo.sp_Admin_CreateProduct", connection, (SqlTransaction)transaction))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@Nombre", model.Nombre.Trim());
                    command.Parameters.AddWithValue("@Categoria", model.Categoria.Trim());
                    command.Parameters.AddWithValue("@Descripcion", string.IsNullOrWhiteSpace(model.Descripcion) ? DBNull.Value : model.Descripcion.Trim());
                    command.Parameters.AddWithValue("@Precio", model.Precio);
                    command.Parameters.AddWithValue("@Stock", model.Stock);
                    command.Parameters.AddWithValue("@Activo", model.Activo);
                    command.Parameters.AddWithValue("@ImagenUrl", string.IsNullOrWhiteSpace(model.ImagenUrl) ? DBNull.Value : model.ImagenUrl.Trim());
                    command.Parameters.AddWithValue("@EsDestacado", model.EsDestacado);
                    productoId = Convert.ToInt32(await command.ExecuteScalarAsync());
                }

                await using (var syncCategoria = new SqlCommand("UPDATE dbo.Productos SET CategoriaId = (SELECT TOP 1 CategoriaId FROM dbo.Categorias WHERE Nombre = @Categoria) WHERE ProductoId = @ProductoId;", connection, (SqlTransaction)transaction))
                {
                    syncCategoria.Parameters.AddWithValue("@Categoria", model.Categoria.Trim());
                    syncCategoria.Parameters.AddWithValue("@ProductoId", productoId);
                    await syncCategoria.ExecuteNonQueryAsync();
                }
                if (model.Stock > 0)
                {
                    await CreateMovementInternalAsync(connection, (SqlTransaction)transaction, productoId, "Entrada", model.Stock, 0, model.Stock, "Registro inicial del producto.", usuarioId, usuarioNombre);
                }
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateProductAsync(ProductFormViewModel model, int usuarioId, string usuarioNombre)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                int previousStock;
                await using (var selectCommand = new SqlCommand("dbo.sp_Admin_GetProductStock", connection, (SqlTransaction)transaction))
                {
                    selectCommand.CommandType = CommandType.StoredProcedure;
                    selectCommand.Parameters.AddWithValue("@ProductoId", model.ProductoId);
                    var result = await selectCommand.ExecuteScalarAsync();
                    if (result is null) throw new InvalidOperationException("El producto no existe.");
                    previousStock = Convert.ToInt32(result);
                }

                await using (var updateCommand = new SqlCommand("dbo.sp_Admin_UpdateProduct", connection, (SqlTransaction)transaction))
                {
                    updateCommand.CommandType = CommandType.StoredProcedure;
                    updateCommand.Parameters.AddWithValue("@ProductoId", model.ProductoId);
                    updateCommand.Parameters.AddWithValue("@Nombre", model.Nombre.Trim());
                    updateCommand.Parameters.AddWithValue("@Categoria", model.Categoria.Trim());
                    updateCommand.Parameters.AddWithValue("@Descripcion", string.IsNullOrWhiteSpace(model.Descripcion) ? DBNull.Value : model.Descripcion.Trim());
                    updateCommand.Parameters.AddWithValue("@Precio", model.Precio);
                    updateCommand.Parameters.AddWithValue("@Stock", model.Stock);
                    updateCommand.Parameters.AddWithValue("@Activo", model.Activo);
                    updateCommand.Parameters.AddWithValue("@ImagenUrl", string.IsNullOrWhiteSpace(model.ImagenUrl) ? DBNull.Value : model.ImagenUrl.Trim());
                    updateCommand.Parameters.AddWithValue("@EsDestacado", model.EsDestacado);
                    await updateCommand.ExecuteNonQueryAsync();

                    await using (var syncCategoria = new SqlCommand("UPDATE dbo.Productos SET CategoriaId = (SELECT TOP 1 CategoriaId FROM dbo.Categorias WHERE Nombre = @Categoria) WHERE ProductoId = @ProductoId;", connection, (SqlTransaction)transaction))
                    {
                        syncCategoria.Parameters.AddWithValue("@Categoria", model.Categoria.Trim());
                        syncCategoria.Parameters.AddWithValue("@ProductoId", model.ProductoId);
                        await syncCategoria.ExecuteNonQueryAsync();
                    }
                }

                if (previousStock != model.Stock)
                {
                    var cantidad = Math.Abs(model.Stock - previousStock);
                    var tipo = model.Stock > previousStock ? "AjusteEntrada" : "AjusteSalida";
                    await CreateMovementInternalAsync(connection, (SqlTransaction)transaction, model.ProductoId, tipo, cantidad, previousStock, model.Stock, "Ajuste realizado desde ediciÃ‡Ã¼n de producto.", usuarioId, usuarioNombre);
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateProductImageAsync(int productoId, string imageUrl)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("UPDATE dbo.Productos SET ImagenUrl = @ImagenUrl WHERE ProductoId = @ProductoId;", connection);
            command.Parameters.AddWithValue("@ProductoId", productoId);
            command.Parameters.AddWithValue("@ImagenUrl", string.IsNullOrWhiteSpace(imageUrl) ? DBNull.Value : imageUrl.Trim());
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task ToggleProductStatusAsync(int productoId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Admin_ToggleProductStatus", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ProductoId", productoId);
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<InventoryMovementViewModel>> GetInventoryMovementsAsync()
        {
            var movements = new List<InventoryMovementViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Admin_GetInventoryMovements", connection);
            command.CommandType = CommandType.StoredProcedure;
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                movements.Add(new InventoryMovementViewModel
                {
                    MovimientoId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    ProductoId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    Producto = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    TipoMovimiento = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Cantidad = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    StockAnterior = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    StockNuevo = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    Motivo = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Usuario = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    FechaMovimiento = reader.IsDBNull(9) ? DateTime.MinValue : reader.GetDateTime(9)
                });
            }
            return movements;
        }

        public async Task RegisterInventoryMovementAsync(InventoryMovementFormViewModel model, int usuarioId, string usuarioNombre)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                string productoNombre;
                int stockAnterior;
                await using (var selectCommand = new SqlCommand("dbo.sp_Admin_GetActiveProductForMovement", connection, (SqlTransaction)transaction))
                {
                    selectCommand.CommandType = CommandType.StoredProcedure;
                    selectCommand.Parameters.AddWithValue("@ProductoId", model.ProductoId);
                    await using var reader = await selectCommand.ExecuteReaderAsync();
                    if (!await reader.ReadAsync()) throw new InvalidOperationException("El producto seleccionado no existe o estÃ‡Â­ inactivo.");
                    productoNombre = reader.IsDBNull(0) ? "Producto" : reader.GetString(0);
                    stockAnterior = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                }

                int nuevoStock = model.TipoMovimiento switch
                {
                    "Entrada" => stockAnterior + model.Cantidad,
                    "Salida" => stockAnterior - model.Cantidad,
                    "Ajuste" => model.Cantidad,
                    _ => throw new InvalidOperationException("Tipo de movimiento no vÃ‡Â­lido.")
                };

                if (nuevoStock < 0) throw new InvalidOperationException("El movimiento deja el stock en negativo.");
                await UpdateProductStockInternalAsync(connection, (SqlTransaction)transaction, model.ProductoId, nuevoStock);
                await CreateMovementInternalAsync(connection, (SqlTransaction)transaction, model.ProductoId, model.TipoMovimiento, model.Cantidad, stockAnterior, nuevoStock, model.Motivo, usuarioId, usuarioNombre, productoNombre);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<OrderAdminListItemViewModel>> GetOrdersAsync(string? estado)
        {
            var pedidos = new List<OrderAdminListItemViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Admin_GetOrders", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@Estado", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(estado) ? DBNull.Value : estado.Trim();
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                pedidos.Add(new OrderAdminListItemViewModel
                {
                    PedidoId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    Cliente = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Correo = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    FechaPedido = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                    Estado = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    TipoEntrega = reader.IsDBNull(5) ? null : reader.GetString(5),
                    DireccionEntrega = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Total = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                    TotalLineas = reader.IsDBNull(8) ? 0 : reader.GetInt32(8)
                });
            }
            return pedidos;
        }

        public async Task<OrderDetailViewModel?> GetOrderDetailAsync(int pedidoId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return await GetOrderDetailInternalAsync(connection, null, pedidoId);
        }

        public async Task UpdateOrderStatusAsync(int pedidoId, string nuevoEstado, int usuarioId, string usuarioNombre)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Admin_UpdateOrderStatus", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@PedidoId", pedidoId);
            command.Parameters.AddWithValue("@NuevoEstado", nuevoEstado);
            command.Parameters.AddWithValue("@UsuarioId", usuarioId);
            command.Parameters.AddWithValue("@UsuarioNombre", usuarioNombre);
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task<SalesReportViewModel> GetSalesReportAsync()
        {
            var model = new SalesReportViewModel();
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using (var command = new SqlCommand("dbo.sp_Admin_SalesSummary", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    model.VentasTotales = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);
                    model.VentasMesActual = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                    model.TotalFacturas = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    model.FacturasMesActual = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                    model.PedidosEntregados = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                }
            }

            await using (var command = new SqlCommand("dbo.sp_Admin_GetInvoices", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    model.Facturas.Add(new InvoiceListItemViewModel
                    {
                        FacturaId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        PedidoId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                        NumeroFactura = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Cliente = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        FechaFactura = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4),
                        Subtotal = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                        Impuesto = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                        Total = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                        Estado = reader.IsDBNull(8) ? string.Empty : reader.GetString(8)
                    });
                }
            }

            foreach (var factura in model.Facturas.Take(5))
            {
                model.ProductosMasVendidos.Add(new TopSellingProductViewModel
                {
                    Producto = factura.Cliente,
                    CantidadVendida = 1,
                    MontoVendido = factura.Total
                });
            }

            if (model.Facturas.Any())
            {
                foreach (var group in model.Facturas.GroupBy(f => new { f.FechaFactura.Year, f.FechaFactura.Month }).OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month).Take(6).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month))
                {
                    model.VentasPorMes.Add(new MonthlySalesViewModel
                    {
                        Periodo = new DateTime(group.Key.Year, group.Key.Month, 1).ToString("MM/yyyy"),
                        Total = group.Sum(x => x.Total)
                    });
                }
            }

            return model;
        }

        public async Task<InvoiceDetailViewModel?> GetInvoiceDetailAsync(int facturaId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            InvoiceDetailViewModel? model = null;
            await using (var command = new SqlCommand("dbo.sp_Admin_GetInvoiceHeader", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@FacturaId", facturaId);
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    model = new InvoiceDetailViewModel
                    {
                        FacturaId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        PedidoId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                        NumeroFactura = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Cliente = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        Correo = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        FechaFactura = reader.IsDBNull(5) ? DateTime.MinValue : reader.GetDateTime(5),
                        Subtotal = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                        Impuesto = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                        Total = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                        Estado = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
                    };
                }
            }

            if (model == null) return null;

            await using (var command = new SqlCommand("dbo.sp_Admin_GetInvoiceLines", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@FacturaId", facturaId);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    model.Detalles.Add(new InvoiceDetailLineViewModel
                    {
                        Producto = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                        Cantidad = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                        PrecioUnitario = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                        Subtotal = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3)
                    });
                }
            }

            return model;
        }


        public async Task<List<AuditLogViewModel>> GetAuditLogsAsync(string? modulo, string? accion, string? buscar)
        {
            var registros = new List<AuditLogViewModel>();

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Admin_GetAuditLogs", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@Modulo", SqlDbType.NVarChar, 80).Value = string.IsNullOrWhiteSpace(modulo) ? DBNull.Value : modulo.Trim();
            command.Parameters.Add("@Accion", SqlDbType.NVarChar, 80).Value = string.IsNullOrWhiteSpace(accion) ? DBNull.Value : accion.Trim();
            command.Parameters.Add("@Buscar", SqlDbType.NVarChar, 200).Value = string.IsNullOrWhiteSpace(buscar) ? DBNull.Value : buscar.Trim();

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                registros.Add(new AuditLogViewModel
                {
                    AuditoriaId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    UsuarioId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    UsuarioNombre = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    UsuarioCorreo = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Rol = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Accion = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Modulo = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Descripcion = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    DireccionIp = reader.IsDBNull(8) ? null : reader.GetString(8),
                    UserAgent = reader.IsDBNull(9) ? null : reader.GetString(9),
                    FechaRegistro = reader.IsDBNull(10) ? DateTime.MinValue : reader.GetDateTime(10)
                });
            }

            return registros;
        }

        public async Task CreateAuditLogAsync(int? usuarioId, string? usuarioNombre, string? usuarioCorreo, string? rol, string accion, string modulo, string descripcion, string? direccionIp, string? userAgent)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await using var command = new SqlCommand("dbo.sp_Admin_CreateAuditLog", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId.HasValue && usuarioId.Value > 0 ? usuarioId.Value : DBNull.Value;
                command.Parameters.Add("@UsuarioNombre", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(usuarioNombre) ? "Usuario no identificado" : usuarioNombre.Trim();
                command.Parameters.Add("@UsuarioCorreo", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(usuarioCorreo) ? "No disponible" : usuarioCorreo.Trim();
                command.Parameters.Add("@Rol", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(rol) ? "No disponible" : rol.Trim();
                command.Parameters.Add("@Accion", SqlDbType.NVarChar, 80).Value = accion.Trim();
                command.Parameters.Add("@Modulo", SqlDbType.NVarChar, 80).Value = modulo.Trim();
                command.Parameters.Add("@Descripcion", SqlDbType.NVarChar, 500).Value = descripcion.Trim();
                command.Parameters.Add("@DireccionIp", SqlDbType.NVarChar, 80).Value = string.IsNullOrWhiteSpace(direccionIp) ? DBNull.Value : direccionIp.Trim();
                command.Parameters.Add("@UserAgent", SqlDbType.NVarChar, 300).Value = string.IsNullOrWhiteSpace(userAgent) ? DBNull.Value : userAgent.Trim();

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
            catch
            {
                // La auditoría no debe bloquear las acciones principales del sistema.
            }
        }

        public async Task<List<string>> GetStoreCategoriesAsync()
        {
            var categorias = new List<string>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Store_GetCategories", connection);
            command.CommandType = CommandType.StoredProcedure;
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                {
                    var categoria = reader.GetString(0).Trim();
                    if (!string.IsNullOrWhiteSpace(categoria)) categorias.Add(categoria);
                }
            }
            return categorias;
        }

        public async Task<List<StoreProductViewModel>> GetStoreProductsAsync(string? categoria, string? buscar)
        {
            var productos = new List<StoreProductViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Store_GetProducts", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@Categoria", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(categoria) ? DBNull.Value : categoria.Trim();
            command.Parameters.Add("@Buscar", SqlDbType.NVarChar, 200).Value = string.IsNullOrWhiteSpace(buscar) ? DBNull.Value : buscar.Trim();
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                productos.Add(new StoreProductViewModel
                {
                    ProductoId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    Nombre = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Categoria = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Descripcion = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Precio = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                    Stock = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    ImagenUrl = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    EsDestacado = reader.FieldCount > 7 && !reader.IsDBNull(7) && reader.GetBoolean(7)
                });
            }
            return productos;
        }

        private async Task<OrderDetailViewModel?> GetOrderDetailInternalAsync(SqlConnection connection, SqlTransaction? transaction, int pedidoId)
        {
            OrderDetailViewModel? model = null;

            await using (var command = new SqlCommand("dbo.sp_Admin_GetOrderHeader", connection, transaction))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@PedidoId", pedidoId);
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    model = new OrderDetailViewModel
                    {
                        PedidoId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        UsuarioId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                        Cliente = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Correo = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        FechaPedido = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4),
                        Estado = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        TipoEntrega = reader.IsDBNull(6) ? null : reader.GetString(6),
                        DireccionEntrega = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Observaciones = reader.IsDBNull(8) ? null : reader.GetString(8),
                        Total = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9)
                    };
                }
            }

            if (model == null) return null;

            await using (var command = new SqlCommand("dbo.sp_Admin_GetOrderDetailLines", connection, transaction))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@PedidoId", pedidoId);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    model.Detalles.Add(new OrderDetailLineViewModel
                    {
                        PedidoDetalleId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        Producto = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        ProductoId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                        Cantidad = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                        PrecioUnitario = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                        Subtotal = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                        StockActual = reader.IsDBNull(6) ? 0 : reader.GetInt32(6)
                    });
                }
            }

            return model;
        }


        public async Task ToggleFeaturedAsync(int productoId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("UPDATE dbo.Productos SET EsDestacado = CASE WHEN ISNULL(EsDestacado,0)=1 THEN 0 ELSE 1 END WHERE ProductoId = @ProductoId;", connection);
            command.Parameters.AddWithValue("@ProductoId", productoId);
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<StoreProductViewModel>> GetFeaturedProductsAsync(int maxItems)
        {
            var productos = new List<StoreProductViewModel>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("SELECT TOP (@Top) ProductoId, Nombre, Categoria, ISNULL(Descripcion,''), Precio, Stock, ISNULL(ImagenUrl,'') AS ImagenUrl, ISNULL(EsDestacado,0) FROM dbo.Productos WHERE Activo = 1 AND ISNULL(EsDestacado,0)=1 ORDER BY ProductoId DESC;", connection);
            command.Parameters.AddWithValue("@Top", maxItems);
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                productos.Add(new StoreProductViewModel
                {
                    ProductoId = reader.GetInt32(0),
                    Nombre = reader.GetString(1),
                    Categoria = reader.GetString(2),
                    Descripcion = reader.GetString(3),
                    Precio = reader.GetDecimal(4),
                    Stock = reader.GetInt32(5),
                    ImagenUrl = reader.GetString(6),
                    EsDestacado = !reader.IsDBNull(7) && reader.GetBoolean(7)
                });
            }
            return productos;
        }
        private static async Task CreateMovementInternalAsync(SqlConnection connection, SqlTransaction transaction, int productoId, string tipoMovimiento, int cantidad, int stockAnterior, int stockNuevo, string? motivo, int usuarioId, string usuarioNombre, string? productoNombre = null)
        {
            if (string.IsNullOrWhiteSpace(productoNombre))
            {
                await using var commandName = new SqlCommand("dbo.sp_Admin_GetProductNameById", connection, transaction);
                commandName.CommandType = CommandType.StoredProcedure;
                commandName.Parameters.AddWithValue("@ProductoId", productoId);
                productoNombre = Convert.ToString(await commandName.ExecuteScalarAsync()) ?? "Producto";
            }
            await using var command = new SqlCommand("dbo.sp_Admin_CreateInventoryMovement", connection, transaction);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ProductoId", productoId);
            command.Parameters.AddWithValue("@ProductoNombre", productoNombre);
            command.Parameters.AddWithValue("@TipoMovimiento", tipoMovimiento);
            command.Parameters.AddWithValue("@Cantidad", cantidad);
            command.Parameters.AddWithValue("@StockAnterior", stockAnterior);
            command.Parameters.AddWithValue("@StockNuevo", stockNuevo);
            command.Parameters.AddWithValue("@Motivo", string.IsNullOrWhiteSpace(motivo) ? DBNull.Value : motivo.Trim());
            command.Parameters.AddWithValue("@UsuarioId", usuarioId);
            command.Parameters.AddWithValue("@UsuarioNombre", usuarioNombre);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task UpdateProductStockInternalAsync(SqlConnection connection, SqlTransaction transaction, int productoId, int nuevoStock)
        {
            await using var command = new SqlCommand("dbo.sp_Admin_UpdateProductStock", connection, transaction);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ProductoId", productoId);
            command.Parameters.AddWithValue("@NuevoStock", nuevoStock);
            await command.ExecuteNonQueryAsync();
        }
    }
}






