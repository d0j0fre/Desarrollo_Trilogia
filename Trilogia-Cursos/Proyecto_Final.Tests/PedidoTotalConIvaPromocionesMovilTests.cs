using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_Final.Models.Store;
using Proyecto_Final.Services;
using Proyecto_FinalAPI.Controllers;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services.Mobile;

namespace Proyecto_Final.Tests;

/// <summary>
/// Observaciones 1 y 3 de las pruebas post instalación (decisiones de Esteban):
///  1. el total del pedido incluye el IVA, igual que la factura (migraciones 0330 y 0333);
///  2. Venta móvil aplica las promociones activas como la Tienda y cotiza antes de confirmar (0331 y 0332).
/// La base de datos se ensayó en LocalDB (ver hallazgos/fin-precios.md); aquí se fijan las reglas que no deben aflojarse.
/// </summary>
public sealed class PedidoTotalConIvaPromocionesMovilTests
{
    // ── IVA: misma regla que la factura ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2970.00, 386.10)]     // ejemplo real: pedido #93, subtotal ₡2 970 → IVA ₡386,10 → total ₡3 356,10
    [InlineData(1000.00, 130.00)]
    [InlineData(0.00, 0.00)]
    [InlineData(18630.00, 2421.90)]
    [InlineData(0.05, 0.01)]          // ROUND de SQL Server: mitad hacia arriba (0,0065 → 0,01)
    public void OrderTax_UsesTheInvoiceRule(double subtotal, double expectedTax) =>
        Assert.Equal((decimal)expectedTax, OrderTax.Calculate((decimal)subtotal));

    [Fact]
    public void Cart_ShowsSubtotalDiscountTaxAndTotalWithTax()
    {
        // Yucas Tostadas ₡1 100 x 3 con el 10 % de promoción: subtotal ₡3 300, descuento ₡330.
        var cart = new CartViewModel
        {
            Items = [new CartItemViewModel { ProductoId = 5, Nombre = "Yucas Tostadas", Precio = 1100m, Cantidad = 3, MontoDescuento = 330m }]
        };

        Assert.Equal(3300m, cart.Subtotal);
        Assert.Equal(330m, cart.DescuentoTotal);
        Assert.Equal(2970m, cart.SubtotalConDescuento);
        Assert.Equal(386.10m, cart.Impuesto);
        Assert.Equal(3356.10m, cart.Total);   // el mismo total de la factura
    }

    [Fact]
    public void Receipt_ShowsTheTaxRowSoTheRowsAddUpToTheTotal()
    {
        const string template = "TOTAL={{TOTAL}}|SUBTOTAL={{SUBTOTAL}}|{{DESCUENTO}}|{{IMPUESTO}}|{{PRODUCTOS}}";
        var html = OrderReceiptHtmlBuilder.Build(template, "Cliente", 93,
            new CheckoutViewModel { MetodoPago = "Efectivo contra entrega", TipoEntrega = "Retiro", DireccionEntrega = "x" },
            [new CartItemViewModel { Nombre = "Yucas Tostadas", Precio = 1100m, Cantidad = 3, MontoDescuento = 330m }],
            3356.10m, 330m, new DateTime(2026, 11, 22, 10, 0, 0));

        Assert.Contains("IVA (13 %)", html);
        Assert.Contains(OrderReceiptHtmlBuilder.FormatCurrency(386.10m), html);
        Assert.Contains($"TOTAL={OrderReceiptHtmlBuilder.FormatCurrency(3356.10m)}", html);

        // Sin IVA (total = subtotal - descuentos) no se agrega la fila.
        var sinIva = OrderReceiptHtmlBuilder.Build(template, "Cliente", 94,
            new CheckoutViewModel { MetodoPago = "Efectivo contra entrega", TipoEntrega = "Retiro", DireccionEntrega = "x" },
            [new CartItemViewModel { Nombre = "Producto", Precio = 10000m, Cantidad = 1, MontoDescuento = 1000m }],
            9000m, 1000m, new DateTime(2026, 11, 22, 10, 0, 0));
        Assert.DoesNotContain("IVA (13 %)", sinIva);
        Assert.Contains("{{IMPUESTO}}", Read("Proyecto_Final", "EmailTemplates", "OrderReceipt.html"), StringComparison.Ordinal);
    }

    // ── Migración 0330: el pedido guarda subtotal, IVA y total con IVA ──────────────────────────

    [Fact]
    public void Migration0330_AddsSubtotalAndTax_AndMigratesExistingOrdersOnlyOnce()
    {
        var sql = Read("database", "migrations", "0330_pedido_total_con_iva.sql");

        Assert.Contains("ALTER TABLE dbo.Pedidos ADD Subtotal DECIMAL(18,2) NULL", sql, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE dbo.Pedidos ADD Impuesto DECIMAL(18,2) NULL", sql, StringComparison.Ordinal);
        // Respaldo del total anterior (rollback exacto) y migración que solo toca filas sin Subtotal: no se aplica dos veces.
        Assert.Contains("CREATE TABLE dbo.PedidosTotalAntesIva", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE p.Subtotal IS NULL", sql, StringComparison.Ordinal);
        // Con factura el pedido toma la factura; sin factura, total anterior x 1,13 con el redondeo de la factura.
        Assert.Contains("COALESCE(f.Total, ISNULL(p.Total, 0) + ROUND(ISNULL(p.Total, 0) * 0.13, 2))", sql, StringComparison.Ordinal);
        // La factura no se modifica: el IVA no se suma dos veces.
        Assert.DoesNotContain("UPDATE dbo.Facturas", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT dbo.Facturas", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0325_facturar_exige_identificacion", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0330_TheTiendaCheckoutSavesTotalWithTax_AndHeaderKeepsPositions()
    {
        var sql = Read("database", "migrations", "0330_pedido_total_con_iva.sql");

        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.sp_Store_CreateOrderWithPromotions", sql, StringComparison.Ordinal);
        Assert.Contains("orders.Total = amounts.Subtotal + ROUND(amounts.Subtotal * 0.13, 2)", sql, StringComparison.Ordinal);
        Assert.Contains("orders.Impuesto = ROUND(amounts.Subtotal * 0.13, 2)", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.sp_Store_GetCheckoutResult", sql, StringComparison.Ordinal);

        // AdminDbService lee sp_Admin_GetOrderHeader por POSICIÓN (Total = 9 … MotivoRechazo = 14): las columnas nuevas van al final.
        var header = sql[sql.IndexOf("CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetOrderHeader", StringComparison.Ordinal)..];
        Assert.True(header.IndexOf("p.MotivoRechazo,", StringComparison.Ordinal) < header.IndexOf("AS Subtotal", StringComparison.Ordinal));
        Assert.True(header.IndexOf("p.Total,", StringComparison.Ordinal) < header.IndexOf("p.MotivoRechazo,", StringComparison.Ordinal));
        var reader = Read("Proyecto_Final", "Services", "AdminDbService.cs");
        Assert.Contains("reader.FieldCount > 15", reader, StringComparison.Ordinal);
        Assert.Contains("reader.FieldCount > 16", reader, StringComparison.Ordinal);
        Assert.Contains("Total = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9)", reader, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0330_pedido_total_con_iva")]
    [InlineData("0331_promociones_calculo_compartido")]
    [InlineData("0332_venta_movil_promociones_iva")]
    [InlineData("0333_movil_detalle_pedido_iva")]
    public void NewMigrations_HaveVerifyRollbackLedgerAndReadmeRow(string id)
    {
        var sql = Read("database", "migrations", id + ".sql");
        Assert.Contains($"MigrationId=N'{id}'", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT dbo.SchemaMigrationHistory", sql, StringComparison.Ordinal);
        Assert.Contains("COMMIT TRANSACTION", sql, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Root(), "database", "migrations", id + ".verify.sql")));
        Assert.True(File.Exists(Path.Combine(Root(), "database", "migrations", id + ".rollback.md")));
        Assert.Contains($"`{id}.sql`", Read("database", "migrations", "README.md"), StringComparison.Ordinal);
        // Solo el rango asignado a esta sesión.
        Assert.InRange(int.Parse(id[..4]), 330, 334);
    }

    // ── Migraciones 0331/0332: Venta móvil aplica las promociones de la Tienda ─────────────────

    [Fact]
    public void Migration0331_SharesTheTiendaPromotionRules()
    {
        var shared = Read("database", "migrations", "0331_promociones_calculo_compartido.sql");
        var tienda = Read("database", "migrations", "0012_inventory_combos_transformations_intelligence.sql");
        Assert.Contains("CREATE OR ALTER FUNCTION dbo.fn_Promociones_Aplicar", shared, StringComparison.Ordinal);
        Assert.Contains("CREATE OR ALTER FUNCTION dbo.fn_Venta_Lineas", shared, StringComparison.Ordinal);

        // Mismas reglas que sp_Store_CreateOrderWithPromotions (0012): vigencia, estado, segmento, un beneficio por producto,
        // descuento redondeado a 2 decimales y regalía por volumen.
        foreach (var rule in new[]
        {
            "promotion.Estado = N'Activa'",
            "CONVERT(DATE, SYSDATETIME()) BETWEEN promotion.FechaInicio AND promotion.FechaFin",
            "promotion.SegmentoCliente IN (N'Todos'",
            "ORDER BY promotion.Prioridad DESC, promotion.PromocionId",
            "candidate.PorcentajeDescuento / 100.0, 2)",
            "(candidate.CantidadComprada / candidate.CantidadMinima) * candidate.CantidadRegalo"
        })
        {
            Assert.Contains(rule, shared, StringComparison.Ordinal);
        }
        Assert.Contains("ORDER BY promotion.Prioridad DESC, promotion.PromocionId", tienda, StringComparison.Ordinal);
        Assert.Contains("candidate.PorcentajeDescuento / 100.0, 2)", tienda, StringComparison.Ordinal);
        // Aditiva: solo funciones, sin tablas ni datos.
        Assert.DoesNotContain("ALTER TABLE", shared, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE dbo.", shared, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO dbo.", shared, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0332_MobileSaleAppliesPromotionsAndSavesTotalWithTax()
    {
        var sql = Read("database", "migrations", "0332_venta_movil_promociones_iva.sql");

        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.sp_Seller_CreateOrder", sql, StringComparison.Ordinal);
        Assert.Contains("FROM dbo.fn_Venta_Lineas(@ClienteUsuarioId, @ItemsJson)", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO dbo.PromocionAplicaciones", sql, StringComparison.Ordinal);
        Assert.Contains("PedidoDetalle (PedidoId, ProductoId, Cantidad, PrecioUnitario, ProductoNombreSnapshot, EsRegalo)", sql, StringComparison.Ordinal);
        // Montos: la misma regla de la factura; total con IVA en el pedido, la factura y el cargo a crédito.
        Assert.Contains("SET @Impuesto     = ROUND(@Subtotal * 0.13, 2);", sql, StringComparison.Ordinal);
        Assert.Contains("SET @Total        = @TotalFactura;", sql, StringComparison.Ordinal);
        Assert.Contains("@Subtotal, @Impuesto, @Total, LEFT(ISNULL(@Observaciones", sql, StringComparison.Ordinal);
        Assert.Contains("@MontoFactura = @TotalFactura", sql, StringComparison.Ordinal);
        // La retención se compara con el total con IVA y el inventario baja por lo comprado más lo regalado.
        Assert.Contains("IF @Total > @UmbralRetencion", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO @Salida", sql, StringComparison.Ordinal);
        // Compatibilidad: scripts existentes capturan el resultado con INSERT … EXEC (4 columnas).
        var finalSelect = sql[sql.LastIndexOf("COMMIT TRANSACTION;\n\n    SELECT", StringComparison.Ordinal)..];
        Assert.Equal(4, Regex.Matches(finalSelect[..finalSelect.IndexOf("END;", StringComparison.Ordinal)], " AS (PedidoId|Estado|FacturaId|NumeroFactura)").Count);
        // POST-qa2-02 (0325) se conserva.
        Assert.Contains("THROW 50718, 'Falta la identificación del cliente para facturar.'", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 50737, 'Falta la identificación del cliente para facturar.'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0332_ApprovingARetainedOrderInvoicesItsLinesAndGifts()
    {
        var sql = Read("database", "migrations", "0332_venta_movil_promociones_iva.sql");
        var approve = sql[sql.IndexOf("CREATE OR ALTER PROCEDURE dbo.sp_Manager_ApproveOrder", StringComparison.Ordinal)..];
        approve = approve[..approve.IndexOf("CREATE OR ALTER PROCEDURE dbo.sp_Seller_CotizarPedido", StringComparison.Ordinal)];

        // Antes agrupaba por producto con MAX(precio) y habría cobrado una regalía del mismo producto que se compra.
        Assert.DoesNotContain("MAX(d.PrecioUnitario)", approve, StringComparison.Ordinal);
        Assert.Contains("SUM(d.PrecioUnitario * d.Cantidad)", approve, StringComparison.Ordinal);
        Assert.Contains("d.PrecioUnitario", approve, StringComparison.Ordinal);
        Assert.Contains("WHERE  d.PedidoId = @PedidoId AND d.PedidoComboId IS NULL", approve, StringComparison.Ordinal);
        Assert.Contains("SET @Impuesto    = ROUND(@Subtotal * 0.13, 2);", approve, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0332_QuoteUsesTheSameFunctionsAsTheOrder()
    {
        var sql = Read("database", "migrations", "0332_venta_movil_promociones_iva.sql");
        var quote = sql[sql.IndexOf("CREATE OR ALTER PROCEDURE dbo.sp_Seller_CotizarPedido", StringComparison.Ordinal)..];

        Assert.Contains("FROM dbo.fn_Venta_Lineas(@ClienteUsuarioId, @ItemsJson)", quote, StringComparison.Ordinal);
        Assert.Contains("ROUND(@Subtotal * 0.13, 2)", quote, StringComparison.Ordinal);
        Assert.Contains("@Subtotal + @Impuesto > @UmbralRetencion", quote, StringComparison.Ordinal);
        // Solo lectura.
        Assert.DoesNotContain("INSERT INTO dbo.", quote, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE dbo.", quote, StringComparison.Ordinal);
    }

    // ── API de la app móvil ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quote_WithoutProducts_NeverReachesTheDatabase()
    {
        var db = new Mock<IOrdersMobileDbService>(MockBehavior.Strict);
        var controller = WithUser(new MobileSalesController(db.Object, Mock.Of<IMobileAuditService>(), NullLogger<MobileSalesController>.Instance));

        var result = await controller.Quote(new SaleQuoteRequest { ClienteId = 4, Items = [new() { ProductoId = 2, Cantidad = 0 }] }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Quote_SendsOnlyValidLines_AndReturnsWhatTheDatabaseQuotes()
    {
        var quote = new SaleQuote { Subtotal = 2970m, Impuesto = 386.10m, Total = 3356.10m, Descuento = 330m, SubtotalLista = 3300m };
        SaleQuoteRequest? received = null;
        var db = new Mock<IOrdersMobileDbService>();
        db.Setup(service => service.QuoteSaleAsync(It.IsAny<SaleQuoteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SaleQuoteRequest, CancellationToken>((request, _) => received = request)
            .ReturnsAsync(quote);
        var controller = WithUser(new MobileSalesController(db.Object, Mock.Of<IMobileAuditService>(), NullLogger<MobileSalesController>.Instance));

        var result = await controller.Quote(new SaleQuoteRequest
        {
            ClienteId = 4,
            Items = [new() { ProductoId = 5, Cantidad = 3 }, new() { ProductoId = 0, Cantidad = 2 }, new() { ProductoId = 6, Cantidad = 0 }]
        }, CancellationToken.None);

        Assert.Same(quote, Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(4, received!.ClienteId);
        var line = Assert.Single(received.Items);
        Assert.Equal((5, 3), (line.ProductoId, line.Cantidad));
    }

    [Fact]
    public void MobileApi_QuoteIsGuardedLikeTheOtherSaleEndpoints_AndOrderDetailCarriesTheTax()
    {
        var method = typeof(MobileSalesController).GetMethod(nameof(MobileSalesController.Quote))!;
        Assert.Contains(method.GetCustomAttributes(false), a => a.GetType().Name == "RequirePermissionAttribute");
        Assert.Contains(method.GetCustomAttributes(false), a => a.GetType().Name == "EnableRateLimitingAttribute");
        Assert.Contains(method.GetCustomAttributes(false), a => a is HttpPostAttribute { Template: "quote" });

        var service = Read("Proyecto_FinalAPI", "Services", "Mobile", "OrdersMobileDbService.cs");
        Assert.Contains("dbo.sp_Seller_CotizarPedido", service, StringComparison.Ordinal);
        Assert.Contains("Subtotal = r.Dec(\"Subtotal\")", service, StringComparison.Ordinal);
        Assert.Contains("Impuesto = r.Dec(\"Impuesto\")", service, StringComparison.Ordinal);
        Assert.NotNull(typeof(MobileOrderDetail).GetProperty("Subtotal"));
        Assert.NotNull(typeof(MobileOrderDetail).GetProperty("Impuesto"));
    }

    // ── Pantallas del sitio ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Cart", "Index.cshtml")]
    [InlineData("Cart", "Checkout.cshtml")]
    [InlineData("Cart", "Confirmation.cshtml")]
    [InlineData("OrdersAdmin", "Detail.cshtml")]
    [InlineData("ClientPortal", "Detail.cshtml")]
    [InlineData("ManagerOrders", "Review.cshtml")]
    [InlineData("SellerOrders", "Confirmation.cshtml")]
    [InlineData("SellerOrders", "RetainedConfirmation.cshtml")]
    public void OrderScreens_ShowSubtotalTaxAndTotalWithTax(string folder, string view)
    {
        var text = Read("Proyecto_Final", "Views", folder, view);
        Assert.Contains("OrderTax.Label", text, StringComparison.Ordinal);
        Assert.Contains("Subtotal", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SellerWebScreen_QuotesPromotionsAndTaxBeforeConfirming()
    {
        var view = Read("Proyecto_Final", "Views", "SellerOrders", "Index.cshtml");
        Assert.Contains("data-producto-id=\"@Model.Productos[i].ProductoId\"", view, StringComparison.Ordinal);
        Assert.Contains("@Url.Action(\"Quote\", \"SellerOrders\")", view, StringComparison.Ordinal);
        Assert.Contains("renderQuote", view, StringComparison.Ordinal);
        Assert.Contains("quote.retenido", view, StringComparison.Ordinal);

        var controller = Read("Proyecto_Final", "Controllers", "SellerOrdersController.cs");
        Assert.Contains("public async Task<IActionResult> Quote(", controller, StringComparison.Ordinal);
        Assert.Contains("QuoteSellerOrderAsync", controller, StringComparison.Ordinal);
        Assert.Contains("dbo.sp_Seller_CotizarPedido", Read("Proyecto_Final", "Services", "AdminDbService.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void Checkout_ConfirmationCarriesTheSavedSubtotalAndTax()
    {
        var store = Read("Proyecto_Final", "Services", "StoreDbService.cs");
        Assert.Contains("Subtotal = reader.GetDecimal(reader.GetOrdinal(\"Subtotal\"))", store, StringComparison.Ordinal);
        Assert.Contains("Impuesto = reader.GetDecimal(reader.GetOrdinal(\"Impuesto\"))", store, StringComparison.Ordinal);
        var cart = Read("Proyecto_Final", "Controllers", "CartController.cs");
        Assert.Contains("Subtotal = order.Subtotal", cart, StringComparison.Ordinal);
        Assert.Contains("Impuesto = order.Impuesto", cart, StringComparison.Ordinal);
    }

    [Fact]
    public void FlutterApp_QuotesBeforeConfirming_AndShowsTheTaxBreakdown()
    {
        var api = Read("Proyecto_Movil", "lib", "features", "sales", "data", "sales_api.dart");
        Assert.Contains("api/mobile/v1/sales/quote", api, StringComparison.Ordinal);
        var quote = Read("Proyecto_Movil", "lib", "features", "sales", "data", "sale_quote.dart");
        Assert.Contains("impuesto", quote, StringComparison.Ordinal);
        Assert.Contains("retenido", quote, StringComparison.Ordinal);
        var page = Read("Proyecto_Movil", "lib", "features", "sales", "presentation", "sales_pages.dart");
        Assert.Contains("IVA (${SalePricing.taxPercent} %)", page, StringComparison.Ordinal);
        Assert.Contains("GRATIS", page, StringComparison.Ordinal);
        var orders = Read("Proyecto_Movil", "lib", "features", "management", "presentation", "orders_pages.dart");
        Assert.Contains("IVA (13 %)", orders, StringComparison.Ordinal);
        // La app no puede modificar su archivo de análisis (regla del proyecto).
        Assert.DoesNotContain("exclude:", Read("Proyecto_Movil", "analysis_options.yaml"), StringComparison.Ordinal);
    }

    // ── Auxiliares ──────────────────────────────────────────────────────────────────────────────

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz de la solución.");
    }

    private static T WithUser<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("sub", "7"),
                    new Claim(ClaimTypes.Role, "Vendedor"),
                    new Claim(ClaimTypes.Name, "Usuario de Prueba")
                ], "Bearer"))
            }
        };
        return controller;
    }
}
