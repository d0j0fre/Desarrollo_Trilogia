using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_Final.Controllers;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>Defectos encontrados al escribir el manual de usuario.</summary>
public sealed class ManualCorreccionesTests
{
    private static readonly DateTime Fecha = new(2026, 10, 28);

    [Theory]
    [InlineData("Borrador", true)]
    [InlineData("Rechazada", true)]
    [InlineData("Enviada", false)]
    [InlineData("Aprobada", false)]
    public void MisJornadas_SoloBorradorORechazadaSePuedenCompletar(string estado, bool editable) =>
        Assert.Equal(editable, AttendanceRules.IsEditable(estado));

    [Fact]
    public async Task MisJornadas_GuardarLaMismaFechaCompletaElBorrador()
    {
        var service = new Mock<IAttendanceService>();
        service.Setup(s => s.GetMineAsync(7, Fecha, Fecha, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Jornada("Borrador")]);
        var controller = Controller(service.Object);

        await controller.Save(new AttendanceFormViewModel { Fecha = Fecha, HorasOrdinarias = 8 }, true, CancellationToken.None);

        service.Verify(s => s.SaveMineAsync(7, It.Is<AttendanceFormViewModel>(m => m.JornadaId == 41 && m.RowVersionBase64 == "AAAAAAAAB9E="),
            true, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("Jornada enviada para aprobación.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task MisJornadas_UnaJornadaEnviadaNoSeSobrescribeYSeExplicaPorQue()
    {
        var service = new Mock<IAttendanceService>();
        service.Setup(s => s.GetMineAsync(7, Fecha, Fecha, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Jornada("Enviada")]);
        var controller = Controller(service.Object);

        await controller.Save(new AttendanceFormViewModel { Fecha = Fecha, HorasOrdinarias = 8 }, true, CancellationToken.None);

        service.Verify(s => s.SaveMineAsync(It.IsAny<int>(), It.IsAny<AttendanceFormViewModel>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("Ya registró la jornada del 28/10/2026 y está enviada: no se puede modificar.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task MisJornadas_CompletarCargaLaJornadaRechazadaEnElFormulario()
    {
        var service = new Mock<IAttendanceService>();
        service.Setup(s => s.GetMineAsync(7, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Jornada("Rechazada")]);

        var result = await Controller(service.Object).Index(null, Fecha, 41, CancellationToken.None);

        var model = Assert.IsType<MyAttendanceIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(41, model.Form.JornadaId);
        Assert.Equal("AAAAAAAAB9E=", model.Form.RowVersionBase64);
        Assert.Equal(8m, model.Form.HorasOrdinarias);
        var view = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "MyAttendance", "Index.cshtml");
        Assert.Contains("name=\"JornadaId\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"RowVersionBase64\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Creditos_ElLimiteSeEscribeYSeLeeEnCulturaInvariante()
    {
        var view = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "Credits", "Details.cshtml");

        Assert.Contains("value=\"@Model.LimiteCredito.ToString(System.Globalization.CultureInfo.InvariantCulture)\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"__Invariant\" value=\"LimiteCredito\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"__Invariant\" value=\"Monto\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"@Model.LimiteCredito\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void MensajeDoble_ElLayoutNoRepiteElAvisoQueYaMostroLaVista()
    {
        var layout = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "Shared", "_Layout.cshtml");

        foreach (var key in new[] { "LoginSuccess", "SuccessMessage", "ErrorMessage" })
        {
            var line = layout.Split('\n').Single(l => l.Contains($"TempData[\"{key}\"]", StringComparison.Ordinal));
            Assert.Contains("!ShownByView(", line, StringComparison.Ordinal);
        }
        Assert.Contains("@Html.Raw(bodyHtml)", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("@RenderBody()", layout, StringComparison.Ordinal);
    }

    private static AttendanceEntryViewModel Jornada(string estado) => new()
    {
        JornadaId = 41, Fecha = Fecha, HorasOrdinarias = 8, Estado = estado, RowVersionBase64 = "AAAAAAAAB9E="
    };

    private static MyAttendanceController Controller(IAttendanceService service)
    {
        var http = new DefaultHttpContext { Session = new TestSession() };
        http.Session.SetInt32("UserId", 7);
        return new MyAttendanceController(service, NullLogger<MyAttendanceController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        };
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

        public string Id => "manual-correcciones-test";
        public bool IsAvailable => true;
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}
