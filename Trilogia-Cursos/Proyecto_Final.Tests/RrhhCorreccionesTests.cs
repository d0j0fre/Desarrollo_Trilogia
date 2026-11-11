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
            Puesto = "Pruebas", Contrasena = SyntheticSecret()
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
            Puesto = "Pruebas", Contrasena = SyntheticSecret()
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

    // ───────────── G2-27 · notificar boletas explica lo que falta ─────────────

    private static IConfiguration Settings(params (string Key, string? Value)[] values) => new ConfigurationBuilder()
        .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value)).Build();

    private static readonly (string, string?)[] MailSettings =
    [
        ("ConfiguracionCorreo:Host", "smtp.example.invalid"), ("ConfiguracionCorreo:Puerto", "587"),
        ("ConfiguracionCorreo:Remitente", "remitente@example.invalid"), ("ConfiguracionCorreo:Contrasenna", SyntheticSecret()),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://localhost:7250")]
    [InlineData("no-es-una-url")]
    public void G2_27_SinUrlPublicaHttpsLaPantallaExplicaElMotivo(string? baseUrl)
    {
        var configuration = Settings([("PaySlips:PublicBaseUrl", baseUrl), .. MailSettings]);

        var problem = PaySlipDeliveryDiagnostics.GetConfigurationProblem(configuration);

        Assert.Equal(PaySlipDeliveryDiagnostics.MissingBaseUrlMessage, problem);
        Assert.Contains("PaySlips:PublicBaseUrl", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void G2_27_SinCorreoConfiguradoLaPantallaLoDice()
    {
        var configuration = Settings(("PaySlips:PublicBaseUrl", "https://sitio.example.invalid"), ("ConfiguracionCorreo:Host", "smtp.example.invalid"));

        Assert.Equal(PaySlipDeliveryDiagnostics.MissingMailMessage, PaySlipDeliveryDiagnostics.GetConfigurationProblem(configuration));
    }

    [Fact]
    public void G2_27_ConLaConfiguracionCompletaNoHayProblema() =>
        Assert.Null(PaySlipDeliveryDiagnostics.GetConfigurationProblem(Settings([("PaySlips:PublicBaseUrl", "https://sitio.example.invalid"), .. MailSettings])));

    [Fact]
    public async Task G2_27_SinConfiguracionNoSeIntentaElEnvioNiSeRegistraUnFallo()
    {
        var delivery = new Mock<IPaySlipDeliveryCoordinator>();
        var http = Http("Supervisor", 20);
        var controller = new PaySlipsController(Mock.Of<IPaySlipService>(), delivery.Object, Settings(("PaySlips:PublicBaseUrl", "")), NullLogger<PaySlipsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        };

        await controller.Send(new PaySlipSendViewModel { CalculoId = 3, IdempotencyKey = Guid.NewGuid() }, CancellationToken.None);

        Assert.Equal(PaySlipDeliveryDiagnostics.MissingBaseUrlMessage, controller.TempData["ErrorMessage"]);
        delivery.Verify(d => d.SendAsync(It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void G2_27_LaConfiguracionDeDesarrolloTraeLaUrlHttpsSinSecretos()
    {
        using var settings = System.Text.Json.JsonDocument.Parse(Read("Proyecto_Final", "appsettings.Development.json"));

        var baseUrl = settings.RootElement.GetProperty("PaySlips").GetProperty("PublicBaseUrl").GetString();
        Assert.True(Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);
        Assert.Equal("", settings.RootElement.GetProperty("ConfiguracionCorreo").GetProperty("Contrasenna").GetString());

        var docs = Read("docs", "azure-despliegue-final-qa.md");
        Assert.Contains("PaySlips__PublicBaseUrl", docs, StringComparison.Ordinal);
    }

    // ───────────── G2-28 / G2-35 · montos, fechas y tipos con formato ─────────────

    [Theory]
    [InlineData(255000, "₡255 000,00")]
    [InlineData(1234567.5, "₡1 234 567,50")]
    [InlineData(0, "₡0,00")]
    public void G2_28_LosMontosLlevanColonYSeparadorDeMiles(double amount, string expected) =>
        Assert.Equal(expected, Plain(RrhhFormat.Money((decimal)amount)));

    [Fact]
    public void G2_28_LasFechasDelPeriodoSeMuestranDiaMesAnio()
    {
        Assert.Equal("16/10/2026 — 31/10/2026", RrhhFormat.PeriodText("2026-10-16 — 2026-10-31"));
        Assert.Equal("Octubre", RrhhFormat.PeriodText("Octubre"));
        Assert.Equal("16/10/2026", RrhhFormat.Date(new DateTime(2026, 10, 16)));
    }

    [Theory]
    [InlineData("Deduccion", "Deducción")]
    [InlineData("Ingreso", "Ingreso")]
    public void G2_28_ElTipoDeConceptoLlevaTilde(string stored, string shown) =>
        Assert.Equal(shown, RrhhFormat.ConceptType(stored));

    [Theory]
    [InlineData("PaySlips", "Index.cshtml")]
    [InlineData("MyPaySlips", "Index.cshtml")]
    [InlineData("MyPaySlips", "Details.cshtml")]
    public void G2_28_LasBoletasFormateanMontosFechasYTipos(string folder, string file)
    {
        var view = Read("Proyecto_Final", "Views", folder, file);

        Assert.Contains("RrhhFormat.Money(", view, StringComparison.Ordinal);
        Assert.DoesNotContain("@item.TotalBruto<", view, StringComparison.Ordinal);
        Assert.DoesNotContain("@Model.TotalNeto<", view, StringComparison.Ordinal);
        Assert.DoesNotContain("@line.Monto<", view, StringComparison.Ordinal);
        Assert.DoesNotContain("@line.Tipo<", view, StringComparison.Ordinal);
    }

    [Fact]
    public void G2_35_MisBoletasUsaBotonesDelSitioEnLugarDeEnlacesAmarillos()
    {
        var view = Read("Proyecto_Final", "Views", "MyPaySlips", "Index.cshtml");

        Assert.Contains("asp-action=\"Details\" asp-route-id=\"@item.CalculoId\" class=\"btn btn-sm btn-ds-view\"", view, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Download\" asp-route-id=\"@item.CalculoId\" class=\"btn btn-sm btn-ds-view\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("</a> · <a", view, StringComparison.Ordinal);
    }

    // ───────────── G2-36 · Mi portal para todo el personal con expediente ─────────────

    [Fact]
    public void G2_36_MiPortalYaNoTieneRolesFijos()
    {
        var attribute = Assert.Single(typeof(EmployeePortalController).GetCustomAttributes(typeof(Proyecto_Final.Filters.SessionAuthorizeAttribute), true)
            .Cast<Proyecto_Final.Filters.SessionAuthorizeAttribute>());

        Assert.Empty(attribute.AllowedRoles);
    }

    [Theory]
    [InlineData("Bodeguero")]
    [InlineData("Cajero")]
    [InlineData("Chofer")]
    [InlineData("Supervisor")]
    public async Task G2_36_UnPuestoDeOficinaConExpedienteAbreMiPortal(string role)
    {
        var employees = new Mock<IEmployeesService>();
        employees.Setup(e => e.GetEmployeePortalAsync(31)).ReturnsAsync(new EmployeePortalViewModel { Perfil = new EmployeeDetailViewModel { EmpleadoId = 5, UsuarioId = 31 } });
        var http = Http(role, 31);
        var controller = new EmployeePortalController(employees.Object, new AdminDbService(Configuration()))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        };

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task G2_36_UnClienteNoEntraAMiPortal()
    {
        var http = Http("Cliente", 40);
        var controller = new EmployeePortalController(Mock.Of<IEmployeesService>(), new AdminDbService(Configuration()))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        };

        var result = Assert.IsType<ViewResult>(await controller.Index());

        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public void G2_36_ElMenuMuestraMiPortalATodoElPersonalYMisJornadasSoloAQuienPuedeAbrirlas()
    {
        var layout = Read("Proyecto_Final", "Views", "Shared", "_Layout.cshtml");
        var portal = layout.IndexOf("asp-controller=\"EmployeePortal\"", StringComparison.Ordinal);
        var attendance = layout.IndexOf("asp-controller=\"MyAttendance\"", StringComparison.Ordinal);

        Assert.Contains("@if (isLoggedIn && !isClient)", layout[(portal - 220)..portal], StringComparison.Ordinal);
        Assert.Contains("CanOpenAsync(userRole, \"MyAttendance\", \"Index\")", layout[(attendance - 220)..attendance], StringComparison.Ordinal);
    }

    // ───────────── BUD-QF · el formulario rápido de Presupuestos acepta decimales ─────────────

    [Fact]
    public void BUD_QF_ElMontoRenombradoLlevaSuPropiaMarcaDeCulturaInvariante()
    {
        var view = Read("Proyecto_Final", "Views", "Budgets", "Index.cshtml");

        // El tag helper marca la cultura invariante con el nombre del modelo (NewBudget.AnnualAmount); al renombrar el
        // campo a «AnnualAmount» la marca debe llevar ese mismo nombre o «1200000.50» se lee con la cultura del servidor.
        Assert.Contains("name=\"AnnualAmount\"", view, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"__Invariant\" value=\"AnnualAmount\" />", view, StringComparison.Ordinal);
    }

    // ───────────── Mis jornadas para todo el personal con el permiso ─────────────

    [Fact]
    public void MisJornadas_SeAbrePorPermisoYNoPorNombreDeRol()
    {
        var type = typeof(MyAttendanceController);

        Assert.Empty(type.GetCustomAttributes(typeof(Proyecto_Final.Filters.SessionAuthorizeAttribute), true));
        var viewPermission = Assert.Single(type.GetCustomAttributes(typeof(Proyecto_Final.Filters.AdminAuthorizeAttribute), true).Cast<Proyecto_Final.Filters.AdminAuthorizeAttribute>());
        Assert.Equal("RRHH_JORNADAS_VER_PROPIAS", viewPermission.Arguments![1]);

        var save = type.GetMethod("Save")!;
        var savePermission = Assert.Single(save.GetCustomAttributes(typeof(Proyecto_Final.Filters.AdminAuthorizeAttribute), true).Cast<Proyecto_Final.Filters.AdminAuthorizeAttribute>());
        Assert.Equal("RRHH_JORNADAS_REGISTRAR", savePermission.Arguments![1]);
    }

    [Theory]
    [InlineData("Bodeguero", true)]
    [InlineData("Chofer", true)]
    [InlineData("Gerente", true)]
    [InlineData("Cliente", false)]
    public async Task MisJornadas_ElMenuLoMuestraSegunElPermiso(string role, bool visible)
    {
        const string staff = "RRHH_JORNADAS_REGISTRAR:RRHH,RRHH_JORNADAS_VER_PROPIAS:RRHH";
        var permissions = new Mock<IRolePermissionService>();
        permissions.Setup(p => p.HasCodePermissionAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string r, string code) => r != "Cliente" && staff.Contains(code, StringComparison.Ordinal));
        var menu = new NavigationMenuService(permissions.Object);

        Assert.Equal(visible, await menu.CanOpenAsync(role, "MyAttendance", "Index"));
        Assert.Equal(visible, await menu.CanSubmitAsync(role, "MyAttendance", "Save"));
    }

    // ───────────── ServiceUnavailable genérico ─────────────

    [Fact]
    public void ServiceUnavailable_ElTextoSirveParaCualquierPantalla()
    {
        var view = Read("Proyecto_Final", "Views", "Shared", "ServiceUnavailable.cshtml");

        Assert.DoesNotContain("presupuesto", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("asp-controller=\"Budgets\"", view, StringComparison.Ordinal);
        Assert.Contains("ViewData[\"VolverTexto\"]", view, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"Home\" asp-action=\"Index\"", view, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("BudgetsController.cs", "presupuestos")]
    [InlineData("CartController.cs", "el carrito")]
    [InlineData("ClientsController.cs", "clientes")]
    [InlineData("ExpensesController.cs", "gastos")]
    public void ServiceUnavailable_CadaPantallaIndicaADondeVolver(string controller, string moduleText) =>
        Assert.Contains($"ViewData[\"VolverTexto\"] = \"{moduleText}\"", Read("Proyecto_Final", "Controllers", controller), StringComparison.Ordinal);

    // ───────────── Contraseña de empleados solo con hash (0269) ─────────────

    private static Microsoft.Data.SqlClient.SqlCommand CredentialCommand(string? password)
    {
        var command = new Microsoft.Data.SqlClient.SqlCommand();
        var add = typeof(EmployeesDbService).GetMethod("AddCredentialHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        try { add.Invoke(null, [command, password]); }
        catch (System.Reflection.TargetInvocationException exception) { throw exception.InnerException!; }
        return command;
    }

    [Fact]
    public void Empleados_LaClaveViajaSoloComoHashPbkdf2()
    {
        var secret = SyntheticSecret();
        var command = CredentialCommand($"  {secret}  ");

        var parameter = Assert.Single(command.Parameters.Cast<Microsoft.Data.SqlClient.SqlParameter>());
        Assert.Equal("@ContrasenaHash", parameter.ParameterName);
        var hash = Assert.IsType<string>(parameter.Value);
        Assert.StartsWith("pbkdf2-sha256$", hash, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, hash, StringComparison.Ordinal);
        // Lo verifica la API, que es quien autentica (la clave se recorta igual que al iniciar sesión).
        Assert.True(new Proyecto_FinalAPI.Services.PasswordHashService().Verify(secret, hash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empleados_SinClaveNoSeCreaLaCuenta(string? password) =>
        Assert.Throws<InvalidOperationException>(() => CredentialCommand(password));

    [Fact]
    public void Empleados_NingunProcedimientoRecibeLaClaveEnTexto()
    {
        var service = Read("Proyecto_Final", "Services", "EmployeesDbService.cs");
        Assert.DoesNotContain("\"@Contrasena\"", service, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcedureHasParameterAsync", service, StringComparison.Ordinal);

        var migration = Read("database", "migrations", "0269_empleados_contrasena_solo_hash.sql");
        Assert.Contains("@ContrasenaHash NVARCHAR(512)", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("@Contrasena NVARCHAR", migration, StringComparison.Ordinal);
        Assert.Contains("N'', @ContrasenaHash, 1", migration, StringComparison.Ordinal);
        Assert.Contains("0269", Read("database", "migrations", "README.md"), StringComparison.Ordinal);
    }

    // ───────────── ayudas ─────────────

    // Valor sintético distinto en cada ejecución: ninguna clave literal en el repositorio.
    private static string SyntheticSecret() => Guid.NewGuid().ToString("N");

    private static string Plain(string text) => text.Replace('\u00A0', ' ').Replace('\u202F', ' ');

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
