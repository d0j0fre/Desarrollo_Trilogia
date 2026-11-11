using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_Final.Controllers;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>Defectos de Personal / Planilla encontrados al escribir el manual de usuario (G1-02…G1-06, G2-21…G2-36).</summary>
public sealed class RrhhCorreccionesTests
{
    // ───────────── G2-21 · Nuevo empleado muestra la causa real ─────────────

    [Fact]
    public async Task G2_21_CorreoDuplicado_MuestraLaCausaDelProcedimiento()
    {
        var service = new Mock<IEmployeesService>();
        service.Setup(s => s.GetEmployeeRolesAsync()).ReturnsAsync([new EmployeeRoleOptionViewModel { PerfilId = 3, Nombre = "Empleado" }]);
        service.Setup(s => s.GetEmployeeRoleSelectListAsync(It.IsAny<string?>(), It.IsAny<int?>())).ReturnsAsync([]);
        service.Setup(s => s.CreateEmployeeAsync(It.IsAny<EmployeeFormViewModel>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ThrowsAsync(SqlErrors.Create(51000, "Ya existe un usuario registrado con ese correo."));
        var controller = EmployeesController(service.Object);

        var result = await controller.Create(new EmployeeFormViewModel
        {
            PerfilId = 3, NombreCompleto = "Persona de prueba", Correo = "ya.existe@example.invalid", Identificacion = "101110111",
            Puesto = "Pruebas", Contrasena = "clave-de-prueba"
        });

        Assert.IsType<ViewResult>(result);
        var errors = controller.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage).ToList();
        Assert.Equal(["Ya existe un usuario registrado con ese correo."], errors);
    }

    [Fact]
    public async Task G2_21_ErrorTecnico_UsaElMensajeGenericoConTilde()
    {
        var service = new Mock<IEmployeesService>();
        service.Setup(s => s.GetEmployeeRolesAsync()).ReturnsAsync([new EmployeeRoleOptionViewModel { PerfilId = 3, Nombre = "Empleado" }]);
        service.Setup(s => s.GetEmployeeRoleSelectListAsync(It.IsAny<string?>(), It.IsAny<int?>())).ReturnsAsync([]);
        service.Setup(s => s.CreateEmployeeAsync(It.IsAny<EmployeeFormViewModel>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ThrowsAsync(SqlErrors.Create(1205, "Deadlock interno"));
        var controller = EmployeesController(service.Object);

        await controller.Create(new EmployeeFormViewModel
        {
            PerfilId = 3, NombreCompleto = "Persona de prueba", Correo = "otra@example.invalid", Identificacion = "101110111",
            Puesto = "Pruebas", Contrasena = "clave-de-prueba"
        });

        var errors = controller.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage).ToList();
        Assert.Equal(["Ocurrió un error al registrar el empleado. Intente nuevamente."], errors);
        Assert.DoesNotContain("Ocurrio ", Read("Proyecto_Final", "Controllers", "EmployeesController.cs"), StringComparison.Ordinal);
    }

    // ───────────── G2-23 · la identificación va dentro de Datos generales ─────────────

    [Fact]
    public void G2_23_LaIdentificacionVaDentroDeDatosGenerales()
    {
        var view = Read("Proyecto_Final", "Views", "Employees", "Details.cshtml");
        var identification = view.IndexOf("<dt>Identificación</dt>", StringComparison.Ordinal);

        Assert.True(identification > view.IndexOf("Datos generales", StringComparison.Ordinal));
        Assert.DoesNotContain("<strong>Identificación:</strong>", view, StringComparison.Ordinal);
        // Nada de contenido entre la directiva @model y el bloque @{ ... } del encabezado.
        Assert.StartsWith("@model", view);
        Assert.Matches(@"^@model [^
]+?
@{", view);
    }

    // ───────────── G2-24 · transiciones de las licencias ─────────────

    private static readonly DateTime Hoy = new(2026, 11, 11);

    [Theory]
    [InlineData("Pendiente", "Aprobada", true)]
    [InlineData("Pendiente", "Rechazada", true)]
    [InlineData("Pendiente", "Cancelada", false)]
    [InlineData("Pendiente", "Pendiente", false)]
    [InlineData("Aprobada", "Cancelada", true)]
    [InlineData("Aprobada", "Pendiente", false)]
    [InlineData("Aprobada", "Rechazada", false)]
    [InlineData("Rechazada", "Pendiente", false)]
    [InlineData("Rechazada", "Aprobada", false)]
    [InlineData("Cancelada", "Pendiente", false)]
    [InlineData("Cancelada", "Aprobada", false)]
    public void G2_24_SoloSePermitenLasTransicionesDefinidas(string actual, string destino, bool permitido) =>
        Assert.Equal(permitido, EmployeeLeaveTransitions.IsAllowed(actual, destino, Hoy.AddDays(10), Hoy));

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void G2_24_UnaAprobadaQueYaEmpezoNoSePuedeCancelar(int diasDesdeHoy) =>
        Assert.Empty(EmployeeLeaveTransitions.NextStates("Aprobada", Hoy.AddDays(diasDesdeHoy), Hoy));

    [Fact]
    public void G2_24_LaPantallaSoloOfreceLasTransicionesValidasYElProcedimientoLasExige()
    {
        var view = Read("Proyecto_Final", "Views", "Employees", "LeaveRequests.cshtml");
        Assert.Contains("EmployeeLeaveTransitions.NextStates(", view, StringComparison.Ordinal);
        Assert.DoesNotContain("<option selected=\"@(item.Estado == \"Pendiente\")\">Pendiente</option>", view, StringComparison.Ordinal);

        var migration = Read("database", "migrations", "0266_licencias_transiciones_validas.sql");
        Assert.Contains("no vuelve a Pendiente", migration, StringComparison.Ordinal);
        Assert.Contains("ya empezó y no puede cancelarse", migration, StringComparison.Ordinal);
        Assert.Contains("BusinessErrors.MessageOr(", Read("Proyecto_Final", "Controllers", "EmployeesController.cs"), StringComparison.Ordinal);
    }

    // ───────────── G1-02 · el valor de una regla viaja como decimal(18,6) ─────────────

    [Theory]
    [InlineData("AddRuleValue", 18)]
    [InlineData("AddFactor", 9)]
    public void G1_02_ElValorDeLaReglaUsaLaPrecisionDeLaColumna(string method, int precision)
    {
        var add = typeof(PayrollDbService).GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var command = new Microsoft.Data.SqlClient.SqlCommand();

        add.Invoke(null, [command, "@Valor", 15000m]);

        var parameter = command.Parameters["@Valor"];
        Assert.Equal((byte)precision, parameter.Precision);
        Assert.Equal((byte)6, parameter.Scale);
        Assert.Equal(15000m, parameter.Value);
    }

    [Fact]
    public void G1_02_LaPaginaDeErrorDePlanillaNoHablaDePresupuestos()
    {
        var view = Read("Proyecto_Final", "Views", "Payroll", "ServiceUnavailable.cshtml");

        Assert.DoesNotContain("presupuesto", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("asp-controller=\"Payroll\"", view, StringComparison.Ordinal);
    }

    // ───────────── G1-03 · los porcentajes se escriben como se leen ─────────────

    [Theory]
    [InlineData("PorcentajeSalario", 10.67, 0.1067)]
    [InlineData("PorcentajeBruto", 5, 0.05)]
    [InlineData("PorcentajeBruto", 100, 1)]
    [InlineData("MontoFijo", 15000, 15000)]
    [InlineData("PorHoraExtra", 1500.5, 1500.5)]
    public void G1_03_ElPorcentajeSeConvierteAFraccionAlGuardar(string calculo, double escrito, double guardado) =>
        Assert.Equal((decimal)guardado, new PayrollRuleFormViewModel { TipoCalculo = calculo, Valor = (decimal)escrito }.ValorAlmacenado);

    [Theory]
    [InlineData("PorcentajeSalario", 100.01, false)]
    [InlineData("PorcentajeBruto", 100, true)]
    [InlineData("PorcentajeSalario", 10.67, true)]
    [InlineData("MontoFijo", 15000, true)]
    public void G1_03_UnPorcentajeDebeEstarEntreCeroYCien(string calculo, double valor, bool valido)
    {
        var rule = ValidRule(calculo, (decimal)valor);
        var errors = Validate(rule);

        Assert.Equal(valido, errors.Count == 0);
    }

    [Fact]
    public void G1_03_LaComisionDeVentasSeEscribeComoPorcentaje()
    {
        Assert.Empty(Validate(ValidRule("PorcentajeBruto", 5m, "COMISION_VENTAS")));
        Assert.NotEmpty(Validate(ValidRule("MontoFijo", 5m, "COMISION_VENTAS")));
        Assert.NotEmpty(Validate(ValidRule("PorcentajeBruto", 500m, "COMISION_VENTAS")));
    }

    [Theory]
    [InlineData("PorcentajeSalario", 0.1067, "10,67 %")]
    [InlineData("PorcentajeBruto", 0.05, "5 %")]
    [InlineData("MontoFijo", 15000, "₡15 000,00")]
    public void G1_03_LasReglasSeListanConSuPorcentaje(string calculo, double guardado, string esperado)
    {
        var text = new PayrollRuleViewModel { TipoCalculo = calculo, Valor = (decimal)guardado }.ValorTexto;

        Assert.Equal(esperado, text.Replace(' ', ' ').Replace(' ', ' '));
    }

    [Fact]
    public void G1_03_LaHuellaDelCalculoNoCambiaPorLaPresentacion()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new PayrollRuleViewModel { Codigo = "X", TipoCalculo = "PorcentajeBruto", Valor = 0.05m });

        Assert.DoesNotContain("ValorTexto", json, StringComparison.Ordinal);
    }

    // ───────────── G1-04 · errores de planilla legibles ─────────────

    [Fact]
    public async Task G1_04_UnaReglaDeNegocioVuelveAPlanillaConElTextoDelProcedimiento()
    {
        var payroll = new Mock<IPayrollService>();
        payroll.Setup(p => p.ChangeStateAsync(It.IsAny<PayrollStateChangeViewModel>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(SqlErrors.Create(55253, "La aprobación requiere un actor distinto del calculador."));
        var controller = NewPayrollController(payroll.Object);

        var result = await controller.Approve(new PayrollStateChangeViewModel { CalculoId = 5, RowVersionBase64 = "AAAAAAAAB9E=" }, CancellationToken.None);

        Assert.Equal(nameof(PayrollController.Index), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal("La aprobación requiere un actor distinto del calculador.", controller.TempData["ErrorMessage"]);
        Assert.Null(controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task G1_04_UnaConfiguracionIncompletaMuestraSuCausa()
    {
        var payroll = new Mock<IPayrollService>();
        payroll.Setup(p => p.CalculateAsync(It.IsAny<PayrollCalculationRequestViewModel>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PayrollConfigurationException("Las deducciones configuradas superan el total bruto."));
        var controller = NewPayrollController(payroll.Object);

        var result = await controller.Calculate(new PayrollCalculationRequestViewModel { PeriodoId = 1, EmpleadoId = 2 }, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Las deducciones configuradas superan el total bruto.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task G1_04_UnaSolicitudInvalidaVuelveConUnMensajeLegibleSinJson()
    {
        var payroll = new Mock<IPayrollService>();
        var controller = NewPayrollController(payroll.Object);
        controller.ModelState.AddModelError("PeriodoId", "Seleccione un período abierto.");

        var result = await controller.Calculate(new PayrollCalculationRequestViewModel(), CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("La solicitud no es válida: Seleccione un período abierto.", controller.TempData["ErrorMessage"]);
        payroll.Verify(p => p.CalculateAsync(It.IsAny<PayrollCalculationRequestViewModel>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task G1_04_UnaFallaTecnicaNoExponeElDetalleInterno()
    {
        var payroll = new Mock<IPayrollService>();
        payroll.Setup(p => p.CreatePeriodAsync(It.IsAny<PayrollPeriodFormViewModel>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(SqlErrors.Create(1205, "Deadlock interno"));
        var controller = NewPayrollController(payroll.Object);

        var result = await controller.CreatePeriod(new PayrollPeriodFormViewModel(), CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.DoesNotContain("Deadlock", (string)controller.TempData["ErrorMessage"]!);
        Assert.Contains("No fue posible procesar", (string)controller.TempData["ErrorMessage"]!);
    }

    [Fact]
    public void G1_04_PlanillaYaNoDevuelveJsonCrudo() =>
        Assert.DoesNotContain("UnprocessableEntity", Read("Proyecto_Final", "Controllers", "PayrollController.cs"), StringComparison.Ordinal);

    // ───────────── G1-05 · listas desplegables en Calcular empleado ─────────────

    [Fact]
    public void G1_05_CalcularEmpleadoUsaListasDeLaBaseYNoIdentificadoresEscritos()
    {
        var view = Read("Proyecto_Final", "Views", "Payroll", "Index.cshtml");

        Assert.Contains("<select id=\"period-id\" name=\"PeriodoId\"", view, StringComparison.Ordinal);
        Assert.Contains("<select id=\"employee-id\" name=\"EmpleadoId\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ID del período", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ID del empleado", view, StringComparison.Ordinal);
    }

    [Fact]
    public async Task G1_05_LaPantallaCargaPeriodosAbiertosYEmpleadosActivos()
    {
        var payroll = new Mock<IPayrollService>();
        payroll.Setup(p => p.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        payroll.Setup(p => p.ListRulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        payroll.Setup(p => p.ListOpenPeriodsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PayrollPeriodOptionViewModel { PeriodoId = 4, Tipo = "Quincenal", Desde = new DateTime(2026, 11, 1), Hasta = new DateTime(2026, 11, 15), FactorSalario = 0.5m }]);
        payroll.Setup(p => p.ListActiveEmployeesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PayrollEmployeeOptionViewModel { EmpleadoId = 9, NombreCompleto = "Ana Mora", Puesto = "Cajera" }]);
        var controller = NewPayrollController(payroll.Object);

        var model = Assert.IsType<PayrollIndexViewModel>(Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None)).Model);

        Assert.Equal("Quincenal · 01/11/2026 al 15/11/2026", Assert.Single(model.PeriodosAbiertos).Etiqueta);
        Assert.Equal("Ana Mora · Cajera", Assert.Single(model.Empleados).Etiqueta);
    }

    // ───────────── G2-25 / G2-26 · Aprobar jornadas ─────────────

    [Fact]
    public async Task G2_26_LaPropiaJornadaNoSeOfreceParaAprobarla()
    {
        var attendance = new Mock<IAttendanceService>();
        attendance.Setup(a => a.GetPendingAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Jornada(1, usuarioId: 7, "Mía"), Jornada(2, usuarioId: 30, "De otra persona")]);
        var controller = NewAttendanceController(attendance.Object, userId: 7);

        var result = Assert.IsType<ViewResult>(await controller.Index(null, null, CancellationToken.None));

        var shown = Assert.IsAssignableFrom<IEnumerable<AttendanceEntryViewModel>>(result.Model).ToList();
        Assert.Equal(["De otra persona"], shown.Select(entry => entry.NombreCompleto));
        Assert.Equal(1, (int)controller.ViewBag.PropiasOcultas);
    }

    [Fact]
    public async Task G2_26_AprobarLaPropiaJornadaMuestraLaCausaDelProcedimiento()
    {
        var attendance = new Mock<IAttendanceService>();
        attendance.Setup(a => a.DecideAsync(It.IsAny<AttendanceDecisionViewModel>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(SqlErrors.Create(55121, "No puede resolver su propia jornada."));
        var controller = NewAttendanceController(attendance.Object, userId: 7);

        await controller.Decide(new AttendanceDecisionViewModel { JornadaId = 1, Decision = "Aprobada", RowVersionBase64 = "AAAAAAAAB9E=" }, null, null, CancellationToken.None);

        Assert.Equal("No puede resolver su propia jornada.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task G2_26_UnErrorTecnicoMantieneElMensajeGenerico()
    {
        var attendance = new Mock<IAttendanceService>();
        attendance.Setup(a => a.DecideAsync(It.IsAny<AttendanceDecisionViewModel>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(SqlErrors.Create(1205, "Deadlock"));
        var controller = NewAttendanceController(attendance.Object, userId: 7);

        await controller.Decide(new AttendanceDecisionViewModel { JornadaId = 1, Decision = "Aprobada", RowVersionBase64 = "AAAAAAAAB9E=" }, null, null, CancellationToken.None);

        Assert.Equal("No fue posible resolver la jornada; recargue la lista.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public void G2_25_LaPantallaMuestraObservacionesYFiltroDeFechas()
    {
        var view = Read("Proyecto_Final", "Views", "Attendance", "Index.cshtml");

        Assert.Contains("Observaciones del empleado", view, StringComparison.Ordinal);
        Assert.Contains("item.Observaciones", view, StringComparison.Ordinal);
        Assert.Contains("name=\"desde\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"hasta\"", view, StringComparison.Ordinal);
        // La decisión conserva el rango que se estaba mirando.
        Assert.Contains("<input type=\"hidden\" name=\"desde\"", view, StringComparison.Ordinal);
    }

    // ───────────── ayudas ─────────────

    private static AttendanceEntryViewModel Jornada(long id, int usuarioId, string nombre) => new()
    {
        JornadaId = id, UsuarioId = usuarioId, NombreCompleto = nombre, Fecha = new DateTime(2026, 11, 3), HorasOrdinarias = 8,
        Observaciones = "Horas extra por inventario", Estado = "Enviada", RowVersionBase64 = "AAAAAAAAB9E="
    };

    private static AttendanceController NewAttendanceController(IAttendanceService attendance, int userId)
    {
        var http = Http("Supervisor", userId);
        return new AttendanceController(attendance, NullLogger<AttendanceController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        };
    }

    private static PayrollRuleFormViewModel ValidRule(string calculo, decimal valor, string codigo = "BONO") => new()
    {
        Codigo = codigo, Nombre = "Regla de prueba", Tipo = "Ingreso", TipoCalculo = calculo, Valor = valor,
        VigenteDesde = new DateTime(2026, 1, 1), Fuente = "Fuente de prueba"
    };

    private static List<System.ComponentModel.DataAnnotations.ValidationResult> Validate(object model)
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, new System.ComponentModel.DataAnnotations.ValidationContext(model), results, true);
        return results;
    }

    private static PayrollController NewPayrollController(IPayrollService payroll)
    {
        var http = Http("Administrador", 1);
        return new PayrollController(payroll, NullLogger<PayrollController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        };
    }

    private static EmployeesController EmployeesController(IEmployeesService service, string role = "Administrador", int userId = 1)
    {
        var http = Http(role, userId);
        return new EmployeesController(service, new AdminDbService(Configuration()), NullLogger<EmployeesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        };
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = "Server=sin-uso" })
        .Build();

    private static DefaultHttpContext Http(string role, int userId)
    {
        var http = new DefaultHttpContext { Session = new TestSession() };
        http.Session.SetInt32("UserId", userId);
        http.Session.SetString("UserEmail", "actor@example.invalid");
        http.Session.SetString("UserRole", role);
        http.Session.SetString("UserFullName", "Actor de prueba");
        return http;
    }

    internal static string Read(params string[] parts) => MobileCorreccionesTests.Read(parts);

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

        public string Id => "rrhh-correcciones-test";
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
