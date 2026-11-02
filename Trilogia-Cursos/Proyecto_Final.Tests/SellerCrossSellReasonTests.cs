using Proyecto_Final.Models.Admin;
using Proyecto_Final.Models.Store;

namespace Proyecto_Final.Tests;

/// <summary>G3-06: en Venta móvil el motivo de la sugerencia habla del pedido, no del carrito.</summary>
public sealed class SellerCrossSellReasonTests
{
    [Fact]
    public void ElMotivoDeVentaMovilHablaDelPedido()
    {
        var tienda = CrossSellPolicy.Select(
            [new CrossSellCandidate(12, "Hielo 2kg", "Hielo", 900, 30, "", true, 3, 0, false, 5)],
            new HashSet<int> { 1 });

        var venta = SellerCrossSellPolicy.Complementario(tienda, new HashSet<string> { "Cerveza" }, new HashSet<int> { 12 });

        Assert.Equal("Se compró junto con productos del carrito en 3 pedidos.", Assert.Single(tienda).Explanation);
        Assert.Equal("Se compró junto con productos de este pedido en 3 pedidos.", venta!.Explanation);
    }
}
