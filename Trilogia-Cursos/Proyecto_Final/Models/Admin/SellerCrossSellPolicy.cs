using Proyecto_Final.Models.Store;

namespace Proyecto_Final.Models.Admin
{
    // CU-262 — Venta móvil: de las recomendaciones de venta cruzada (0016) se ofrece
    // un artículo complementario, es decir, de una categoría que el pedido todavía no
    // tiene (con cerveza, hielo o una boca; no otra cerveza), y que el vendedor pueda
    // agregar desde su catálogo.
    public static class SellerCrossSellPolicy
    {
        public static CrossSellSuggestion? Complementario(
            IEnumerable<CrossSellSuggestion> sugerencias,
            IReadOnlySet<string> categoriasDelPedido,
            IReadOnlySet<int> catalogoVendedor)
        {
            return sugerencias.FirstOrDefault(s =>
                catalogoVendedor.Contains(s.ProductoId)
                && s.Stock > 0
                && !categoriasDelPedido.Contains(s.Categoria));
        }

        // Lista "1,2,3" que envía la pantalla; descarta valores inválidos y repetidos.
        public static IReadOnlySet<int> ParseProductos(string? productos, int maximo = 50)
        {
            if (string.IsNullOrWhiteSpace(productos)) return new HashSet<int>();
            return productos
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(valor => int.TryParse(valor, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .Take(maximo)
                .ToHashSet();
        }
    }
}
