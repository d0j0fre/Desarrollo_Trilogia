using Proyecto_Final.Services;
using Proyecto_FinalAPI.Services.Mobile;

namespace Proyecto_Final.Tests;

/// <summary>POST-qa2-02 (facturar exige identificación, 0325) y POST-qa2-03 (facturas vencidas, 0326).</summary>
public sealed class PostInstalacionQa2FacturacionTests
{
    // ── POST-qa2-02 · CU-091-03 ─────────────────────────────────────────────────────────────

    [Fact]
    public void MissingIdentification_IsOnlyRecognizedForItsOwnErrors()
    {
        Assert.False(BusinessErrors.IsMissingIdentification(null));
        Assert.False(BusinessErrors.IsMissingIdentification(new InvalidOperationException("otro")));
        Assert.False(BusinessErrors.IsMissingIdentification(new Exception("x", new InvalidOperationException("y"))));
        Assert.Equal([50906, 50718, 50737], BusinessErrors.MissingIdentificationNumbers);
        Assert.Equal("Falta la identificación del cliente para facturar.", BusinessErrors.MissingIdentificationMessage);
    }

    [Fact]
    public void Migration0325_RejectsInvoicingAnOrderWithoutIdentification()
    {
        var sql = Read("database", "migrations", "0325_facturar_exige_identificacion.sql");

        // Los tres procedimientos que crean facturas.
        foreach (var procedure in new[] { "sp_Admin_GenerateInvoiceFromOrder", "sp_Seller_CreateOrder", "sp_Manager_ApproveOrder" })
            Assert.Contains($"CREATE OR ALTER PROCEDURE dbo.{procedure}", sql, StringComparison.Ordinal);
        foreach (var error in new[] { "THROW 50906", "THROW 50718", "THROW 50737" })
            Assert.Contains(error, sql, StringComparison.Ordinal);
        Assert.True(System.Text.RegularExpressions.Regex.Matches(sql, "Falta la identificación del cliente para facturar\\.").Count >= 3);

        // Venta móvil solo exige la identificación cuando factura al registrar (pedido no retenido).
        Assert.Contains("@DescuentaInv = 1 AND NULLIF(LTRIM(RTRIM(@IdentificacionCliente))", sql, StringComparison.Ordinal);
        // Registrar sin facturar no cambia: el pedido retenido se inserta igual, y no se tocan facturas existentes.
        Assert.DoesNotContain("UPDATE dbo.Facturas", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE", sql.Replace("DELETE FROM dbo.SchemaMigrationHistory", string.Empty), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Root(), "database", "migrations", "0325_facturar_exige_identificacion.verify.sql")));
        Assert.True(File.Exists(Path.Combine(Root(), "database", "migrations", "0325_facturar_exige_identificacion.rollback.md")));
    }

    [Fact]
    public void Controllers_ShowTheMissingIdentificationMessage()
    {
        var billing = Read("Proyecto_Final", "Controllers", "BillingController.cs");
        var seller = Read("Proyecto_Final", "Controllers", "SellerOrdersController.cs");
        Assert.Contains("BusinessErrors.IsMissingIdentification", billing, StringComparison.Ordinal);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(seller, "BusinessErrors\\.IsMissingIdentification").Count); // registro y sincronización sin conexión
        Assert.Contains("MissingIdentificationMessage", seller, StringComparison.Ordinal);
    }

    [Fact]
    public void MobileSale_AnswersBadRequestWhenTheIdentificationIsMissing_AndSendsItWhenGiven()
    {
        var controller = Read("Proyecto_FinalAPI", "Controllers", "MobileSalesController.cs");
        Assert.Contains("MissingIdentificationError = 50718", controller, StringComparison.Ordinal);
        Assert.Contains("exception.Number == MissingIdentificationError", controller, StringComparison.Ordinal);
        Assert.Contains("return InvalidRequest(\"Falta la identificación del cliente para facturar.\")", controller, StringComparison.Ordinal);

        var service = Read("Proyecto_FinalAPI", "Services", "Mobile", "OrdersMobileDbService.cs");
        Assert.DoesNotContain("@IdentificacionCliente\", SqlDbType.NVarChar, 100).Value = DBNull.Value", service, StringComparison.Ordinal);

        // La app Flutter manda la identificación escrita por el vendedor.
        var api = Read("Proyecto_Movil", "lib", "features", "sales", "data", "sales_api.dart");
        Assert.Contains("'identificacionCliente'", api, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("1-0234-0567", true)]        // 9 dígitos
    [InlineData("3-101-123456", true)]        // 10 dígitos
    [InlineData("155812345678", true)]        // DIMEX de 12 dígitos
    [InlineData("12345", false)]
    [InlineData("1234567890123", false)]
    public void MobileSale_ValidatesTheIdentificationLength(string? value, bool expected) =>
        Assert.Equal(expected, OrdersMobileDbService.IsValidIdentification(value));

    [Fact]
    public void MobileSale_NormalizesTheIdentificationLikeTheWebsite() =>
        Assert.Equal("102340567", OrdersMobileDbService.NormalizeIdentification(" 1-0234-0567 "));

    // ── POST-qa2-03 · CU-093-04 ─────────────────────────────────────────────────────────────

    [Fact]
    public void Migration0326_ComputesOverdueInvoicesWithoutChangingTheInvoiceStatus()
    {
        var sql = Read("database", "migrations", "0326_facturas_vencidas_avisos.sql");

        Assert.Contains("CREATE OR ALTER FUNCTION dbo.fn_Cliente_FacturasVencidas", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.sp_Facturas_Vencidas", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.sp_FacturasVencidas_GenerarAvisos", sql, StringComparison.Ordinal);
        // Mismo FIFO de 0230 y plazo de crédito del cliente.
        Assert.Contains("PlazoCreditoDias", sql, StringComparison.Ordinal);
        Assert.Contains("ROWS UNBOUNDED PRECEDING", sql, StringComparison.Ordinal);
        // Decisión del gerente: Facturas.Estado no se toca.
        Assert.DoesNotContain("UPDATE dbo.Facturas", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Root(), "database", "migrations", "0326_facturas_vencidas_avisos.verify.sql")));
        Assert.True(File.Exists(Path.Combine(Root(), "database", "migrations", "0326_facturas_vencidas_avisos.rollback.md")));
    }

    [Fact]
    public void Migration0326_AlertsOnceAPerClientAndDayToAdministratorManagerAndCollections()
    {
        var sql = Read("database", "migrations", "0326_facturas_vencidas_avisos.sql");

        foreach (var profile in new[] { "N'Administrador'", "N'Gerente'", "N'Crédito y Cobro'" })
            Assert.Contains(profile, sql, StringComparison.Ordinal);
        Assert.Contains("N'FacturaVencida'", sql, StringComparison.Ordinal);
        // Idempotente: no repite el aviso del mismo cliente al mismo destinatario el mismo día.
        Assert.Contains("NOT EXISTS", sql, StringComparison.Ordinal);
        Assert.Contains("CAST(n.FechaCreacion AS DATE) = @Hoy", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "2026-11-22T00:30:00", 6, true)]       // primera corrida tras iniciar: corre sin esperar la hora
    [InlineData("2026-11-22", "2026-11-22T09:00:00", 6, false)] // ya corrió hoy
    [InlineData("2026-11-21", "2026-11-22T05:59:00", 6, false)] // aún no es la hora diaria
    [InlineData("2026-11-21", "2026-11-22T06:00:00", 6, true)]
    public void OverdueWorker_RunsAtStartupAndThenOncePerDay(string? last, string now, int hour, bool expected)
    {
        DateTime? lastRun = last is null ? null : DateTime.Parse(last);
        Assert.Equal(expected, InvoiceOverdueDailyWorker.ShouldRun(lastRun, DateTime.Parse(now), hour));
    }

    [Fact]
    public void OverdueWorker_IsRegisteredAndTheScreensShowTheBadge()
    {
        Assert.Contains("AddHostedService<InvoiceOverdueDailyWorker>", Read("Proyecto_Final", "Program.cs"), StringComparison.Ordinal);
        Assert.Contains("FacturasVencidas", Read("Proyecto_Final", "Views", "AccountsReceivableAdmin", "Detail.cshtml"), StringComparison.Ordinal);
        Assert.Contains("FacturasVencidasPorCliente", Read("Proyecto_Final", "Views", "AccountsReceivableAdmin", "Index.cshtml"), StringComparison.Ordinal);
        Assert.Contains("facturasVencidas.TryGetValue", Read("Proyecto_Final", "Views", "Clients", "Details.cshtml"), StringComparison.Ordinal);
        Assert.Contains("FacturaVencida", Read("Proyecto_Final", "Views", "Billing", "Detail.cshtml"), StringComparison.Ordinal);
        Assert.Contains("AccountsReceivableAdmin", Read("Proyecto_Final", "Controllers", "NotificationsController.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void OverdueInvoice_DescribesTheDelay()
    {
        var overdue = new Proyecto_Final.Models.Admin.OverdueInvoiceViewModel
        {
            NumeroFactura = "FAC-1", Pendiente = 98000m, Vencimiento = new DateTime(2026, 10, 7), DiasAtraso = 46
        };
        Assert.Contains("46 día(s)", overdue.Descripcion, StringComparison.Ordinal);
        Assert.Contains("07/10/2026", overdue.Descripcion, StringComparison.Ordinal);
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
