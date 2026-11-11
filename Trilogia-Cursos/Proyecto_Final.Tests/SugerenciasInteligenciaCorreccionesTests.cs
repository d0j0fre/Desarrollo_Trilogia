using System.ComponentModel.DataAnnotations;
using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Tests;

/// <summary>
/// BUG-24-01 a BUG-24-07 y G1-13/G1-14: Sugerencias de compra, Inteligencia de inventario
/// (tendencia estacional) y estados de las órdenes de compra.
/// </summary>
public sealed class SugerenciasInteligenciaCorreccionesTests
{
    private static string Read(params string[] parts) => MobileCorreccionesTests.Read(parts);

    private static List<ValidationResult> Validate(InventoryIntelligenceFilterViewModel filter)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(filter, new ValidationContext(filter), results, validateAllProperties: true);
        return results;
    }

    // ---------- BUG-24-02 / G1-13: un solo cálculo, los mismos parámetros ----------

    [Fact]
    public void SugerenciasDeCompraYTendenciaUsanLosMismosParametrosPorDefecto()
    {
        var filter = new InventoryIntelligenceFilterViewModel();
        var order = new PurchaseOrderFormViewModel();

        Assert.Equal(filter.RecentMonths, order.MesesPromedio);
        Assert.Equal(filter.CoverageMonths, order.MesesCobertura);
        Assert.Equal(InventoryIntelligenceFilterViewModel.DefaultRecentMonths, filter.RecentMonths);
        Assert.Equal(InventoryIntelligenceFilterViewModel.DefaultCoverageMonths, filter.CoverageMonths);
    }

    [Fact]
    public void LaPantallaDeSugerenciasNoFijaUnaCoberturaPropia()
    {
        var controller = Read("Proyecto_Final", "Controllers", "PurchaseOrdersController.cs");

        // Antes pedía (3, 1) y Inteligencia usa (3, 2): el mismo producto daba cantidades distintas.
        Assert.DoesNotContain("GetPurchaseSuggestionsAsync(3, 1", controller, StringComparison.Ordinal);
        Assert.Contains("InventoryIntelligenceFilterViewModel.DefaultCoverageMonths", controller, StringComparison.Ordinal);
        Assert.Contains("includeAll: true", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void LaMigracion0290AgregaIncluirTodosSinCambiarElCalculo()
    {
        var sql = Read("database", "migrations", "0290_sugerencias_compra_promedio_real.sql");
        var origin = Read("database", "migrations", "0012_inventory_combos_transformations_intelligence.sql");

        Assert.Contains("@IncluirTodos BIT = 0", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE @IncluirTodos = 1 OR CantidadSugerida > 0", sql, StringComparison.Ordinal);
        // La fórmula de demanda es la misma que la de la 0012 (no se escribió un segundo cálculo).
        const string formula = "CEILING(ISNULL(sales.Units, 0) / CONVERT(DECIMAL(18,4), @MesesRecientes) * @MesesCobertura)";
        Assert.Contains(formula, sql, StringComparison.Ordinal);
        Assert.Contains(formula, origin, StringComparison.Ordinal);
    }

    // ---------- BUG-24-01: sin historial, cantidad manual ----------

    [Fact]
    public void ElProductoSinHistorialSeMarcaDatosInsuficientesYPideCantidadManual()
    {
        var intelligence = Read("Proyecto_Final", "Views", "InventoryIntelligence", "Index.cshtml");
        var suggestions = Read("Proyecto_Final", "Views", "PurchaseOrders", "Create.cshtml");
        var controller = Read("Proyecto_Final", "Controllers", "PurchaseOrdersController.cs");

        Assert.Contains("Datos insuficientes", intelligence, StringComparison.Ordinal);
        Assert.DoesNotContain("Sin ventas suficientes", intelligence, StringComparison.Ordinal);
        Assert.Contains("Ingresar cantidad manualmente", intelligence, StringComparison.Ordinal);
        Assert.Contains("ingrese la cantidad manualmente", suggestions, StringComparison.Ordinal);
        // La cantidad queda vacía (0) y la fila sin marcar hasta que el usuario la digite.
        Assert.Contains("line.DatosInsuficientes ? 0 : line.CantidadSugerida", controller, StringComparison.Ordinal);
    }

    // ---------- BUG-24-03: alerta de estancamiento ----------

    [Fact]
    public void LaAlertaDeEstancamientoSugierePromocionesYElGerenteVeLaPantalla()
    {
        var view = Read("Proyecto_Final", "Views", "InventoryIntelligence", "Index.cshtml");
        var promotions = Read("Proyecto_Final", "Controllers", "PromotionsController.cs");
        var migration = Read("database", "migrations", "0291_gerente_inteligencia_inventario.sql");

        Assert.Contains("Alerta gerencial", view, StringComparison.Ordinal);
        Assert.Contains("Incluir en promoción", view, StringComparison.Ordinal);
        Assert.Contains("asp-route-productoId", view, StringComparison.Ordinal);
        Assert.Contains("Create(int? productoId = null)", promotions, StringComparison.Ordinal);
        Assert.Contains("p.Nombre=N'Gerente'", migration, StringComparison.Ordinal);
        Assert.Contains("INVENTARIO_INTELIGENCIA_VER", migration, StringComparison.Ordinal);
    }

    // ---------- BUG-24-04 / 24-05: categoría y definición de venta ----------

    [Fact]
    public void LaTendenciaSumaFacturasNoAnuladasYAceptaCategoria()
    {
        var invoices = Read("database", "migrations", "0292_tendencia_estacional_facturas.sql");
        var category = Read("database", "migrations", "0293_tendencia_estacional_categoria.sql");

        Assert.Contains("invoice.Estado <> N'Anulada'", invoices, StringComparison.Ordinal);
        Assert.Contains("SUM(invoice.Total)", invoices, StringComparison.Ordinal);
        Assert.Contains("FROM dbo.Facturas", invoices, StringComparison.Ordinal);
        Assert.DoesNotContain("dbo.PedidoDetalle", invoices, StringComparison.Ordinal);
        Assert.Contains("@Categoria NVARCHAR(100) = NULL", category, StringComparison.Ordinal);
        Assert.Contains("product.Categoria = @Categoria", category, StringComparison.Ordinal);
    }

    [Fact]
    public void LaTendenciaEnviaLaCategoriaAlProcedimiento()
    {
        var service = Read("Proyecto_Final", "Services", "InventoryIntelligenceDbService.cs");
        var view = Read("Proyecto_Final", "Views", "InventoryIntelligence", "Index.cshtml");

        Assert.Contains("\"@Categoria\"", service, StringComparison.Ordinal);
        Assert.Contains("name=\"Category\"", view, StringComparison.Ordinal);
        Assert.Contains("IVA incluido", view, StringComparison.Ordinal);
    }

    // ---------- BUG-24-06: validación del período ----------

    [Theory]
    [InlineData(2024, 2026, true)]
    [InlineData(2017, 2026, true)]    // 10 años: es el máximo permitido
    [InlineData(2016, 2026, false)]   // 11 años
    [InlineData(2010, 2026, false)]   // 17 años
    [InlineData(2026, 2024, false)]   // inicial mayor que final
    [InlineData(2026, 2026, true)]
    public void ElPeriodoSeValidaConUnSoloMensaje(int start, int end, bool valid)
    {
        var results = Validate(new InventoryIntelligenceFilterViewModel { StartYear = start, EndYear = end });

        if (valid)
        {
            Assert.Empty(results);
        }
        else
        {
            var error = Assert.Single(results);
            Assert.Single(error.MemberNames);
            Assert.DoesNotContain("The ", error.ErrorMessage, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LosMensajesDeValidacionSeVenYEstanEnEspanol()
    {
        var view = Read("Proyecto_Final", "Views", "InventoryIntelligence", "Index.cshtml");
        var inverted = Validate(new InventoryIntelligenceFilterViewModel { StartYear = 2026, EndYear = 2024 });
        var outOfRange = Validate(new InventoryIntelligenceFilterViewModel { StartYear = 1999 });

        Assert.Equal("El año inicial no puede ser mayor que el año final.", Assert.Single(inverted).ErrorMessage);
        Assert.Equal("El año inicial debe estar entre 2000 y 2100.", Assert.Single(outOfRange).ErrorMessage);
        Assert.Contains("asp-validation-summary=\"All\"", view, StringComparison.Ordinal);
        // Con parámetros no válidos no se genera el análisis.
        Assert.Contains("@if (ViewData.ModelState.IsValid)", view, StringComparison.Ordinal);
    }

    // ---------- BUG-24-07: exportación a Excel ----------

    [Fact]
    public void LaTendenciaSeExportaAXlsxConElExportadorDelNavegador()
    {
        var view = Read("Proyecto_Final", "Views", "InventoryIntelligence", "Index.cshtml");
        var script = Read("Proyecto_Final", "wwwroot", "js", "xlsx-export.js");

        Assert.Contains("id=\"exportSeasonal\"", view, StringComparison.Ordinal);
        Assert.Contains("window.XlsxExport.download(", view, StringComparison.Ordinal);
        Assert.Contains("~/js/xlsx-export.js", view, StringComparison.Ordinal);
        Assert.Contains(".xlsx", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ZipArchive", Read("Proyecto_Final", "Controllers", "InventoryIntelligenceController.cs"), StringComparison.Ordinal);
        // Las celdas numéricas se guardan como número (no como texto) y conservan las de texto.
        Assert.Contains("typeof cell.num === 'number'", script, StringComparison.Ordinal);
        Assert.Contains("t=\"inlineStr\"", script, StringComparison.Ordinal);
    }

    // ---------- G1-14: estados legibles ----------

    [Theory]
    [InlineData("Pendiente", "Pendiente")]
    [InlineData("RecibidaParcial", "Recibida parcial")]
    [InlineData("Recibida", "Recibida")]
    [InlineData("CerradaConDiscrepancia", "Cerrada con faltantes")]
    [InlineData("Cancelada", "Cancelada")]
    [InlineData(null, "")]
    public void LosEstadosDeLasOrdenesSeMuestranLegibles(string? stored, string expected) =>
        Assert.Equal(expected, PurchaseOrderStatus.Label(stored));

    [Fact]
    public void LasPantallasDeOrdenesMuestranElEstadoLegible()
    {
        foreach (var path in new[]
                 {
                     new[] { "Proyecto_Final", "Views", "PurchaseOrders", "Index.cshtml" },
                     new[] { "Proyecto_Final", "Views", "PurchaseOrders", "Detail.cshtml" },
                     new[] { "Proyecto_Final", "Views", "PriceHistory", "Index.cshtml" }
                 })
        {
            Assert.Contains("PurchaseOrderStatus.Label(", Read(path), StringComparison.Ordinal);
        }

        // El valor guardado (y el filtro por URL) no cambia.
        Assert.Equal("RecibidaParcial", PurchaseOrderStatus.PartiallyReceived);
        Assert.Equal("CerradaConDiscrepancia", PurchaseOrderStatus.ClosedWithDiscrepancy);
    }

    // ---------- Registro de migraciones ----------

    [Theory]
    [InlineData("0290_sugerencias_compra_promedio_real")]
    [InlineData("0291_gerente_inteligencia_inventario")]
    [InlineData("0292_tendencia_estacional_facturas")]
    [InlineData("0293_tendencia_estacional_categoria")]
    public void CadaMigracionTieneVerificacionReversionYFilaEnElReadme(string id)
    {
        var readme = Read("database", "migrations", "README.md");
        var sql = Read("database", "migrations", id + ".sql");

        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "database", "migrations", id + ".verify.sql")));
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "database", "migrations", id + ".rollback.md")));
        Assert.Contains($"`{id}.sql`", readme, StringComparison.Ordinal);
        Assert.Contains("$(MigrationSha256)", sql, StringComparison.Ordinal);
        Assert.Contains("SchemaMigrationHistory", sql, StringComparison.Ordinal);
        Assert.Contains("ya figura aplicada", sql, StringComparison.Ordinal);
    }
}
