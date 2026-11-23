using System.ComponentModel.DataAnnotations;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Tests;

/// <summary>Ronda 2 (qa2): defectos hallados al repetir los casos en Azure.</summary>
public sealed class PostInstalacionQa2Ronda2Tests
{
    // CU-171-04: una promoción sin producto mostraba «The value '' is invalid.» (error de enlace del entero).
    [Fact]
    public void Promotion_WithoutProduct_ShowsTheSpanishRequiredMessage()
    {
        var model = new PromotionFormViewModel { Nombre = "QA-POST", PorcentajeDescuento = 10, ProductoId = null };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, r => r.ErrorMessage == "Seleccione el producto estratégico.");
    }

    [Fact]
    public void Promotion_WithProduct_PassesTheProductValidation()
    {
        var model = new PromotionFormViewModel { Nombre = "QA-POST", PorcentajeDescuento = 10, ProductoId = 5 };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(PromotionFormViewModel.ProductoId)));
    }
}

/// <summary>POST-qa2-09 (ronda 3): los errores de enlace de MVC salen en español.</summary>
public sealed class PostInstalacionQa2Ronda3Tests
{
    [Fact]
    public void ModelBindingMessages_AreInSpanish()
    {
        var options = new Microsoft.AspNetCore.Mvc.MvcOptions();
        Proyecto_Final.Services.SpanishModelBindingMessages.Apply(options);
        var provider = options.ModelBindingMessageProvider;

        Assert.Equal("Debe completar o seleccionar «Producto».", provider.AttemptedValueIsInvalidAccessor("", "Producto"));
        Assert.Equal("El valor «abc» no es válido para «Cantidad».", provider.AttemptedValueIsInvalidAccessor("abc", "Cantidad"));
        Assert.Equal("Debe completar o seleccionar este campo.", provider.ValueIsInvalidAccessor(""));
        Assert.Equal("«Cantidad» debe ser un número.", provider.ValueMustBeANumberAccessor("Cantidad"));
        Assert.DoesNotContain("The value", provider.AttemptedValueIsInvalidAccessor("", "Producto"), StringComparison.Ordinal);
    }

    // POST-qa2-10 · CU-131-05: el tablero y Inventario cuentan el stock bajo con la misma regla.
    [Fact]
    public void Migration0355_DashboardCountsLowStockWithTheProductMinimum()
    {
        var sql = Read("database", "migrations", "0355_tablero_stock_minimo.sql");

        foreach (var procedure in new[] { "sp_Reportes_DashboardKpis", "sp_Asistente_Metricas", "sp_Reportes_DashboardDetalle" })
            Assert.Contains(procedure, sql, StringComparison.Ordinal);
        Assert.Contains("(Stock > 0 AND Stock <= StockMinimo)", sql, StringComparison.Ordinal);
        Assert.Contains("Stock <= 0", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Root(), "database", "migrations", "0355_tablero_stock_minimo.verify.sql")));
        Assert.True(File.Exists(Path.Combine(Root(), "database", "migrations", "0355_tablero_stock_minimo.rollback.md")));
    }

    // POST-qa2-11 · CU-093-04 (prueba de horas): los dias de atraso se cuentan con el dia de Costa Rica, no con el UTC del servidor.
    [Fact]
    public void Migration0356_OverdueDaysUseTheCostaRicaDay()
    {
        var sql = Read("database", "migrations", "0356_dias_atraso_reloj_cr.sql");

        Assert.Contains("fn_Cliente_EstadoDeuda", sql, StringComparison.Ordinal);
        Assert.Contains("fn_Cliente_FacturasVencidas", sql, StringComparison.Ordinal);
        Assert.Contains("DATEDIFF(DAY, CAST(dbo.fn_FechaHoraCR(p.Vencimiento) AS DATE), dbo.fn_HoyCR())", sql, StringComparison.Ordinal);
        Assert.Contains("DATEDIFF(DAY, CAST(dbo.fn_FechaHoraCR(v.VencimientoMasAntiguo) AS DATE), dbo.fn_HoyCR())", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Root(), "database", "migrations", "0356_dias_atraso_reloj_cr.verify.sql")));
        Assert.True(File.Exists(Path.Combine(Root(), "database", "migrations", "0356_dias_atraso_reloj_cr.rollback.md")));
    }

    [Fact]
    public void InventoryAlert_OnlyCountsActiveProducts()
    {
        var view = Read("Proyecto_Final", "Views", "Inventory", "Index.cshtml");
        Assert.Contains("p.Activo && (p.Stock <= 0 || p.Stock <= p.StockMinimo)", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersTheSpanishBindingMessages()
    {
        Assert.Contains("SpanishModelBindingMessages.Apply", Read("Proyecto_Final", "Program.cs"), StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz de la solución.");
    }
}
