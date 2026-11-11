using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_Final.Middleware;
using Proyecto_Final.Models;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>
/// Liquidación de cobros y de mercadería, flota y Mi perfil (BUG-10-03/04/05/07, BUG-15-01/02,
/// BUG-02-01/02, G2-12/17/18/20/37/38). Las reglas que viven en la base las prueban los
/// <c>.verify.sql</c> de las migraciones 0285 a 0288.
/// </summary>
public sealed class GdLiquidacionFlotaTests
{
    // ── G2-38: el detalle de comprobantes contra el total ──────────────────────────────
    [Fact]
    public void Comprobantes_LaSumaDelDetalleCoincideConElTotal()
    {
        var detalle = new List<CashSettlementVoucher>
        {
            new() { Tipo = "SINPE", Referencia = "A", Monto = 30000m },
            new() { Tipo = "Transferencia", Monto = 20000m },
            new() { Tipo = "Depósito", Monto = 14100m },
            new(), new(),   // filas vacías del formulario
        };

        Assert.Null(CashSettlementRules.ValidateVouchers(detalle, 64100m));
    }

    [Fact]
    public void Comprobantes_UnTotalQueNoCoincideSeRechazaConLosDosMontos()
    {
        var detalle = new List<CashSettlementVoucher> { new() { Tipo = "SINPE", Monto = 10000m } };

        var error = CashSettlementRules.ValidateVouchers(detalle, 13000m);

        Assert.NotNull(error);
        Assert.Contains($"₡{13000m:N2}", error, StringComparison.Ordinal);
        Assert.Contains($"₡{10000m:N2}", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Comprobantes_UnTotalSinDetalleSeRechaza() =>
        Assert.NotNull(CashSettlementRules.ValidateVouchers(new List<CashSettlementVoucher>(), 500m));

    [Fact]
    public void Comprobantes_SinDetalleNiTotalEstaBien() =>
        Assert.Null(CashSettlementRules.ValidateVouchers(new List<CashSettlementVoucher> { new(), new() }, 0m));

    [Theory]
    [InlineData(null, 100)]      // monto sin tipo
    [InlineData("SINPE", 0)]     // tipo sin monto
    [InlineData("SINPE", -5)]    // monto negativo
    public void Comprobantes_CadaFilaNecesitaTipoYMontoPositivo(string? tipo, int monto)
    {
        var detalle = new List<CashSettlementVoucher> { new() { Tipo = tipo, Referencia = "X", Monto = monto } };

        Assert.Equal("Cada comprobante necesita su tipo y un monto mayor que cero.",
            CashSettlementRules.ValidateVouchers(detalle, monto));
    }

    [Fact]
    public void Comprobantes_ElJsonSoloLlevaLasFilasEscritas()
    {
        var json = FinanceDbService.BuildVouchersJson(new List<CashSettlementVoucher>
        {
            new() { Tipo = " SINPE ", Referencia = " 123 ", Monto = 5m }, new(), new() { Monto = 0m },
        });

        Assert.Contains("\"Tipo\":\"SINPE\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Referencia\":\"123\"", json, StringComparison.Ordinal);
        Assert.Equal(1, json.Split("\"Monto\"").Length - 1);
        Assert.Equal(string.Empty, FinanceDbService.BuildVouchersJson(new List<CashSettlementVoucher> { new() }));
    }

    // ── BUG-10-05: el faltante queda asociado al chofer ───────────────────────────────
    [Fact]
    public void Liquidacion_ElMensajeDeUnFaltanteNombraAlChofer()
    {
        var texto = CashSettlementRules.SuccessMessage(new CashSettlementResult(7, "Faltante", -2200m, "Randall Quirós", 2200m));

        Assert.Contains("Faltante", texto, StringComparison.Ordinal);
        Assert.Contains($"₡{2200m:N2}", texto, StringComparison.Ordinal);
        Assert.Contains("a cargo del chofer Randall Quirós", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Liquidacion_UnaCuadradaNoMencionaFaltante() =>
        Assert.DoesNotContain("chofer", CashSettlementRules.SuccessMessage(new CashSettlementResult(8, "Cuadrada", 0m, "Randall", 0m)),
            StringComparison.Ordinal);

    // ── BUG-10-03: recibo PDF ─────────────────────────────────────────────────────────
    [Fact]
    public void Recibo_EsUnPdfConElNumeroLaRutaYLosMontos()
    {
        var recibo = new CashSettlementReceiptViewModel
        {
            LiquidacionCobrosId = 12, RutaId = 3, RutaCodigo = "RUT-20261007232827", Zona = "Heredia", ChoferNombre = "Randall Quirós",
            VehiculoPlaca = "CRJ-001", VehiculoDescripcion = "Panel", MontoEsperadoEfectivo = 0m, MontoEsperadoOtros = 64100m,
            MontoEfectivoRecibido = 0m, MontoComprobantes = 64100m, Diferencia = 0m, Estado = "Cuadrada",
            LiquidadoPorNombre = "Admin Demo", FechaLiquidacion = new DateTime(2026, 11, 11, 10, 5, 0),
            Comprobantes =
            {
                new CashSettlementVoucher { Tipo = "SINPE", Referencia = "A1", Monto = 30000m },
                new CashSettlementVoucher { Tipo = "Transferencia", Monto = 20000m },
                new CashSettlementVoucher { Tipo = "Depósito", Monto = 14100m },
            },
            Pedidos = { new CashSettlementReceiptOrder { PedidoId = 45, Cliente = "Cliente Demo", MetodoPago = "SINPE", Total = 64100m } },
        };

        var bytes = CashSettlementReceiptPdf.Build(recibo);
        var texto = Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-", texto, StringComparison.Ordinal);
        Assert.Equal("LIQ-000012", recibo.Numero);
        Assert.Equal("recibo-liquidacion-LIQ-000012.pdf", CashSettlementReceiptPdf.FileName(recibo));
        Assert.Contains("LIQ-000012", texto, StringComparison.Ordinal);
        Assert.Contains("RUT-20261007232827", texto, StringComparison.Ordinal);
        Assert.Contains("64,100.00", texto, StringComparison.Ordinal);
        Assert.Contains("Cuadrada", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("ANULADA", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Recibo_UnaLiquidacionAnuladaLoDiceConSuMotivo()
    {
        var recibo = new CashSettlementReceiptViewModel
        {
            LiquidacionCobrosId = 1, RutaCodigo = "RUT-1", Estado = "Faltante", Anulada = true, MotivoAnulacion = "Error de digitación",
            FechaLiquidacion = new DateTime(2026, 11, 11),
        };

        var texto = Encoding.Latin1.GetString(CashSettlementReceiptPdf.Build(recibo));

        Assert.Contains("LIQUIDACI", texto, StringComparison.Ordinal);
        Assert.Contains("ANULADA", texto, StringComparison.Ordinal);
        Assert.Contains("Error de digitaci", texto, StringComparison.Ordinal);
    }

    // ── G2-12: mensajes de negocio en Vehículos, Mantenimiento, Alertas y Finanzas ─────
    [Theory]
    [InlineData("VehiclesController.cs", 3)]
    [InlineData("FinanceController.cs", 4)]
    public void G2_12_LosControladoresUsanElMensajeDeNegocio(string controller, int usosMinimos)
    {
        var source = MobileCorreccionesTests.Read("Proyecto_Final", "Controllers", controller);

        Assert.True(source.Split("BusinessErrors.MessageOr(").Length - 1 >= usosMinimos);
        Assert.DoesNotContain("= \"No fue posible completar la operación solicitada.\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("(string.Empty, \"No fue posible completar la operación solicitada.\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void G2_12_MantenimientoYAlertasDeFlotaUsanElMensajeDeNegocio()
    {
        var source = MobileCorreccionesTests.Read("Proyecto_Final", "Controllers", "FleetController.cs");
        var desdeMantenimiento = source[source.IndexOf("CU-153 Mantenimiento", StringComparison.Ordinal)..];

        // Solo las acciones de Mantenimiento y Alertas; Kilometraje es de otra corrección.
        Assert.True(desdeMantenimiento.Split("BusinessErrors.MessageOr(").Length - 1 >= 6);
        Assert.DoesNotContain("TempData[\"ErrorMessage\"] = \"No fue posible completar la operación solicitada.\"", desdeMantenimiento, StringComparison.Ordinal);
    }

    // ── BUG-02-02: teléfono de Mi perfil ──────────────────────────────────────────────
    [Theory]
    [InlineData("88887777")]
    [InlineData("8888-7777")]
    [InlineData("2222 3333")]
    [InlineData("+50688887777", false)]   // 11 dígitos
    [InlineData("12345678901", false)]    // 11 dígitos
    [InlineData("1234567", false)]        // 7 dígitos
    [InlineData("ocho-ocho", false)]
    [InlineData("8888777712345678900000", false)]
    [InlineData("88887777 ext. 4", false)]
    public void Perfil_ElTelefonoAceptaDe8A10DigitosYNadaMas(string telefono, bool valido = true)
    {
        var model = new ProfileEditViewModel { NombreCompleto = "Ana", Correo = "ana@labodega.cr", Telefono = telefono };

        Assert.Equal(valido, Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), validateAllProperties: true));
    }

    [Fact]
    public void Perfil_ElTelefonoEsOpcionalYElNombreTieneTope()
    {
        var sinTelefono = new ProfileEditViewModel { NombreCompleto = "Ana", Correo = "ana@labodega.cr" };
        var nombreLargo = new ProfileEditViewModel { NombreCompleto = new string('a', 151), Correo = "ana@labodega.cr" };

        Assert.True(Validator.TryValidateObject(sinTelefono, new ValidationContext(sinTelefono), new List<ValidationResult>(), true));
        Assert.False(Validator.TryValidateObject(nombreLargo, new ValidationContext(nombreLargo), new List<ValidationResult>(), true));
    }

    // ── BUG-02-01: falla de conexión ──────────────────────────────────────────────────
    [Fact]
    public async Task SinBaseDeDatos_LaRespuestaTieneUnMensajeControlado()
    {
        var status = new Mock<ISessionStatusService>();
        status.Setup(s => s.IsCurrentAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot open server 'secreto' requested by the login"));
        var body = new MemoryStream();
        var context = new DefaultHttpContext { Session = new TestSession() };
        context.Response.Body = body;
        context.Session.SetInt32("UserId", 7);
        var middleware = new SessionStatusMiddleware(_ => Task.CompletedTask, NullLogger<SessionStatusMiddleware>.Instance);

        await middleware.InvokeAsync(context, status.Object);

        var html = Encoding.UTF8.GetString(body.ToArray());
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Contains("No podemos completar esta solicitud", html, StringComparison.Ordinal);
        Assert.DoesNotContain("secreto", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Perfil_ElControladorMuestraUnaVistaDeErrorSinDetallesTecnicos()
    {
        var source = MobileCorreccionesTests.Read("Proyecto_Final", "Controllers", "ProfileController.cs");

        Assert.Contains("return View(\"LoadError\")", source, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status503ServiceUnavailable", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ex.Message", source.Replace("ex.Message == \"DuplicateEmail\"", string.Empty), StringComparison.Ordinal);
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new();
        public bool IsAvailable => true;
        public string Id => "test";
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}
