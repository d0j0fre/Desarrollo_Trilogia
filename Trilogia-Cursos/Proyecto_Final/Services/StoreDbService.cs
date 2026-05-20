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
                ?? throw new InvalidOperationException("No se encontrÃ³ la cadena de conexiÃ³n DefaultConnection.");
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
                throw new InvalidOperationException("El carrito estÃ¡ vacÃ­o.");

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

            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
    }
}


