using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Models.Store;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>
/// fin-correo: los correos que se repiten cuando se configura el SMTP en Azure (aprobación de pedidos, reclamos,
/// garantías, boletas, alertas documentales y de flota) y los enlaces a la URL pública del sitio.
/// </summary>
public sealed class CorreoFlujosTests : IDisposable
{
    private const string PublicUrl = "https://labodega.example.invalid";
    private readonly string _pickup = Path.Combine(Path.GetTempPath(), "trilogia-correo-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_pickup)) Directory.Delete(_pickup, recursive: true);
    }

    private static IConfiguration Settings(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value)).Build();

    private EmailService PickupMailer() => new(Settings(
        ("ConfiguracionCorreo:Host", "localhost"), ("ConfiguracionCorreo:Puerto", "25"),
        ("ConfiguracionCorreo:Remitente", "no-reply@labodega.test"), ("ConfiguracionCorreo:Contrasenna", "solo-pruebas"),
        ("ConfiguracionCorreo:DirectorioRecogida", _pickup), ("PaySlips:PublicBaseUrl", PublicUrl)));

    /// <summary>Texto completo de un .eml con cuerpo en base64 (cabeceras + cuerpo decodificado).</summary>
    private static string ReadEml(string path)
    {
        var raw = File.ReadAllText(path);
        var split = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var headers = raw[..split];
        var body = raw[(split + 4)..];
        if (headers.Contains("boundary=", StringComparison.OrdinalIgnoreCase)) return raw; // multiparte: se revisa el texto crudo
        return headers + "\n" + D(headers.Contains("base64", StringComparison.OrdinalIgnoreCase)
            ? Encoding.UTF8.GetString(Convert.FromBase64String(body.Replace("\r\n", string.Empty).Trim()))
            : body);
    }

    // El HTML codifica los acentos como entidades (&#243;): se decodifican para comparar el texto que lee la persona.
    private static string D(string html) => System.Net.WebUtility.HtmlDecode(html);

    private string SingleEml() => ReadEml(Assert.Single(Directory.GetFiles(_pickup, "*.eml")));

    // ── URL pública ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://labodega.azurewebsites.net", "ClientPortal/Detail/7", "https://labodega.azurewebsites.net/ClientPortal/Detail/7")]
    [InlineData("https://labodega.azurewebsites.net/", "/ClientPortal/Detail/7", "https://labodega.azurewebsites.net/ClientPortal/Detail/7")]
    [InlineData("  https://labodega.example.invalid/sitio  ", "Fleet/Alerts", "https://labodega.example.invalid/sitio/Fleet/Alerts")]
    public void PublicSiteUrl_BuildsAbsoluteHttpsLinks(string baseUrl, string path, string expected)
    {
        Assert.Equal(expected, PublicSiteUrl.Build(Settings(("PaySlips:PublicBaseUrl", baseUrl)), path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://labodega.example.invalid")]
    [InlineData("no-es-una-url")]
    public void PublicSiteUrl_WithoutValidHttpsAddress_ReturnsNullInsteadOfALocalhostLink(string? baseUrl)
    {
        Assert.Null(PublicSiteUrl.Build(Settings(("PaySlips:PublicBaseUrl", baseUrl)), "ClientPortal/Detail/7"));
    }

    // ── Aprobación, rechazo y cancelación de pedidos (CU-074-01) ─────────────

    [Theory]
    [InlineData("Pendiente", "Aprobado", true)]
    [InlineData("Retenido", "Liberado", true)]
    [InlineData("Retenido", "Rechazado", true)]
    [InlineData("Pendiente", "Cancelado", true)]
    [InlineData("Aprobado", "Aprobado", false)]   // ya estaba aprobado: sin correo duplicado
    [InlineData("Cancelado", "Cancelado", false)]
    [InlineData("Aprobado", "EnProceso", false)]
    [InlineData("EnProceso", "Entregado", false)]
    public void OrderStatusEmail_IsSentOnlyWhenTheOrderIsApprovedRejectedOrCancelled(string anterior, string nuevo, bool esperado)
    {
        Assert.Equal(esperado, EmailService.ShouldNotifyOrderStatus(anterior, nuevo));
    }

    [Fact]
    public void OrderStatusEmail_Approved_GoesToTheClientWithOrderNumberAndPublicLink()
    {
        PickupMailer().SendOrderStatus("cliente@example.invalid", "Ana Pérez", 77, "Aprobado", null);

        var eml = SingleEml();
        Assert.Contains("To: cliente@example.invalid", eml);
        Assert.Contains("Su pedido #77 fue aprobado", eml);
        Assert.Contains("Ana Pérez", eml);
        Assert.Contains("#77", eml);
        Assert.Contains($"{PublicUrl}/ClientPortal/Detail/77", eml);
        Assert.DoesNotContain("localhost", eml);
    }

    [Fact]
    public void OrderStatusEmail_Cancelled_ShowsTheReasonAndEncodesIt()
    {
        var html = EmailTemplateBuilder.BuildOrderStatusEmail("Ana", 9, "Cancelado", "Sin stock <b>hoy</b>", null);

        Assert.Contains("Su pedido fue cancelado", html);
        Assert.Contains("Motivo", html);
        Assert.Contains("Sin stock &lt;b&gt;hoy&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>hoy</b>", html);
    }

    [Fact]
    public void OrderStatusEmail_WithoutPublicUrl_HasNoButtonInsteadOfABrokenLink()
    {
        var html = D(EmailTemplateBuilder.BuildOrderStatusEmail("Ana", 9, "Aprobado", null, null));

        Assert.DoesNotContain("Ver mi pedido", html);
        Assert.DoesNotContain("href='#'", html);
    }

    [Theory]
    [InlineData("Aprobado", "fue aprobado")]
    [InlineData("Liberado", "fue aprobado")]
    [InlineData("Rechazado", "no pudo ser aprobado")]
    [InlineData("Cancelado", "fue cancelado")]
    public void OrderStatusEmail_SubjectIsInSpanishForEachResult(string estado, string fragmento)
    {
        Assert.Contains(fragmento, EmailService.OrderStatusSubject(estado, 5));
    }

    [Fact]
    public void OrderStatusEmail_ControllersKeepTheDecisionWhenTheMailFails()
    {
        var root = FindRepositoryRoot();
        var orders = File.ReadAllText(Path.Combine(root, "Proyecto_Final", "Controllers", "OrdersAdminController.cs"));
        var manager = File.ReadAllText(Path.Combine(root, "Proyecto_Final", "Controllers", "ManagerOrdersController.cs"));

        foreach (var source in new[] { orders, manager })
        {
            Assert.Contains("SendOrderStatus", source);
            Assert.Contains("WarningMessage", source);
            Assert.Contains("_logger.LogWarning", source);
        }
        // El estado anterior evita el correo duplicado y se lee antes de cambiar el pedido.
        Assert.True(orders.IndexOf("GetOrderDetailAsync(pedidoId)", StringComparison.Ordinal)
                    < orders.IndexOf("UpdateOrderStatusAsync(pedidoId", StringComparison.Ordinal));
        Assert.Contains("ShouldNotifyOrderStatus(antes.Estado, nuevoEstado)", orders);
    }

    // ── Comprobante de compra de la Tienda (CU-074-02, PED-002) ──────────────

    [Fact]
    public void OrderReceipt_LinksToThePublicOrderPageAndNeverToAHashOrLocalhost()
    {
        var html = BuildReceipt(null);
        Assert.DoesNotContain("href=\"#\"", html);
        Assert.DoesNotContain("Ver mi pedido", html);

        html = BuildReceipt($"{PublicUrl}/ClientPortal/Detail/5");
        Assert.Contains($"href=\"{PublicUrl}/ClientPortal/Detail/5\"", html);
        Assert.Contains("Ver mi pedido", html);
    }

    private static string BuildReceipt(string? link)
    {
        var template = "<a href=\"{{LINK_PEDIDO}}\" style=\"x\"> Ver mi pedido </a>|{{TOTAL}}|{{PRODUCTOS}}";
        return OrderReceiptHtmlBuilder.Build(
            template, "Ana", 5, new CheckoutViewModel(),
            [new CartItemViewModel { Nombre = "Producto", Precio = 1000m, Cantidad = 1 }],
            1000m, 0m, new DateTime(2026, 11, 22), link);
    }

    // ── Reclamo, garantía y boleta ───────────────────────────────────────────

    [Fact]
    public void ClaimClosedEmail_ShowsTheResolutionToTheClient()
    {
        var html = D(EmailTemplateBuilder.BuildClaimClosedEmail("Ana", 12, "Producto dañado", "Se repuso el producto.\nSe entrega el martes."));

        Assert.Contains("Su reclamo fue resuelto", html);
        Assert.Contains("#12", html);
        Assert.Contains("Producto dañado", html);
        Assert.Contains("Se repuso el producto.<br />Se entrega el martes.", html);
    }

    [Theory]
    [InlineData("Aprobada", "fue aprobada", "Su garantía fue aprobada")]
    [InlineData("Rechazada", "no fue aprobada", "Su garantía no fue aprobada")]
    public void WarrantyResultEmail_ShowsResultAndResolution(string estado, string frase, string titulo)
    {
        var raw = EmailTemplateBuilder.BuildWarrantyResultEmail("Ana", 3, "Licuadora", estado, "Resolución <i>final</i>");
        var html = D(raw);

        Assert.Contains(frase, html);
        Assert.Contains(titulo, html);
        Assert.Contains("Licuadora", html);
        Assert.Contains("&lt;i&gt;final&lt;/i&gt;", raw);
        Assert.DoesNotContain("<i>final</i>", raw);
    }

    [Fact]
    public async Task PaySlipEmail_CarriesThePdfAttachmentAndTheSecureLink()
    {
        var slip = new PaySlipViewModel
        {
            CalculoId = 1, Empleado = "Luis Mora", Periodo = "2026-11", SalarioBase = 500000m, TotalBruto = 500000m,
            TotalDeducciones = 50000m, TotalNeto = 450000m, Estado = "Pagada"
        };
        var pdf = PaySlipPdf.Build(slip);
        var sender = new SmtpPaySlipEmailSender(PickupMailer());

        await sender.SendLinkAsync("luis@example.invalid", "Luis Mora", new Uri($"{PublicUrl}/MyPaySlips/Details/1"), pdf, CancellationToken.None);

        var raw = File.ReadAllText(Assert.Single(Directory.GetFiles(_pickup, "*.eml")));
        Assert.Contains("To: luis@example.invalid", raw);
        Assert.Contains("application/pdf", raw);
        Assert.Contains("boleta-de-pago.pdf", raw);
        Assert.Contains("%PDF", Encoding.ASCII.GetString(pdf, 0, 5));
        // El cuerpo HTML está en base64 dentro del mensaje multiparte: se decodifica la parte de texto.
        var htmlPart = raw.Split("Content-Type: text/html", 2)[1].Split("\r\n\r\n", 2)[1].Split("\r\n--", 2)[0];
        var html = Encoding.UTF8.GetString(Convert.FromBase64String(htmlPart.Replace("\r\n", string.Empty).Trim()));
        Assert.Contains($"{PublicUrl}/MyPaySlips/Details/1", html);
        Assert.Contains("Luis Mora", html);
    }

    // ── Alertas documentales (CU-202-01) ─────────────────────────────────────

    private static DocumentAlertNotificationCandidate Candidate(int alertId, string recipient = "responsable@example.invalid") => new()
    {
        AlertId = alertId, DocumentId = alertId + 100, ThresholdDays = 30, DocumentTitle = $"Permiso {alertId}",
        ExpirationDate = new DateTime(2026, 12, 22), Recipient = recipient
    };

    private static IConfiguration DocumentMail(bool enabled = true, bool smtp = true) => Settings(
        ("DocumentAlerts:EmailEnabled", enabled ? "true" : "false"),
        ("ConfiguracionCorreo:Host", smtp ? "smtp.invalid" : ""), ("ConfiguracionCorreo:Puerto", "587"),
        ("ConfiguracionCorreo:Remitente", "sender@example.invalid"), ("ConfiguracionCorreo:Contrasenna", "<smtp-app-password>"),
        ("PaySlips:PublicBaseUrl", PublicUrl));

    private static DocumentAlertService DocumentService(Mock<IDocumentManagementDbService> db, Mock<IDocumentAlertEmailSender> email, IConfiguration configuration) =>
        new(db.Object, email.Object, configuration, TimeProvider.System, NullLogger<DocumentAlertService>.Instance);

    [Fact]
    public async Task DocumentAlerts_AlsoMailAlertsCreatedBeforeTheMailWasConfigured_WithoutDuplicates()
    {
        var db = new Mock<IDocumentManagementDbService>();
        db.Setup(x => x.GenerateAlertsAsync(It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>())).ReturnsAsync([Candidate(1)]);
        db.Setup(x => x.GetPendingNotificationsAsync()).ReturnsAsync([Candidate(1), Candidate(2)]);
        var email = new Mock<IDocumentAlertEmailSender>();

        var count = await DocumentService(db, email, DocumentMail()).GenerateAsync(0);

        Assert.Equal(2, count);
        email.Verify(x => x.SendAsync("responsable@example.invalid", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        db.Verify(x => x.RegisterNotificationAsync(It.IsAny<DocumentAlertNotificationCandidate>(), "Enviado", null), Times.Exactly(2));
    }

    [Fact]
    public async Task DocumentAlerts_MailLinksToThePublicAlertsPage()
    {
        var db = new Mock<IDocumentManagementDbService>();
        db.Setup(x => x.GenerateAlertsAsync(It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>())).ReturnsAsync([Candidate(1)]);
        var email = new Mock<IDocumentAlertEmailSender>();

        await DocumentService(db, email, DocumentMail()).GenerateAsync(0);

        email.Verify(x => x.SendAsync(It.IsAny<string>(), "Alerta de vencimiento: Permiso 1",
            It.Is<string>(html => html.Contains($"{PublicUrl}/Documents/Alerts") && html.Contains("actualice el documento")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task DocumentAlerts_SendNothingWhenMailIsDisabledOrIncomplete(bool enabled, bool smtp)
    {
        var db = new Mock<IDocumentManagementDbService>();
        db.Setup(x => x.GenerateAlertsAsync(It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>())).ReturnsAsync([Candidate(1)]);
        var email = new Mock<IDocumentAlertEmailSender>();

        var count = await DocumentService(db, email, DocumentMail(enabled, smtp)).GenerateAsync(0);

        Assert.Equal(1, count);
        email.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DocumentAlerts_AFailedSendIsRecordedAndTheOthersStillGoOut()
    {
        var db = new Mock<IDocumentManagementDbService>();
        db.Setup(x => x.GenerateAlertsAsync(It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>())).ReturnsAsync([Candidate(1, "mala@example.invalid"), Candidate(2)]);
        var email = new Mock<IDocumentAlertEmailSender>();
        email.Setup(x => x.SendAsync("mala@example.invalid", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("smtp"));

        await DocumentService(db, email, DocumentMail()).GenerateAsync(0);

        db.Verify(x => x.RegisterNotificationAsync(It.Is<DocumentAlertNotificationCandidate>(c => c.AlertId == 1), "Fallido", "InvalidOperationException"), Times.Once);
        db.Verify(x => x.RegisterNotificationAsync(It.Is<DocumentAlertNotificationCandidate>(c => c.AlertId == 2), "Enviado", null), Times.Once);
    }

    [Fact]
    public async Task DocumentAlerts_WithoutMigration0335_StillMailsTheNewAlerts()
    {
        var db = new Mock<IDocumentManagementDbService>();
        db.Setup(x => x.GenerateAlertsAsync(It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>())).ReturnsAsync([Candidate(1)]);
        db.Setup(x => x.GetPendingNotificationsAsync()).ThrowsAsync(new InvalidOperationException("Could not find stored procedure"));
        var email = new Mock<IDocumentAlertEmailSender>();

        Assert.Equal(1, await DocumentService(db, email, DocumentMail()).GenerateAsync(0));
        email.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Alertas de flota (CU-154-02) ─────────────────────────────────────────

    [Fact]
    public void FleetAlertEmail_NamesTheVehicleDocumentAndRemainingDays()
    {
        var html = D(EmailTemplateBuilder.BuildFleetDocumentAlertEmail("CRJ-001", "Marchamo", new DateTime(2026, 12, 17), 25, $"{PublicUrl}/Fleet/Alerts"));

        Assert.Contains("CRJ-001", html);
        Assert.Contains("Marchamo", html);
        Assert.Contains("17/12/2026", html);
        Assert.Contains("Vence en 25 día(s)", html);
        Assert.Contains($"{PublicUrl}/Fleet/Alerts", html);
        Assert.Contains("Administración y Logística", html);
    }

    [Theory]
    [InlineData(-3, "Venció hace 3 día(s)")]
    [InlineData(0, "Vence hoy")]
    [InlineData(1, "Vence en 1 día(s)")]
    public void DaysDescription_CoversExpiredToday_AndUpcoming(int dias, string esperado)
    {
        Assert.Equal(esperado, EmailTemplateBuilder.DescribirDias(dias));
    }

    [Fact]
    public async Task FleetNotifier_DoesNothingWhenMailIsDisabledOrIncomplete()
    {
        var disabled = Settings(("ConnectionStrings:DefaultConnection", "Server=x"), ("DocumentAlerts:EmailEnabled", "false"));
        var incomplete = Settings(("ConnectionStrings:DefaultConnection", "Server=x"), ("DocumentAlerts:EmailEnabled", "true"), ("ConfiguracionCorreo:Host", "smtp.invalid"));

        var off = await new FleetAlertNotifier(new FleetDbService(disabled), PickupMailer(), disabled, NullLogger<FleetAlertNotifier>.Instance).NotifyAsync(null);
        var partial = await new FleetAlertNotifier(new FleetDbService(incomplete), PickupMailer(), incomplete, NullLogger<FleetAlertNotifier>.Instance).NotifyAsync([30]);

        Assert.False(off.Enabled);
        Assert.True(partial.Enabled);
        Assert.False(partial.SmtpConfigured);
        Assert.Contains("incompleta", partial.Describe());
        Assert.False(Directory.Exists(_pickup) && Directory.GetFiles(_pickup).Length > 0);
    }

    [Fact]
    public void FleetNotificationResult_ExplainsEachOutcomeInPlainSpanish()
    {
        Assert.Contains("desactivado", FleetAlertNotificationResult.Disabled.Describe());
        Assert.Contains("No hay avisos pendientes", new FleetAlertNotificationResult(true, true, 0, 0, 0, 0).Describe());
        Assert.Contains("no hay usuarios de Administración o Logística", new FleetAlertNotificationResult(true, true, 2, 0, 0, 2).Describe());
        Assert.Contains("No fue posible enviar", new FleetAlertNotificationResult(true, true, 2, 0, 2, 0).Describe());
        Assert.Contains("2 documento(s)", new FleetAlertNotificationResult(true, true, 2, 2, 0, 0).Describe());
        Assert.Contains("1 no se pudo enviar", new FleetAlertNotificationResult(true, true, 2, 1, 1, 0).Describe());
    }

    // ── Texto, codificación y configuración ──────────────────────────────────

    [Fact]
    public void EmailTemplates_AreWrittenInProperSpanish()
    {
        var html = D(string.Join("\n",
            EmailTemplateBuilder.BuildInvoiceEmail("Ana", "FAC-1", 4, new DateTime(2026, 11, 22), 1000m),
            EmailTemplateBuilder.BuildPasswordResetEmail("Ana", $"{PublicUrl}/Account/ResetPassword?token=x"),
            EmailTemplateBuilder.BuildContactConfirmationEmail("Ana", "Consulta", "Hola"),
            EmailTemplateBuilder.BuildConsultationResolvedEmail("Ana", "Consulta", "Cerrada")));

        foreach (var wrong in new[] { "contrasenna", "Recuperacion", "Numero de", "Confirmacion", "facturacion", "boton", "seguira", "gestion comercial", "adjuntamos" })
        {
            Assert.DoesNotContain(wrong, html);
        }
        foreach (var right in new[] { "contraseña", "Recuperación", "Número de comprobante", "Confirmación de facturación", "botón", "seguirá", "gestión comercial" })
        {
            Assert.Contains(right, html);
        }
    }

    [Fact]
    public void ApiEmailTemplate_UsesTheSameSpanishAndALinkTheCallerProvides()
    {
        var root = FindRepositoryRoot();
        var api = File.ReadAllText(Path.Combine(root, "Proyecto_FinalAPI", "Services", "EmailTemplateBuilder.cs"));
        foreach (var wrong in new[] { "contrasenna", "Notificacion", "gestion comercial", "automaticamente", "Recuperacion" })
        {
            Assert.DoesNotContain(wrong, api);
        }
    }

    [Fact]
    public void EmailService_SendsUtf8SubjectWithTheAccentsAndANamedSender()
    {
        PickupMailer().SendEmail("cliente@example.invalid", "Confirmación del pedido #5 – ñandú", "<p>Hola</p>");

        var raw = File.ReadAllText(Assert.Single(Directory.GetFiles(_pickup, "*.eml")));
        Assert.Contains("From: \"Licorera La Bodega\" <no-reply@labodega.test>", raw);
        Assert.Contains("charset=utf-8", raw, StringComparison.OrdinalIgnoreCase);
        var subject = raw.Split("Subject: ", 2)[1].Split("\r\n", 2)[0];
        Assert.StartsWith("=?utf-8?", subject, StringComparison.OrdinalIgnoreCase); // codificado, no bytes sueltos
    }

    [Fact]
    public void EmailService_RejectsAnEmptyRecipientBeforeTryingToSend()
    {
        var error = Assert.Throws<InvalidOperationException>(() => PickupMailer().SendEmail("  ", "Asunto", "<p>x</p>"));
        Assert.Contains("destinatario", error.Message);
    }

    [Fact]
    public void ContactForm_NoLongerHardCodesAPersonalMailbox()
    {
        var root = FindRepositoryRoot();
        var home = File.ReadAllText(Path.Combine(root, "Proyecto_Final", "Controllers", "HomeController.cs"));

        Assert.DoesNotContain("@gmail.com", home);
        Assert.Contains("ConfiguracionCorreo:DestinatarioConsultas", home);
    }

    [Fact]
    public void CheckoutReceipt_FallsBackToTheSessionMailWhenTheFormEmailIsEmpty()
    {
        var root = FindRepositoryRoot();
        var cart = File.ReadAllText(Path.Combine(root, "Proyecto_Final", "Controllers", "CartController.cs"));

        Assert.Contains("!string.IsNullOrWhiteSpace(model.CorreoElectronico)", cart);
    }

    [Fact]
    public void Migration0335_HasVerifyRollbackAndReadmeEntry()
    {
        var dir = Path.Combine(FindRepositoryRoot(), "database", "migrations");

        Assert.True(File.Exists(Path.Combine(dir, "0335_correo_alertas_pendientes.sql")));
        Assert.True(File.Exists(Path.Combine(dir, "0335_correo_alertas_pendientes.verify.sql")));
        Assert.True(File.Exists(Path.Combine(dir, "0335_correo_alertas_pendientes.rollback.md")));
        Assert.Contains("0335_correo_alertas_pendientes.sql", File.ReadAllText(Path.Combine(dir, "README.md")));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz de la solución.");
    }
}
