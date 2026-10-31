using Proyecto_Final.Controllers;
using Proyecto_Final.Models.Store;

namespace Proyecto_Final.Tests;

/// <summary>Correcciones del bloque Carrito, portal del cliente, devoluciones, clientes y crédito (QA G8).</summary>
public sealed class CarritoClientesCorreccionesTests
{
    private static CartItemViewModel Producto(int id, string nombre, int cantidad) =>
        new() { ItemType = CartItemTypes.Product, ProductoId = id, Nombre = nombre, Cantidad = cantidad };

    private static CartItemViewModel Combo(int id, string nombre, int cantidad) =>
        new() { ItemType = CartItemTypes.Combo, ComboId = id, Nombre = nombre, Cantidad = cantidad };

    [Fact]
    public void Checkout_ReportsProductsWhoseStockDroppedBelowTheRequestedUnits()
    {
        // BUG-12-03: 5 unidades en el carrito y el stock bajó a 2.
        var faltantes = CartController.DescribeStockShortages(
            [Producto(7, "Pilsen 6 Pack", 5), Producto(8, "Red Bull 250ml", 1)],
            [Producto(7, "Pilsen 6 Pack", 2), Producto(8, "Red Bull 250ml", 1)]);

        var linea = Assert.Single(faltantes);
        Assert.Contains("Pilsen 6 Pack", linea, StringComparison.Ordinal);
        Assert.Contains("pidió 5", linea, StringComparison.Ordinal);
        Assert.Contains("solo hay 2", linea, StringComparison.Ordinal);
    }

    [Fact]
    public void Checkout_ReportsLinesThatDisappearedBecauseStockIsZero()
    {
        var faltantes = CartController.DescribeStockShortages(
            [Producto(7, "Pilsen 6 Pack", 5), Combo(3, "Combo Fiesta", 2)],
            []);

        Assert.Equal(2, faltantes.Count);
        Assert.All(faltantes, linea => Assert.Contains("ya no hay unidades", linea, StringComparison.Ordinal));
    }

    [Fact]
    public void Checkout_DoesNotWarnWhenEveryLineIsAvailable()
    {
        // Un producto y un combo con el mismo identificador no se confunden.
        var faltantes = CartController.DescribeStockShortages(
            [Producto(3, "Imperial", 2), Combo(3, "Combo Fiesta", 1)],
            [Producto(3, "Imperial", 2), Combo(3, "Combo Fiesta", 1)]);

        Assert.Empty(faltantes);
    }
}
