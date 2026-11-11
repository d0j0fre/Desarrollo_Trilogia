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

    // ───────────── ayudas ─────────────

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
