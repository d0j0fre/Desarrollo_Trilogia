using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Models.Store;


namespace Proyecto_Final.Services
{
    public class StoreDbService
    {
        private readonly string _connectionString;

        public StoreDbService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        public async Task<StoreProductViewModel?> GetStoreProductByIdAsync(int productoId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Store_GetProductById", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ProductoId", productoId);
            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new StoreProductViewModel
            {
                ProductoId = reader.GetInt32(0),
                Nombre = reader.GetString(1),
                Categoria = reader.GetString(2),
                Descripcion = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Precio = reader.GetDecimal(4),
                Stock = reader.GetInt32(5),
                ImagenUrl = reader.IsDBNull(6) ? "~/img/whisky-premium.webp" : reader.GetString(6)
            };
        }

        public async Task<OrderCreationResult> CreateOrderWithPromotionsAsync(
            int usuarioId,
            CheckoutViewModel checkout,
            IReadOnlyCollection<CartItemViewModel> items)
        {
            if (items.Count == 0) throw new InvalidOperationException("El carrito está vacío.");

            var itemsJson = JsonSerializer.Serialize(items.Select(item => new
            {
                productoId = item.ProductoId,
                cantidad = item.Cantidad
            }));

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Store_CreateOrderWithPromotions", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
            command.Parameters.Add("@TipoEntrega", SqlDbType.NVarChar, 100).Value = checkout.TipoEntrega;
            command.Parameters.Add("@DireccionEntrega", SqlDbType.NVarChar, 500).Value =
                string.IsNullOrWhiteSpace(checkout.DireccionEntrega) ? DBNull.Value : checkout.DireccionEntrega.Trim();
            command.Parameters.Add("@Observaciones", SqlDbType.NVarChar, 500).Value =
                string.IsNullOrWhiteSpace(checkout.Observaciones) ? DBNull.Value : checkout.Observaciones.Trim();
            command.Parameters.Add("@IdentificacionCliente", SqlDbType.NVarChar, 100).Value =
                string.IsNullOrWhiteSpace(checkout.Identificacion) ? DBNull.Value : checkout.Identificacion.Trim();
            command.Parameters.Add("@ItemsJson", SqlDbType.NVarChar, -1).Value = itemsJson;
            command.Parameters.Add("@MetodoPago", SqlDbType.NVarChar, 40).Value = checkout.MetodoPago.Trim();
            command.Parameters.Add("@ReferenciaPago", SqlDbType.NVarChar, 80).Value =
                string.IsNullOrWhiteSpace(checkout.ReferenciaPago) ? DBNull.Value : checkout.ReferenciaPago.Trim();

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException("La base de datos no devolvió el pedido creado.");

            var result = new OrderCreationResult
            {
                PedidoId = reader.GetInt32(0),
                Total = reader.GetDecimal(1)
            };

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    result.Gifts.Add(new CartItemViewModel
                    {
                        ProductoId = reader.GetInt32(0),
                        Nombre = reader.GetString(1),
                        Cantidad = reader.GetInt32(2),
                        Precio = 0m,
                        EsRegalo = true,
                        PromocionNombre = reader.GetString(3)
                    });
                }
            }

            return result;
        }

        // CU-173 — segmento del cliente (para elegir promociones aplicables).
        public async Task<string> GetUserSegmentAsync(int usuarioId)
        {
            if (usuarioId <= 0) return "Minorista";
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Cliente_GetSegmento", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@UsuarioId", usuarioId);
            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            return result is string s && !string.IsNullOrWhiteSpace(s) ? s : "Minorista";
        }

    }
}


