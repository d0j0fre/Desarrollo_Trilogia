using Proyecto_Final.Controllers;
using Proyecto_Final.Models.Admin;
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

    private static ReturnListItemViewModel Devolucion(string producto, int cantidad, string cliente, string motivo, DateTime fecha) =>
        new() { ProductoNombre = producto, Cantidad = cantidad, ClienteInfo = cliente, Motivo = motivo, FechaRegistro = fecha, Estado = "EnCuarentena" };

    [Fact]
    public void ReturnsReport_GroupsTheSelectedPeriodByProductClientAndReason()
    {
        // BUG-14-02: reporte del mes por producto, cliente y motivo.
        var reporte = ReturnsReportViewModel.Build(
            [
                Devolucion("Red Bull 250ml", 1, "QA Cliente A", "Dañado", new DateTime(2026, 10, 3, 9, 0, 0)),
                Devolucion("Red Bull 250ml", 2, "", "dañado ", new DateTime(2026, 10, 31, 23, 59, 0)),
                Devolucion("Imperial", 4, "QA Cliente A", "Error de despacho", new DateTime(2026, 10, 15)),
                Devolucion("Imperial", 9, "Fuera de rango", "Dañado", new DateTime(2026, 9, 30, 23, 0, 0))
            ],
            new DateTime(2026, 10, 1),
            new DateTime(2026, 10, 31));

        Assert.Equal(3, reporte.Devoluciones.Count);
        Assert.Equal(7, reporte.TotalUnidades);
        Assert.Equal(("Imperial", 1, 4), (reporte.PorProducto[0].Clave, reporte.PorProducto[0].Devoluciones, reporte.PorProducto[0].Unidades));
        Assert.Equal(("Red Bull 250ml", 2, 3), (reporte.PorProducto[1].Clave, reporte.PorProducto[1].Devoluciones, reporte.PorProducto[1].Unidades));
        Assert.Contains(reporte.PorCliente, g => g.Clave == ReturnsReportViewModel.SinCliente && g.Unidades == 2);
        Assert.Contains(reporte.PorMotivo, g => g.Clave.Equals("Dañado", StringComparison.OrdinalIgnoreCase) && g.Devoluciones == 2);
    }

    [Fact]
    public void ReturnsReport_IsReachableFromReturnsAndQuarantineScreens()
    {
        var views = Path.Combine(FindProjectRoot(), "Views", "Returns");
        Assert.Contains("asp-action=\"Report\"", File.ReadAllText(Path.Combine(views, "Index.cshtml")), StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Report\"", File.ReadAllText(Path.Combine(views, "Quarantine.cshtml")), StringComparison.Ordinal);
        Assert.Contains("asp-action=\"ExportCsv\"", File.ReadAllText(Path.Combine(views, "Report.cshtml")), StringComparison.Ordinal);
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la solución."), "Proyecto_Final");
    }
}
