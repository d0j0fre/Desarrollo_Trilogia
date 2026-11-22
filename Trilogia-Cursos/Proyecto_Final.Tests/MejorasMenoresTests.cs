using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_Final.Controllers;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Models.Store;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>fin-mejoras: mejoras menores M-01 a M-10 salidas de las pruebas en Azure.</summary>
public sealed class MejorasMenoresTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz de la solución.");
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));

    private static int Count(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    // ---------------------------------------------------------------- M-01
    [Fact]
    public void M01_InventoryAlert_OnlyCountsActiveProducts()
    {
        var view = Read("Proyecto_Final", "Views", "Inventory", "Index.cshtml");
        Assert.Contains("Model.Where(p => p.Activo && (p.Stock <= 0 || p.Stock <= p.StockMinimo))", view, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- M-02
    [Fact]
    public void M02_PaymentLargerThanBalance_ExplainsTheBalance()
    {
        var ex = new InvalidOperationException("x", SqlErrors.Create(52014, "El monto no puede superar la deuda actual del cliente."));
        var message = AccountsReceivableAdminController.MensajeMovimientoRechazado(ex, "Abono", 15000m);

        Assert.StartsWith("El abono no puede superar el saldo pendiente del cliente (₡", message, StringComparison.Ordinal);
        Assert.Contains("15", message, StringComparison.Ordinal);
        Assert.DoesNotContain("No fue posible completar", message, StringComparison.Ordinal);
        Assert.True(AccountsReceivableAdminController.EsRechazoPorSaldo(ex));
    }

    [Fact]
    public void M02_WithoutKnownBalance_StillExplainsThePaymentRule()
    {
        var ex = new InvalidOperationException("x", SqlErrors.Create(52014, "..."));
        Assert.Equal("El abono no puede superar el saldo pendiente del cliente.",
            AccountsReceivableAdminController.MensajeMovimientoRechazado(ex, "Abono", null));
        Assert.Equal("El monto no puede superar el saldo pendiente del cliente.",
            AccountsReceivableAdminController.MensajeMovimientoRechazado(ex, "AjusteNegativo", null));
    }

    [Fact]
    public void M02_OtherCreditRules_ShowTheProcedureMessage_AndTechnicalErrorsStayGeneric()
    {
        var rule = new InvalidOperationException("x", SqlErrors.Create(52013, "El cargo supera el límite de crédito disponible para este cliente."));
        Assert.Equal("El cargo supera el límite de crédito disponible para este cliente.",
            AccountsReceivableAdminController.MensajeMovimientoRechazado(rule, "Cargo", 100m));

        var technical = new InvalidOperationException("x", SqlErrors.Create(2627, "violación de clave"));
        Assert.Equal("No fue posible completar la operación solicitada.",
            AccountsReceivableAdminController.MensajeMovimientoRechazado(technical, "Abono", 100m));
        Assert.Equal("No fue posible completar la operación solicitada.",
            AccountsReceivableAdminController.MensajeMovimientoRechazado(new Exception("boom"), "Abono", null));
        Assert.False(AccountsReceivableAdminController.EsRechazoPorSaldo(new Exception("boom")));
    }

    [Fact]
    public void M02_RegisterMovement_UsesTheExplainingMessage()
    {
        var controller = Read("Proyecto_Final", "Controllers", "AccountsReceivableAdminController.cs");
        Assert.Contains("TempData[\"ErrorMessage\"] = MensajeMovimientoRechazado(ex, movementForm.TipoMovimiento, deuda);", controller, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- M-03
    private static DocumentAlertNotificationCandidate Candidate(int id, string recipient = "responsable@example.invalid") => new()
    {
        AlertId = id, DocumentId = id + 100, ThresholdDays = 30, DocumentTitle = $"Permiso {id}",
        ExpirationDate = new DateTime(2026, 12, 22), Recipient = recipient
    };

    private static IConfiguration Settings(bool enabled, bool smtp) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["DocumentAlerts:EmailEnabled"] = enabled ? "true" : "false",
        ["ConfiguracionCorreo:Host"] = smtp ? "smtp.invalid" : "", ["ConfiguracionCorreo:Puerto"] = "587",
        ["ConfiguracionCorreo:Remitente"] = "sender@example.invalid", ["ConfiguracionCorreo:Contrasenna"] = "<smtp-app-password>",
        ["PaySlips:PublicBaseUrl"] = "https://labodega.example.invalid"
    }).Build();

    private static DocumentAlertService Service(Mock<IDocumentManagementDbService> db, Mock<IDocumentAlertEmailSender> email, IConfiguration configuration) =>
        new(db.Object, email.Object, configuration, TimeProvider.System, NullLogger<DocumentAlertService>.Instance);

    [Fact]
    public async Task M03_GenerateDetailed_CountsSentMails_AndMessageSaysSo()
    {
        var db = new Mock<IDocumentManagementDbService>();
        db.Setup(x => x.GenerateAlertsAsync(It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>())).ReturnsAsync([Candidate(1), Candidate(2)]);
        db.Setup(x => x.GetPendingNotificationsAsync()).ReturnsAsync([]);
        var email = new Mock<IDocumentAlertEmailSender>();

        var result = await Service(db, email, Settings(true, true)).GenerateDetailedAsync(0);

        Assert.Equal(new DocumentAlertRunResult(2, 2, 0, false, false), result);
        Assert.Equal("Proceso completado: 2 alerta(s) nueva(s) o pendiente(s) de notificación, y se enviaron 2 correos.", result.Describe());
    }

    [Fact]
    public async Task M03_GenerateDetailed_WithMailTurnedOff_SaysNoMailWasSent()
    {
        var db = new Mock<IDocumentManagementDbService>();
        db.Setup(x => x.GenerateAlertsAsync(It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>())).ReturnsAsync([Candidate(1)]);
        var email = new Mock<IDocumentAlertEmailSender>();

        var result = await Service(db, email, Settings(false, true)).GenerateDetailedAsync(0);

        Assert.True(result.EmailDisabled);
        Assert.Contains("no se enviaron correos porque el envío de correos está apagado", result.Describe(), StringComparison.Ordinal);
        email.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task M03_GenerateDetailed_WithIncompleteSmtp_SaysTheConfigurationIsIncomplete()
    {
        var db = new Mock<IDocumentManagementDbService>();
        db.Setup(x => x.GenerateAlertsAsync(It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>())).ReturnsAsync([Candidate(1)]);

        var result = await Service(db, new Mock<IDocumentAlertEmailSender>(), Settings(true, false)).GenerateDetailedAsync(0);

        Assert.True(result.SmtpIncomplete);
        Assert.Contains("configuración del correo está incompleta", result.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task M03_GenerateDetailed_CountsFailedMails_AndKeepsGenerateAsyncCount()
    {
        var db = new Mock<IDocumentManagementDbService>();
        db.Setup(x => x.GenerateAlertsAsync(It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>())).ReturnsAsync([Candidate(1), Candidate(2)]);
        db.Setup(x => x.GetPendingNotificationsAsync()).ReturnsAsync([]);
        var email = new Mock<IDocumentAlertEmailSender>();
        email.Setup(x => x.SendAsync("responsable@example.invalid", It.Is<string>(s => s.Contains("Permiso 2")), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp caído"));

        var service = Service(db, email, Settings(true, true));
        var result = await service.GenerateDetailedAsync(0);

        Assert.Equal(new DocumentAlertRunResult(2, 1, 1, false, false), result);
        Assert.Contains("se envió 1 correo y 1 no se pudieron enviar", result.Describe(), StringComparison.Ordinal);
        Assert.Equal(2, await service.GenerateAsync(0));
        db.Verify(x => x.RegisterNotificationAsync(It.IsAny<DocumentAlertNotificationCandidate>(), "Fallido", It.IsAny<string?>()), Times.AtLeastOnce);
    }

    [Fact]
    public void M03_NoRecipients_SaysNoMailWasNeeded_AndControllerShowsTheDescription()
    {
        Assert.Contains("no se enviaron correos (no había destinatarios pendientes)", new DocumentAlertRunResult(3, 0, 0, false, false).Describe(), StringComparison.Ordinal);
        var controller = Read("Proyecto_Final", "Controllers", "DocumentsController.cs");
        Assert.Contains("TempData[\"SuccessMessage\"] = result.Describe();", controller, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- M-04
    [Fact]
    public void M04_ClientWarrantiesList_ShowsTheWrittenResolution()
    {
        var view = Read("Proyecto_Final", "Views", "ClientPortal", "Warranties.cshtml");
        Assert.Contains("<th>Resolución</th>", view, StringComparison.Ordinal);
        Assert.Contains("@solicitud.Resolucion", view, StringComparison.Ordinal);
        Assert.Contains("solicitud.FechaResolucion", view, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- M-05
    private static ComboDetailViewModel Combo(params (string Name, int Qty, int Stock, bool Active)[] parts) => new()
    {
        ComboId = 3, Nombre = "Combo Fiesta", Activo = true,
        Componentes = parts.Select((p, i) => new ComboDetailLineViewModel
        {
            ProductoId = i + 1, ProductoNombre = p.Name, Cantidad = p.Qty, StockDisponible = p.Stock, ProductoActivo = p.Active
        }).ToList()
    };

    [Fact]
    public void M05_ComboWithAComponentAtZero_NamesTheProduct()
    {
        var message = ComboAvailability.Explain(Combo(("Vino tinto", 1, 8, true), ("Queso", 2, 0, true)));
        Assert.Contains("«Queso» (sin stock)", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Vino tinto", message, StringComparison.Ordinal);
        Assert.StartsWith("El combo «Combo Fiesta» no está disponible: no alcanza el stock de", message, StringComparison.Ordinal);
    }

    [Fact]
    public void M05_ComboWithInsufficientOrInactiveComponents_ExplainsEachOne()
    {
        var short1 = ComboAvailability.Explain(Combo(("Queso", 3, 2, true)));
        Assert.Contains("«Queso» (se necesitan 3 y hay 2)", short1, StringComparison.Ordinal);

        var inactive = ComboAvailability.Explain(Combo(("Vino", 1, 9, false)));
        Assert.Contains("el producto «Vino» ya no está(n) activo(s)", inactive, StringComparison.Ordinal);
    }

    [Fact]
    public void M05_UnknownOrInactiveCombo_KeepsTheGenericMessage()
    {
        Assert.Equal(ComboAvailability.GenericMessage, ComboAvailability.Explain(null));
        Assert.Equal(ComboAvailability.GenericMessage, ComboAvailability.Explain(new ComboDetailViewModel { Activo = false }));
        Assert.Equal(ComboAvailability.GenericMessage, ComboAvailability.Explain(Combo(("Vino", 1, 9, true))));
    }

    [Fact]
    public void M05_ComboFormShowsTheProductsError_OnlyOnce_AndStoreUsesTheExplanation()
    {
        var create = Read("Proyecto_Final", "Views", "Combos", "Create.cshtml");
        Assert.Equal(1, Count(create, "asp-validation-for=\"Productos\""));

        var home = Read("Proyecto_Final", "Controllers", "HomeController.cs");
        var cart = Read("Proyecto_Final", "Controllers", "CartController.cs");
        Assert.Contains("await ExplicarComboNoDisponibleAsync(id, cancellationToken)", home, StringComparison.Ordinal);
        Assert.Contains("await ExplicarComboNoDisponibleAsync(comboId, cancellationToken)", cart, StringComparison.Ordinal);
        Assert.DoesNotContain("return NotFound();", cart[cart.IndexOf("AddCombo(", StringComparison.Ordinal)..cart.IndexOf("Math.Max(cantidad, 1) + (item?.Cantidad", StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- M-06
    [Fact]
    public void M06_ApprovingYourOwnExpenseOrBudget_ExplainsSegregationOfDuties()
    {
        var expense = FinanceApprovalMessages.For(SqlErrors.Create(54306, "El registrador no puede aprobar su propio gasto."), "aprobar el gasto");
        var budget = FinanceApprovalMessages.For(SqlErrors.Create(54208, "El creador no puede aprobar su propio presupuesto."), "aprobar el presupuesto");

        Assert.Contains("usted mismo lo registró", expense, StringComparison.Ordinal);
        Assert.Contains("segregación de funciones", expense, StringComparison.Ordinal);
        Assert.Contains("usted mismo lo creó", budget, StringComparison.Ordinal);
        Assert.Contains("segregación de funciones", budget, StringComparison.Ordinal);
    }

    [Fact]
    public void M06_OtherBusinessRulesShowTheirMessage_AndTechnicalErrorsStayGeneric()
    {
        Assert.Equal("El gasto excede el presupuesto; se requiere autorización especial.",
            FinanceApprovalMessages.For(SqlErrors.Create(54307, "El gasto excede el presupuesto; se requiere autorización especial."), "aprobar el gasto"));
        Assert.Equal("No fue posible aprobar el gasto.", FinanceApprovalMessages.For(SqlErrors.Create(1205, "deadlock"), "aprobar el gasto"));
        Assert.Equal("No fue posible aprobar el gasto.", FinanceApprovalMessages.For(new Exception("boom"), "aprobar el gasto"));
    }

    [Fact]
    public void M06_BothControllersUseTheSharedMessages()
    {
        Assert.Contains("FinanceApprovalMessages.For(exception, operation)", Read("Proyecto_Final", "Controllers", "ExpensesController.cs"), StringComparison.Ordinal);
        Assert.Contains("FinanceApprovalMessages.For(exception, operation)", Read("Proyecto_Final", "Controllers", "BudgetsController.cs"), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- M-07
    [Theory]
    [InlineData("Generada", true, "Pagada")]
    [InlineData("Generada", false, "Generada")]
    [InlineData("Anulada", true, "Anulada")]
    [InlineData("Anulada", false, "Anulada")]
    public void M07_InvoiceShowsPaid_OnlyWhenGeneratedAndBalanceIsZero(string estado, bool pagada, string expected)
    {
        Assert.Equal(expected, InvoiceDisplayStatus.Visible(estado, pagada));
    }

    [Fact]
    public void M07_Balance_IsPaidOnlyWithAChargeAndNothingPending()
    {
        Assert.True(new InvoiceBalance(1, 10, 5000m, 0m).Pagada);
        Assert.False(new InvoiceBalance(1, 10, 5000m, 1m).Pagada);
        Assert.False(new InvoiceBalance(1, 10, 0m, 0m).Pagada);
        Assert.Equal("success", InvoiceDisplayStatus.CssClass("Pagada"));
        Assert.Equal("dark", InvoiceDisplayStatus.CssClass("Anulada"));
    }

    [Fact]
    public void M07_ViewModelsAndViews_ShowTheScreenStatus_WithoutTouchingTheStoredOne()
    {
        var detail = new InvoiceDetailViewModel { Estado = "Generada", Pagada = true };
        Assert.Equal("Pagada", detail.EstadoVisible);
        Assert.Equal("Generada", detail.Estado);
        Assert.Equal("Pagada", new InvoiceListItemViewModel { Estado = "Generada", Pagada = true }.EstadoVisible);
        Assert.Equal("Pagada", new ClientPortalInvoiceViewModel { Estado = "Generada", Pagada = true }.EstadoVisible);

        Assert.Contains("@Model.EstadoVisible", Read("Proyecto_Final", "Views", "Billing", "Detail.cshtml"), StringComparison.Ordinal);
        Assert.Contains("@item.EstadoVisible", Read("Proyecto_Final", "Views", "Billing", "Index.cshtml"), StringComparison.Ordinal);
        Assert.Contains("@Model.EstadoVisible", Read("Proyecto_Final", "Views", "ClientPortal", "Invoice.cshtml"), StringComparison.Ordinal);
    }

    [Fact]
    public void M07_Migration0336_IsAdditive_AndNeverChangesInvoiceStatus()
    {
        var dir = Path.Combine(Root(), "database", "migrations");
        var sql = File.ReadAllText(Path.Combine(dir, "0336_facturas_saldo_pagada.sql"));
        Assert.True(File.Exists(Path.Combine(dir, "0336_facturas_saldo_pagada.verify.sql")));
        Assert.True(File.Exists(Path.Combine(dir, "0336_facturas_saldo_pagada.rollback.md")));
        Assert.Contains("fn_Cliente_FacturasSaldo", sql, StringComparison.Ordinal);
        Assert.Contains("sp_Facturas_Saldo", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE dbo.Facturas", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0336_facturas_saldo_pagada", File.ReadAllText(Path.Combine(dir, "README.md")), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- M-08
    [Fact]
    public void M08_SalaryHistory_GetsTheEffectiveDate_FromTheMatchingVigencia()
    {
        var history = new List<EmployeeSalaryHistoryViewModel>
        {
            new() { HistorialSalarioId = 2, SalarioNuevo = 700000m, Motivo = "Ajuste anual", FechaCambio = new DateTime(2026, 3, 10, 9, 0, 0) },
            new() { HistorialSalarioId = 1, SalarioNuevo = 600000m, Motivo = "Ascenso", FechaCambio = new DateTime(2026, 1, 5, 9, 0, 0) },
        };
        var vigencias = new List<EmployeeSalaryEffectiveViewModel>
        {
            new(new DateTime(1900, 1, 1), 500000m, "Salario inicial"),
            new(new DateTime(2026, 2, 1), 600000m, "Ascenso"),
            new(new DateTime(2026, 4, 1), 700000m, "Ajuste anual"),
        };

        SalaryHistoryDates.Attach(history, vigencias);

        Assert.Equal(new DateTime(2026, 4, 1), history.Single(h => h.HistorialSalarioId == 2).FechaVigencia);
        Assert.Equal(new DateTime(2026, 2, 1), history.Single(h => h.HistorialSalarioId == 1).FechaVigencia);
    }

    [Fact]
    public void M08_SameSalaryTwice_UsesEachVigenciaOnce_AndUnmatchedRowsKeepOnlyTheRegistrationDate()
    {
        var history = new List<EmployeeSalaryHistoryViewModel>
        {
            new() { HistorialSalarioId = 1, SalarioNuevo = 600000m, FechaCambio = new DateTime(2026, 1, 1) },
            new() { HistorialSalarioId = 2, SalarioNuevo = 600000m, FechaCambio = new DateTime(2026, 6, 1) },
            new() { HistorialSalarioId = 3, SalarioNuevo = 999m, FechaCambio = new DateTime(2026, 7, 1) },
        };
        SalaryHistoryDates.Attach(history,
        [
            new EmployeeSalaryEffectiveViewModel(new DateTime(2026, 2, 1), 600000m, null),
            new EmployeeSalaryEffectiveViewModel(new DateTime(2026, 7, 1), 600000m, null),
        ]);

        Assert.Equal(new DateTime(2026, 2, 1), history[0].FechaVigencia);
        Assert.Equal(new DateTime(2026, 7, 1), history[1].FechaVigencia);
        Assert.Null(history[2].FechaVigencia);
    }

    [Fact]
    public void M08_Describe_ShowsTheEffectiveDate_AndTheInitialOneAsSinceHire()
    {
        Assert.Equal("Vigente desde 01/04/2026", SalaryHistoryDates.Describe(new DateTime(2026, 4, 1)));
        Assert.Equal("Desde el alta", SalaryHistoryDates.Describe(new DateTime(1900, 1, 1)));
        Assert.Equal(string.Empty, SalaryHistoryDates.Describe(null));
        Assert.Contains("SalaryHistoryDates.Describe(item.FechaVigencia)", Read("Proyecto_Final", "Views", "Employees", "Details.cshtml"), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- M-09
    [Fact]
    public void M09_Migration0337_NumbersResolvedStopsFirst_ThenThePendingOnes()
    {
        var dir = Path.Combine(Root(), "database", "migrations");
        var sql = File.ReadAllText(Path.Combine(dir, "0337_rutas_recalcular_numeracion.sql"));
        Assert.True(File.Exists(Path.Combine(dir, "0337_rutas_recalcular_numeracion.verify.sql")));
        Assert.True(File.Exists(Path.Combine(dir, "0337_rutas_recalcular_numeracion.rollback.md")));
        Assert.Contains("DECLARE @Seq INT = @Resueltas", sql, StringComparison.Ordinal);
        Assert.Contains("EstadoEntrega NOT IN (N'Pendiente', N'EnRuta')", sql, StringComparison.Ordinal);
        Assert.Contains("Prioridad", sql, StringComparison.Ordinal);                 // conserva la regla de 0281
        Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0337_rutas_recalcular_numeracion", File.ReadAllText(Path.Combine(dir, "README.md")), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- M-10
    [Fact]
    public void M10_ApprovingWithoutStock_NamesTheMissingProducts_AndSaysNothingChanged()
    {
        var message = OrderStockShortageMessage.Build(25,
        [
            new OrderStockShortage("Whisky 12 años", 3, 1),
            new OrderStockShortage("Copa de regalo", 1, 0),
        ]);

        Assert.StartsWith("No se aprobó el pedido #25: no hay stock suficiente de", message, StringComparison.Ordinal);
        Assert.Contains("«Whisky 12 años» (se necesitan 3 y hay 1)", message, StringComparison.Ordinal);
        Assert.Contains("«Copa de regalo» (se necesitan 1 y no hay existencias)", message, StringComparison.Ordinal);
        Assert.Contains("El pedido sigue retenido y no se hizo ningún cambio.", message, StringComparison.Ordinal);
        Assert.Equal(OrderStockShortageMessage.Generic, OrderStockShortageMessage.Build(25, []));
    }

    [Fact]
    public void M10_OnlyTheStockErrorsOfTheApprovalProcedure_TriggerTheExplanation()
    {
        Assert.True(AdminDbService.IsApproveStockError(SqlErrors.Create(50735, "Stock insuficiente para uno o más productos al momento de aprobar.")));
        Assert.True(AdminDbService.IsApproveStockError(new InvalidOperationException("x", SqlErrors.Create(50736, "Error de concurrencia de stock."))));
        Assert.False(AdminDbService.IsApproveStockError(SqlErrors.Create(50732, "Solo se pueden aprobar pedidos en estado Retenido.")));
        Assert.False(AdminDbService.IsApproveStockError(new Exception("boom")));

        var controller = Read("Proyecto_Final", "Controllers", "ManagerOrdersController.cs");
        Assert.Contains("AdminDbService.IsApproveStockError(ex)", controller, StringComparison.Ordinal);
        Assert.Contains("await DescribirFaltanteDeStockAsync(pedidoId)", controller, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- M-11
    [Fact]
    public void M11_DeliveryBoard_ServerRendersAmountsWithTheCostaRicaFormat_LikeTheAutoRefresh()
    {
        var view = Read("Proyecto_Final", "Views", "DeliveryBoard", "Index.cshtml");
        Assert.Equal(2, Count(view, "Proyecto_Final.Services.OrderReceiptHtmlBuilder.FormatCurrency(order.Total)"));
        Assert.DoesNotContain("₡@order.Total.ToString(\"N2\")", view, StringComparison.Ordinal);
        Assert.Contains("toLocaleString('es-CR'", view, StringComparison.Ordinal);
    }

    [Fact]
    public void M11_FormatCurrency_UsesCostaRicaGroupingAndDecimals_WhateverTheServerCulture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            var text = OrderReceiptHtmlBuilder.FormatCurrency(14577m).Replace(' ', ' ').Replace(' ', ' ');
            Assert.Equal("₡14 577,00", text);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    // ---------------------------------------------------------------- M-12
    [Fact]
    public void M12_InvalidCapacityMessage_StatesTheAllowedRange()
    {
        var range = (System.ComponentModel.DataAnnotations.RangeAttribute)typeof(VehicleFormViewModel).GetProperty("Capacidad")!
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false).Single();
        Assert.Equal(0, range.Minimum);
        Assert.Equal(100000, range.Maximum);
        Assert.Equal("La capacidad debe ser un número entero entre 0 y 100000.", range.ErrorMessage);
        Assert.Contains("max=\"100000\"", Read("Proyecto_Final", "Views", "Vehicles", "Create.cshtml"), StringComparison.Ordinal);
        Assert.Contains("max=\"100000\"", Read("Proyecto_Final", "Views", "Vehicles", "Edit.cshtml"), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- M-13
    [Fact]
    public void M13_EditingAnInactivePromotion_WarnsWhenOpening_NotWhenSaving()
    {
        var controller = Read("Proyecto_Final", "Controllers", "PromotionsController.cs");
        var edit = controller[controller.IndexOf("public async Task<IActionResult> Edit(int id)", StringComparison.Ordinal)..];
        edit = edit[..edit.IndexOf("[HttpPost]", StringComparison.Ordinal)];
        Assert.Contains("model.Estado", edit, StringComparison.Ordinal);
        Assert.Contains("EditInactiveMessage(model.Nombre, model.Estado)", edit, StringComparison.Ordinal);
        Assert.Contains("RedirectToAction(nameof(Index))", edit, StringComparison.Ordinal);
        Assert.Equal("Solo se pueden editar promociones activas. La promoción «Verano» está inactiva.", PromotionsController.EditInactiveMessage("Verano", "Inactiva"));
        Assert.Contains("Estado = reader.GetString(12)", Read("Proyecto_Final", "Services", "PromotionsDbService.cs"), StringComparison.Ordinal);
    }
}
