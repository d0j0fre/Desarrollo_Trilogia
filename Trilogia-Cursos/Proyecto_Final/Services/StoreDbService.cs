using System.Data;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Store;

namespace Proyecto_Final.Services;

public sealed class StoreDbService
{
    private readonly string _connectionString;

    public StoreDbService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("No se encontró la configuración de base de datos.");
    }

    public async Task<StoreProductViewModel?> GetStoreProductByIdAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.sp_Store_GetProductById", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@ProductoId", SqlDbType.Int).Value = productId;
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

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
        int userId,
        CheckoutViewModel checkout,
        IReadOnlyCollection<CartItemViewModel> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("El carrito está vacío.");
        }

        if (checkout.OperationToken == Guid.Empty)
        {
            throw new ArgumentException("El intento de checkout no tiene un identificador válido.", nameof(checkout));
        }

        var itemsJson = StoreCheckoutPayloadBuilder.CreateItemsJson(items);

        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.sp_Store_CreateOrderWithPromotions", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@TipoEntrega", SqlDbType.NVarChar, 100).Value = checkout.TipoEntrega;
        command.Parameters.Add("@DireccionEntrega", SqlDbType.NVarChar, 500).Value = DbValue(checkout.DireccionEntrega);
        command.Parameters.Add("@Observaciones", SqlDbType.NVarChar, 500).Value = DbValue(checkout.Observaciones);
        command.Parameters.Add("@IdentificacionCliente", SqlDbType.NVarChar, 100).Value = DbValue(checkout.Identificacion);
        command.Parameters.Add("@ItemsJson", SqlDbType.NVarChar, -1).Value = itemsJson;
        command.Parameters.Add("@MetodoPago", SqlDbType.NVarChar, 40).Value = checkout.MetodoPago.Trim();
        command.Parameters.Add("@ReferenciaPago", SqlDbType.NVarChar, 80).Value = DbValue(checkout.ReferenciaPago);
        command.Parameters.Add("@TokenOperacion", SqlDbType.UniqueIdentifier).Value = checkout.OperationToken;

        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("La base de datos no devolvió el pedido creado.");
        }

        var result = new OrderCreationResult
        {
            PedidoId = reader.GetInt32(reader.GetOrdinal("PedidoId")),
            Total = reader.GetDecimal(reader.GetOrdinal("Total"))
        };

        if (await reader.NextResultAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var productOrdinal = reader.GetOrdinal("ProductoId");
                var comboOrdinal = reader.GetOrdinal("ComboId");
                var imageOrdinal = reader.GetOrdinal("ImagenUrl");
                result.Items.Add(new CartItemViewModel
                {
                    ItemType = reader.GetString(reader.GetOrdinal("TipoItem")),
                    ProductoId = reader.IsDBNull(productOrdinal) ? 0 : reader.GetInt32(productOrdinal),
                    ComboId = reader.IsDBNull(comboOrdinal) ? null : reader.GetInt32(comboOrdinal),
                    Nombre = reader.GetString(reader.GetOrdinal("Nombre")),
                    Categoria = reader.GetString(reader.GetOrdinal("Categoria")),
                    Descripcion = ReadNullableString(reader, "Descripcion"),
                    Precio = reader.GetDecimal(reader.GetOrdinal("PrecioUnitario")),
                    Cantidad = reader.GetInt32(reader.GetOrdinal("Cantidad")),
                    ImagenUrl = reader.IsDBNull(imageOrdinal) ? "~/img/OFER-Combo.webp" : reader.GetString(imageOrdinal),
                    MontoDescuento = reader.GetDecimal(reader.GetOrdinal("MontoDescuento"))
                });
            }
        }

        if (await reader.NextResultAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Gifts.Add(new CartItemViewModel
                {
                    ItemType = CartItemTypes.Product,
                    ProductoId = reader.GetInt32(reader.GetOrdinal("ProductoId")),
                    Nombre = reader.GetString(reader.GetOrdinal("Nombre")),
                    Cantidad = reader.GetInt32(reader.GetOrdinal("Cantidad")),
                    Precio = 0m,
                    EsRegalo = true,
                    PromocionNombre = reader.GetString(reader.GetOrdinal("PromocionNombre"))
                });
            }
        }

        return result;
    }

    public async Task<string> GetUserSegmentAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return "Minorista";
        }

        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.sp_Cliente_GetSegmento", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = userId;
        await connection.OpenAsync(cancellationToken);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is string segment && !string.IsNullOrWhiteSpace(segment) ? segment : "Minorista";
    }

    private static object DbValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static string? ReadNullableString(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
