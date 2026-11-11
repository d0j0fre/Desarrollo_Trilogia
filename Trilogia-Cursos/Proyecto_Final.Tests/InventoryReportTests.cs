using Proyecto_Final.Controllers;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Tests;

public sealed class InventoryReportTests
{
    private static ProductAdminViewModel Product(string name, int stock, int minimum, decimal price, bool active = true, string status = "") =>
        new() { ProductoId = name.GetHashCode(), Nombre = name, Categoria = "Licores", Stock = stock, StockMinimo = minimum, Precio = price, Activo = active, EstadoStock = status };

    [Fact]
    public void Build_ClassifiesStockAndTotalsTheValue()
    {
        var report = InventoryReportController.Build(new[]
        {
            Product("Agotado", 0, 5, 1000m),
            Product("Bajo", 3, 5, 2000m),
            Product("Normal", 10, 5, 500m),
        }, null, null, false);

        Assert.Equal(3, report.TotalProductos);
        Assert.Equal(1, report.Agotados);
        Assert.Equal(1, report.StockBajo);
        Assert.Equal(13, report.TotalUnidades);
        Assert.Equal(3 * 2000m + 10 * 500m, report.ValorInventario);
    }

    [Fact]
    public void Build_HidesInactiveProductsUnlessAsked_AndFiltersByStatus()
    {
        var products = new[] { Product("A", 0, 5, 100m), Product("B", 9, 5, 100m, active: false), Product("C", 20, 5, 100m) };

        Assert.Equal(2, InventoryReportController.Build(products, null, null, false).TotalProductos);
        Assert.Equal(3, InventoryReportController.Build(products, null, null, true).TotalProductos);
        var outOfStock = InventoryReportController.Build(products, null, "agotado", true);
        Assert.Equal(["A"], outOfStock.Filas.Select(f => f.Nombre));
        Assert.Equal(InventoryReportViewModel.AllStatuses, InventoryReportController.Build(products, null, "cualquiera", false).Estado);
    }
}
