using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Tests;

/// <summary>Defectos de Finanzas y Seguridad encontrados al escribir el manual (G1-07, G1-10, G1-11, G1-12, G1-15, G2-29).</summary>
public sealed class FinanzasCorreccionesTests
{
    private static string Source(params string[] relative)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, "Proyecto_Final", .. relative]));
    }

    private static string Migration(string name)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(root, "database", "migrations", name));
    }

    [Fact]
    public void G107_LaColumnaVersionEscribeLaExpresionRazorEntreParentesis()
    {
        var view = Source("Views", "Documents", "Index.cshtml");
        Assert.Contains("v@(item.Version)", view);
        Assert.DoesNotContain("v@item.Version", view);
    }

    [Fact]
    public void G110_ElBotonFiltrarDeIngresosYEgresosNoFuerzaTextoBlanco()
    {
        var view = Source("Views", "FinancialReportAdmin", "Index.cshtml");
        Assert.Contains("btn-ds-search", view);
        Assert.DoesNotContain("#fff", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void G111_LosIngresosSalenDeLasFacturasNoAnuladas()
    {
        var service = Source("Services", "AdminDbService.cs");
        var start = service.IndexOf("GetFinancialReportAsync", StringComparison.Ordinal);
        var end = service.IndexOf("egresosQuery = ", start, StringComparison.Ordinal);
        var incomeQuery = service[start..end];

        Assert.Contains("FROM dbo.Facturas", incomeQuery);
        Assert.Contains("f.Estado <> N'Anulada'", incomeQuery);
        Assert.DoesNotContain("Confirmado simulado", incomeQuery);
    }

    [Fact]
    public void G111_LaPantallaExplicaDeDondeSaleCadaCifra()
    {
        var view = Source("Views", "FinancialReportAdmin", "Index.cshtml");
        Assert.Contains("facturas no anuladas", view);
        Assert.Contains("Reporte de ventas", view);
        Assert.Contains("Aprobado o Pagado", view);
    }

    [Theory]
    [InlineData("Views/BudgetComparison/Index.cshtml", 4)]
    [InlineData("Views/Expenses/Index.cshtml", 3)]
    [InlineData("Views/Budgets/Index.cshtml", 2)]
    public void G112_LasListasDeFiltroMarcanElValorAplicado(string path, int expectedSelects)
    {
        var view = Source(path.Split('/'));
        var marked = view.Split("selected=\"@(Model.Filter.", StringSplitOptions.None).Length - 1;
        Assert.Equal(expectedSelects, marked);
    }

    [Fact]
    public void G115_UnGastoSinPresupuestoDiceSinPresupuesto()
    {
        var result = new ExpenseCreationResult(1, false, "Excedido", 0m);
        Assert.True(result.WithoutBudget);
        Assert.Equal("Gasto registrado. Nivel presupuestario: Excedido (sin presupuesto).", result.BudgetMessage());
    }

    [Fact]
    public void G115_UnGastoQueSuperaSuPresupuestoConservaElPorcentaje()
    {
        var result = new ExpenseCreationResult(1, false, "Excedido", 125m);
        Assert.False(result.WithoutBudget);
        Assert.Contains("Excedido (125", result.BudgetMessage());
        Assert.DoesNotContain("sin presupuesto", result.BudgetMessage());
    }

    [Fact]
    public void G115_UnGastoNormalSoloConfirmaElRegistro() =>
        Assert.Equal("Gasto registrado.", new ExpenseCreationResult(1, false, "Normal", 10m).BudgetMessage());

    [Theory]
    [InlineData(null, "todos")]
    [InlineData("", "todos")]
    [InlineData("rrhh", "rrhh")]
    [InlineData("PLANILLA", "planilla")]
    [InlineData(" general ", "general")]
    [InlineData("otra-cosa", "todos")]
    public void G229_ElOrigenDeLaAuditoriaSeNormaliza(string? received, string expected) =>
        Assert.Equal(expected, AuditOrigins.Normalize(received));

    [Fact]
    public void G229_LaAuditoriaOfreceLasTresBitacorasYTodas() =>
        Assert.Equal(["todos", "general", "rrhh", "planilla"], AuditOrigins.Options.Select(o => o.Code));

    [Fact]
    public void G229_LasListasDeAuditoriaSeLlenanConValoresReales()
    {
        var view = Source("Views", "Audit", "Index.cshtml");
        Assert.Contains("Model.Modulos", view);
        Assert.Contains("Model.Acciones", view);
        // Ya no hay módulos ni acciones fijos en el HTML.
        Assert.DoesNotContain("<option value=\"Pedidos\"", view);
        Assert.DoesNotContain("<option value=\"Login\"", view);
    }

    [Fact]
    public void G229_LaMigracionUneLasTresBitacorasSoloEnLectura()
    {
        var sql = Migration("0273_auditoria_unificada.sql");
        Assert.Contains("dbo.AuditoriaSistema", sql);
        Assert.Contains("dbo.RRHHAuditoria", sql);
        Assert.Contains("dbo.PlanillaAuditoria", sql);
        Assert.DoesNotContain("INSERT dbo.RRHHAuditoria", sql);
        Assert.DoesNotContain("UPDATE dbo.", sql);
        Assert.DoesNotContain("DELETE dbo.", sql);
    }
}
