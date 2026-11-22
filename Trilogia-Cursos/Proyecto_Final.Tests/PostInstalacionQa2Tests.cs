using Proyecto_Final.Controllers;

namespace Proyecto_Final.Tests;

/// <summary>Defectos hallados al repetir en Azure las pruebas del QA02 (sesión qa2).</summary>
public sealed class PostInstalacionQa2Tests
{
    [Theory]
    [InlineData("Create.cshtml")]
    [InlineData("Edit.cshtml")]
    public void ProductForms_ShowTheImageValidationMessage(string view)
    {
        // POST-qa2-01 · CU-053-04: el rechazo de la imagen (formato o tamaño) se escribe en ModelState
        // sobre ImagenArchivo; el resumen de la vista es «ModelOnly», así que el campo necesita su propio mensaje.
        var html = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Proyecto_Final", "Views", "Inventory", view));
        Assert.Contains("asp-validation-for=\"ImagenArchivo\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-11-30", "2026-11-01", true)]
    [InlineData("2026-11-01", "2026-11-30", false)]
    [InlineData("2026-11-15", "2026-11-15", false)]
    public void FinancialReport_DetectsAnInvertedDateRange(string inicio, string fin, bool invalido)
    {
        // POST-qa2-04 · CU-094-04
        Assert.Equal(invalido, FinancialReportAdminController.RangoInvalido(DateTime.Parse(inicio), DateTime.Parse(fin)));
    }

    [Fact]
    public void FinancialReport_AcceptsAnOpenRange()
    {
        Assert.False(FinancialReportAdminController.RangoInvalido(null, DateTime.Today));
        Assert.False(FinancialReportAdminController.RangoInvalido(DateTime.Today, null));
        Assert.False(FinancialReportAdminController.RangoInvalido(null, null));
    }

    [Fact]
    public void FinancialReportView_ShowsTheRangeError()
    {
        var html = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Proyecto_Final", "Views", "FinancialReportAdmin", "Index.cshtml"));
        Assert.Contains("ViewBag.ErrorRango", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(3, 1, "Stock bajo", "Normal")]   // tope fijo de la base (≤5) pero por encima de su mínimo
    [InlineData(3, 5, "Stock bajo", "Bajo")]
    [InlineData(1, 1, "Stock bajo", "Bajo")]
    [InlineData(0, 0, "Agotado", "Agotado")]
    [InlineData(20, 5, "Disponible", "Normal")]
    public void InventoryStatus_FollowsTheConfiguredMinimum(int stock, int minimum, string dbStatus, string expected)
    {
        // POST-qa2-06 · CU-131-05: el aviso de Inventario, el reporte y el tablero usan el mismo criterio.
        var product = new Proyecto_Final.Models.Admin.ProductAdminViewModel { Stock = stock, StockMinimo = minimum, EstadoStock = dbStatus };
        Assert.Equal(expected, Proyecto_Final.Models.Admin.InventoryReportRowViewModel.Classify(product));
        var view = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Proyecto_Final", "Views", "Inventory", "Index.cshtml"));
        Assert.DoesNotContain("p.EstadoStock == \"Stock bajo\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void MerchandiseShortage_IsDetectedOnlyForTheProcedureError()
    {
        // POST-qa2-05 · CU-105-02: el faltante (error 53416) debe dejar una alerta en la Auditoría.
        Assert.False(Proyecto_Final.Controllers.FinanceController.EsFaltanteDeMercaderia(null));
        Assert.False(Proyecto_Final.Controllers.FinanceController.EsFaltanteDeMercaderia(new InvalidOperationException("otro")));
        Assert.False(Proyecto_Final.Controllers.FinanceController.EsFaltanteDeMercaderia(new Exception("x", new InvalidOperationException("y"))));

        var controller = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Proyecto_Final", "Controllers", "FinanceController.cs"));
        Assert.Contains("ALERTA: faltante de mercadería", controller, StringComparison.Ordinal);
        Assert.Contains("Number: 53416", controller, StringComparison.Ordinal);
        var migration = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "database", "migrations", "0286_liquidacion_mercaderia_ruta.sql"));
        Assert.Contains("THROW 53416", migration, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz de la solución.");
    }
}
