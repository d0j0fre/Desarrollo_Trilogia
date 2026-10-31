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

    [Theory]
    [InlineData(52021, "no tiene crédito activo")]
    [InlineData(52022, "bloqueado")]
    [InlineData(52023, "pagos vencidos")]
    [InlineData(52024, "límite de crédito")]
    public void SellerCreditOrder_ExplainsEachCreditRejection(int sqlError, string expected)
    {
        // BUG-06-02 · CU-062-02, CU-062-03, CU-062-06.
        Assert.Contains(expected, SellerOrdersController.CreditRejectionMessage(sqlError), StringComparison.Ordinal);
        Assert.Null(SellerOrdersController.CreditRejectionMessage(50715));
    }

    [Fact]
    public void CreditOrderMigration_ChargesTheOrderAndReusesTheDebtFunction()
    {
        var root = Path.GetDirectoryName(FindProjectRoot())!;
        var migration = File.ReadAllText(Path.Combine(root, "database", "migrations", "0230_cliente_estado_deuda.sql"));
        var service = File.ReadAllText(Path.Combine(root, "Proyecto_Final", "Services", "AdminDbService.cs"));

        Assert.Contains("CREATE OR ALTER FUNCTION dbo.fn_Cliente_EstadoDeuda", migration, StringComparison.Ordinal);
        Assert.Contains("@MetodoPago            NVARCHAR(40)    = NULL", migration, StringComparison.Ordinal);
        Assert.Contains("EXEC dbo.sp_Credito_CargarPedido", migration, StringComparison.Ordinal);
        Assert.Contains("FROM dbo.fn_Cliente_EstadoDeuda(@UsuarioId)", migration, StringComparison.Ordinal);
        Assert.Contains("TR_Pedidos_AnularCargoCredito", migration, StringComparison.Ordinal);

        // El cargo es el total facturado (con IVA): la factura si existe, si no el total que Venta móvil
        // calcula una sola vez para su auto-factura; al facturarse después, el cargo queda igual a la factura.
        Assert.Contains("@MontoFactura = @TotalFactura", migration, StringComparison.Ordinal);
        Assert.Contains("SELECT TOP 1 f.Total FROM dbo.Facturas f WHERE f.PedidoId = @PedidoId", migration, StringComparison.Ordinal);
        Assert.Contains("CREATE OR ALTER TRIGGER dbo.TR_Facturas_SincronizarCargoCredito", migration, StringComparison.Ordinal);
        Assert.Equal(1, migration.Split("SET @Impuesto    = ROUND(@Subtotal * 0.13, 2);").Length - 1);
        Assert.Contains("\"@MetodoPago\"", service, StringComparison.Ordinal);
        Assert.Contains("\"@PlazoCreditoDias\"", service, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "database", "migrations", "0230_cliente_estado_deuda.verify.sql")));
        Assert.True(File.Exists(Path.Combine(root, "database", "migrations", "0230_cliente_estado_deuda.rollback.md")));
    }

    [Fact]
    public void ClientDeactivation_IsBlockedWhileTheClientHasDebt()
    {
        // BUG-06-01 · CU-063-02: los dos caminos de inactivación consultan la deuda.
        var root = Path.GetDirectoryName(FindProjectRoot())!;
        var migration = File.ReadAllText(Path.Combine(root, "database", "migrations", "0231_cliente_inactivar_con_saldo.sql"));

        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.sp_Admin_UpdateClient", migration, StringComparison.Ordinal);
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.sp_Admin_ToggleClientStatus", migration, StringComparison.Ordinal);
        Assert.Equal(2, migration.Split("FROM dbo.fn_Cliente_EstadoDeuda(@UsuarioId) WHERE DeudaActual > 0").Length - 1);

        var sinSaldo = new InvalidOperationException("x", new Exception());
        Assert.False(ClientsController.IsSaldoPendiente(sinSaldo));
    }

    private static ClientAccountStatementViewModel Cuenta(int movimientos = 2) => new()
    {
        UsuarioId = 53,
        NombreCompleto = "QA-06 Cliente prueba (Peña)",
        Correo = "qa@example.test",
        LimiteCredito = 500000m,
        Saldo = 60000m,
        CreditoDisponible = 440000m,
        CantidadFacturas = 1,
        TotalFacturado = 113000m,
        TotalPagado = 40000m,
        PlazoCreditoDias = 30,
        FechaCorte = new DateTime(2026, 10, 30, 10, 0, 0),
        Facturas = [new() { FacturaId = 1, NumeroFactura = "FAC-20261030-0054", PedidoId = 54, FechaFactura = new DateTime(2026, 10, 30), Total = 113000m, Estado = "Generada", MetodoPago = "Crédito" }],
        Movimientos = Enumerable.Range(1, movimientos).Select(i => new ClientAccountMovementViewModel
        {
            CreditoMovimientoId = i,
            FechaMovimiento = new DateTime(2026, 10, 1).AddHours(i),
            TipoMovimiento = i % 2 == 1 ? "Cargo" : "Abono",
            Descripcion = $"Movimiento {i}",
            Cargo = i % 2 == 1 ? 100000m : 0,
            Abono = i % 2 == 1 ? 0 : 40000m,
            SaldoAcumulado = 60000m
        }).ToList()
    };

    private static string PdfText(byte[] pdf) => System.Text.Encoding.Latin1.GetString(pdf);

    [Fact]
    public void StatementPdf_IsAValidPdfWithBalanceInvoicesAndPayments()
    {
        // BUG-12-02 · CU-123-03: el estado de cuenta se descarga como archivo, no se imprime la pantalla.
        var pdf = Proyecto_Final.Services.ClientAccountPdf.BuildStatement(Cuenta(), []);
        var text = PdfText(pdf);

        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", text, StringComparison.Ordinal);
        Assert.Contains("CRC 60,000.00", text, StringComparison.Ordinal);
        Assert.Contains("FAC-20261030-0054", text, StringComparison.Ordinal);
        Assert.Contains("Pago", text, StringComparison.Ordinal);
        Assert.Contains("prueba \\(Peña\\)", text, StringComparison.Ordinal); // acentos en WinAnsi y paréntesis escapados
        AssertXrefPointsToObjects(pdf);
    }

    [Fact]
    public void StatementPdf_AddsPagesForLongHistories()
    {
        var pdf = Proyecto_Final.Services.ClientAccountPdf.BuildStatement(Cuenta(movimientos: 150), []);
        var text = PdfText(pdf);

        Assert.Contains("/Count 3", text, StringComparison.Ordinal);
        Assert.Contains("Página 3 de 3", text, StringComparison.Ordinal);
        AssertXrefPointsToObjects(pdf);
    }

    [Fact]
    public void InvoicePdf_ContainsTheInvoiceLinesAndTotals()
    {
        // BUG-12-02 · CU-124-02.
        var factura = new ClientPortalInvoiceViewModel
        {
            PedidoId = 54,
            NumeroFactura = "FAC-20261030-0054",
            Cliente = "Cliente QA",
            Correo = "qa@example.test",
            FechaFactura = new DateTime(2026, 10, 30),
            Subtotal = 100000m,
            Impuesto = 13000m,
            Total = 113000m,
            Estado = "Generada",
            Lineas = [new() { Producto = "Pilsen 6 Pack", Cantidad = 5, PrecioUnitario = 20000m, Subtotal = 100000m }]
        };

        var pdf = Proyecto_Final.Services.ClientAccountPdf.BuildInvoice(factura);
        var text = PdfText(pdf);

        Assert.Contains("Pilsen 6 Pack", text, StringComparison.Ordinal);
        Assert.Contains("CRC 113,000.00", text, StringComparison.Ordinal);
        Assert.Equal("factura-FAC-20261030-0054.pdf", Proyecto_Final.Services.ClientAccountPdf.InvoiceFileName(factura));
        AssertXrefPointsToObjects(pdf);
    }

    [Fact]
    public void ClientDetailAndPortal_ReadTheSameStatementProcedure()
    {
        // BUG-06-03 / BUG-12-01: una sola consulta para que el saldo coincida en todos lados.
        var root = Path.GetDirectoryName(FindProjectRoot())!;
        var service = File.ReadAllText(Path.Combine(root, "Proyecto_Final", "Services", "ClientAccountDbService.cs"));
        var migration = File.ReadAllText(Path.Combine(root, "database", "migrations", "0232_cliente_estado_cuenta.sql"));
        var clients = File.ReadAllText(Path.Combine(root, "Proyecto_Final", "Controllers", "ClientsController.cs"));
        var portal = File.ReadAllText(Path.Combine(root, "Proyecto_Final", "Controllers", "ClientPortalController.cs"));

        Assert.Contains("dbo.sp_Cliente_EstadoCuenta", service, StringComparison.Ordinal);
        Assert.Contains("CROSS APPLY dbo.fn_Cliente_EstadoDeuda(u.UsuarioId) deuda", migration, StringComparison.Ordinal);
        Assert.Contains("_clientAccount.GetStatementAsync", clients, StringComparison.Ordinal);
        Assert.Contains("_clientAccount.GetStatementAsync", portal, StringComparison.Ordinal);
        Assert.Contains("\"application/pdf\"", portal, StringComparison.Ordinal);
    }

    private static void AssertXrefPointsToObjects(byte[] pdf)
    {
        var text = PdfText(pdf);
        var start = int.Parse(text[(text.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10)..].Split('\n')[0]);
        Assert.StartsWith("xref", text[start..], StringComparison.Ordinal);
        var lines = text[start..].Split('\n');
        var count = int.Parse(lines[1].Split(' ')[1]);
        for (var i = 1; i < count; i++)
        {
            var offset = int.Parse(lines[2 + i][..10]);
            Assert.StartsWith($"{i} 0 obj", text[offset..], StringComparison.Ordinal);
        }
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la solución."), "Proyecto_Final");
    }
}
