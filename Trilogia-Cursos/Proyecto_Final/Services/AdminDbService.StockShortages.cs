using System.Data;
using Microsoft.Data.SqlClient;

namespace Proyecto_Final.Services
{
    /// <summary>M-10: producto del pedido cuyo stock actual no alcanza para lo que el pedido necesita.</summary>
    public sealed record OrderStockShortage(string Producto, int Necesita, int Hay);

    // M-10: al aprobar un pedido retenido, sp_Manager_ApproveOrder revisa el stock de TODAS las líneas (también
    // las regalías) y, si falta, revierte todo (50735). Aquí solo se averigua cuál producto es el que falta.
    public partial class AdminDbService
    {
        /// <summary>Errores de stock de sp_Manager_ApproveOrder: 50735 (insuficiente) y 50736 (concurrencia).</summary>
        public static readonly int[] ApproveStockErrorNumbers = [50735, 50736];

        public static bool IsApproveStockError(Exception? exception)
        {
            for (var current = exception; current is not null; current = current.InnerException)
                if (current is SqlException sql && Array.IndexOf(ApproveStockErrorNumbers, sql.Number) >= 0)
                    return true;
            return false;
        }

        public async Task<List<OrderStockShortage>> GetOrderStockShortagesAsync(int pedidoId, CancellationToken cancellationToken = default)
        {
            var lista = new List<OrderStockShortage>();
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(@"
                SELECT pr.Nombre, SUM(d.Cantidad) AS Necesita, pr.Stock AS Hay
                FROM dbo.PedidoDetalle d
                INNER JOIN dbo.Productos pr ON pr.ProductoId = d.ProductoId
                WHERE d.PedidoId = @PedidoId
                GROUP BY pr.ProductoId, pr.Nombre, pr.Stock
                HAVING pr.Stock < SUM(d.Cantidad)
                ORDER BY pr.Nombre;", connection);
            command.Parameters.Add("@PedidoId", SqlDbType.Int).Value = pedidoId;
            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lista.Add(new OrderStockShortage(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2)));
            }
            return lista;
        }
    }

    public static class OrderStockShortageMessage
    {
        public const string Generic = "No se pudo aprobar el pedido: no hay stock suficiente de uno o más productos.";

        /// <summary>«No se aprobó el pedido #12: no hay stock suficiente de «Vino» (se necesitan 3, hay 1). No se hizo ningún cambio.»</summary>
        public static string Build(int pedidoId, IReadOnlyList<OrderStockShortage> faltantes)
        {
            if (faltantes.Count == 0) return Generic;
            var detalle = string.Join("; ", faltantes.Select(f =>
                f.Hay <= 0
                    ? $"«{f.Producto}» (se necesitan {f.Necesita} y no hay existencias)"
                    : $"«{f.Producto}» (se necesitan {f.Necesita} y hay {f.Hay})"));
            return $"No se aprobó el pedido #{pedidoId}: no hay stock suficiente de {detalle}. El pedido sigue retenido y no se hizo ningún cambio.";
        }
    }
}
