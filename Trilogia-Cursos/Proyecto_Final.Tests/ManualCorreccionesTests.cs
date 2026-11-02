using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
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

    [Fact]
    public void MisGarantias_ElMenuDelClienteAbreLaListaYNoElFormularioDeSolicitud()
    {
        var layout = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "Shared", "_Layout.cshtml");
        var link = System.Text.RegularExpressions.Regex.Match(layout,
            @"<a asp-controller=""ClientPortal""\s+asp-action=""(\w+)""[^>]*>\s*<i class=""fa fa-undo""></i> Mis garantías");

        Assert.True(link.Success);
        Assert.Equal("Warranties", link.Groups[1].Value);
        // La lista no pide parámetros; el formulario «Warranty» exige la línea del pedido.
        Assert.Empty(typeof(ClientPortalController).GetMethod("Warranties")!.GetParameters());
    }

    [Fact]
    public async Task VentaMovil_ElVendedorAbreElDetalleDeSoloLecturaYNoElDeAdministracion()
    {
        var permisos = new Mock<IRolePermissionService>();
        var menu = new NavigationMenuService(permisos.Object);

        Assert.True(await menu.CanOpenAsync("Vendedor", "SellerOrders", "Detail"));
        Assert.False(await menu.CanOpenAsync("Vendedor", "OrdersAdmin", "Detail"));
        Assert.False(await menu.CanOpenAsync("Cliente", "SellerOrders", "Detail"));
        Assert.Empty(NavigationMenu.GetScreenAccess("SellerOrders", "Detail").Permissions);
    }

    [Fact]
    public void VentaMovil_ElDetalleDelVendedorNoTieneAccionesDeAdministracion()
    {
        var vista = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "OrdersAdmin", "Detail.cshtml");
        var inicio = vista.IndexOf("@if (soloLectura)", StringComparison.Ordinal);
        var soloLectura = vista[inicio..vista.IndexOf("            else", inicio, StringComparison.Ordinal)];
        Assert.DoesNotContain("<form", soloLectura, StringComparison.Ordinal);
        Assert.DoesNotContain("OrdersAdmin", soloLectura, StringComparison.Ordinal);

        foreach (var archivo in new[] { "MyOrders.cshtml", "Confirmation.cshtml" })
        {
            var boton = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "SellerOrders", archivo);
            Assert.DoesNotContain("asp-controller=\"OrdersAdmin\" asp-action=\"Detail\"", boton, StringComparison.Ordinal);
            Assert.Contains("asp-controller=\"@detalleController\" asp-action=\"Detail\"", boton, StringComparison.Ordinal);
        }
    }

    // Toda acción de Venta móvil que recibe el id de un pedido.
    private static string[] NombresAccionesConPedido() => typeof(SellerOrdersController).GetMethods()
        .Where(m => m.ReturnType == typeof(Task<IActionResult>)
                    && m.GetParameters() is [{ Name: "id", ParameterType: var tipo }] && tipo == typeof(int))
        .Select(m => m.Name)
        .Order(StringComparer.Ordinal)
        .ToArray();

    public static TheoryData<string> AccionesConPedido() => new(NombresAccionesConPedido());

    [Fact]
    public void VentaMovil_SeRevisanTodasLasAccionesQueRecibenUnPedido() =>
        Assert.Equal(new[] { "Confirmation", "Detail", "RetainedConfirmation" }, NombresAccionesConPedido());

    [Theory]
    [MemberData(nameof(AccionesConPedido))]
    public async Task VentaMovil_UnPedidoAjenoSeRechaza(string accion)
    {
        var access = new Mock<ISellerOrderAccess>();
        access.Setup(a => a.GetOwnOrderAsync(7, 99)).ReturnsAsync((OrderDetailViewModel?)null);
        var controller = SellerController(access.Object);

        var result = await (Task<IActionResult>)typeof(SellerOrdersController).GetMethod(accion)!.Invoke(controller, [99])!;

        Assert.Equal("MyOrders", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal("No se encontró el pedido entre sus pedidos.", controller.TempData["ErrorMessage"]);
        access.Verify(a => a.GetOwnOrderAsync(7, 99), Times.Once);
    }

    [Fact]
    public async Task VentaMovil_UnPedidoPropioSePermite()
    {
        var pedido = new OrderDetailViewModel { PedidoId = 37, Estado = "Retenido" };
        var access = new Mock<ISellerOrderAccess>();
        access.Setup(a => a.GetOwnOrderAsync(7, 37)).ReturnsAsync(pedido);

        var result = await SellerController(access.Object).RetainedConfirmation(37);

        Assert.Same(pedido, Assert.IsType<ViewResult>(result).Model);
    }

    [Fact]
    public void VentaMovil_LaSincronizacionNoDevuelveNiReenviaPedidosAjenos()
    {
        var controller = MobileCorreccionesTests.Read("Proyecto_Final", "Controllers", "SellerOrdersController.cs");
        var sync = controller[controller.IndexOf("public async Task<IActionResult> SyncOffline", StringComparison.Ordinal)..];
        var check = sync.IndexOf("_orderAccess.IsOwnOrderAsync(vendedorId, result.PedidoId)", StringComparison.Ordinal);

        Assert.True(check > 0);
        Assert.True(check < sync.IndexOf("EnviarComprobantePorCorreoAsync", StringComparison.Ordinal));
        Assert.True(check < sync.IndexOf("RegistrarAuditoriaAsync", StringComparison.Ordinal));
    }

    private static SellerOrdersController SellerController(ISellerOrderAccess access)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = "Server=sin-uso" })
            .Build();
        var http = new DefaultHttpContext { Session = new TestSession() };
        http.Session.SetInt32("UserId", 7);
        http.Session.SetString("UserEmail", "vendedor@x.test");
        http.Session.SetString("UserRole", "Vendedor");
        return new SellerOrdersController(new AdminDbService(configuration), new EmailService(configuration),
            NullLogger<SellerOrdersController>.Instance, Mock.Of<ICrossSellService>(), access)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        };
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
