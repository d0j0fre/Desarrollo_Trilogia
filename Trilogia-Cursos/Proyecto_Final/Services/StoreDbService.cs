using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Store;
using System.Data;
using System.Text.Json;

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

        public async Task<int> CreateOrderAsync(int usuarioId, CheckoutViewModel checkout, IReadOnlyCollection<CartItemViewModel> items)
        {
            if (items.Count == 0)
                throw new InvalidOperationException("El carrito está vacío.");

            var itemsPayload = items.Select(item => new
            {
                productoId = item.ProductoId,
                cantidad = item.Cantidad
            });

            var itemsJson = JsonSerializer.Serialize(itemsPayload);

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Store_CreateOrder", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@UsuarioId", usuarioId);
            command.Parameters.AddWithValue("@TipoEntrega", checkout.TipoEntrega);
            command.Parameters.AddWithValue("@DireccionEntrega", (object?)checkout.DireccionEntrega?.Trim() ?? DBNull.Value);
            command.Parameters.AddWithValue("@Observaciones", (object?)checkout.Observaciones?.Trim() ?? DBNull.Value);
            command.Parameters.AddWithValue("@IdentificacionCliente", (object?)checkout.Identificacion?.Trim() ?? DBNull.Value);
            command.Parameters.AddWithValue("@ItemsJson", itemsJson);
            command.Parameters.Add("@MetodoPago", SqlDbType.NVarChar, 40).Value = checkout.MetodoPago.Trim();
            command.Parameters.Add("@ReferenciaPago", SqlDbType.NVarChar, 80).Value = string.IsNullOrWhiteSpace(checkout.ReferenciaPago)
                ? DBNull.Value
                : checkout.ReferenciaPago.Trim();

            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
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

        // CU-173 — persiste las promociones aplicadas sobre un pedido recién creado.
        public async Task ApplyPromotionsToOrderAsync(int pedidoId, IReadOnlyCollection<AppliedPromotion> aplicaciones, int usuarioId, string usuarioNombre)
        {
            if (aplicaciones is null || aplicaciones.Count == 0) return;

            var payload = aplicaciones.Select(a => new
            {
                a.PromocionId,
                a.ProductoId,
                a.TipoBeneficio,
                a.MontoDescontado,
                a.UnidadesRegalo,
                a.ProductoRegaloId
            });
            var json = JsonSerializer.Serialize(payload);

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("dbo.sp_Promociones_AplicarAPedido", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("@PedidoId", SqlDbType.Int).Value = pedidoId;
            command.Parameters.Add("@AplicacionesJson", SqlDbType.NVarChar, -1).Value = json;
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId > 0 ? usuarioId : DBNull.Value;
            command.Parameters.Add("@Nombre", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(usuarioNombre) ? DBNull.Value : usuarioNombre;
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }
    }
}


