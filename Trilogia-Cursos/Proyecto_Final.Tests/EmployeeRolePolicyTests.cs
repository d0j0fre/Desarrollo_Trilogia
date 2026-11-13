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

/// <summary>G2-22: un Supervisor no puede crear ni tocar cuentas de Administrador o Gerente, y nadie se inactiva a sí mismo.</summary>
public sealed class EmployeeRolePolicyTests
{
    private static readonly List<EmployeeRoleOptionViewModel> Roles =
    [
        new() { PerfilId = 1, Nombre = "Administrador" },
        new() { PerfilId = 3, Nombre = "Empleado" },
        new() { PerfilId = 4, Nombre = "Vendedor" },
        new() { PerfilId = 5, Nombre = "Gerente" },
        new() { PerfilId = 7, Nombre = "Supervisor" },
        new() { PerfilId = 9, Nombre = "Bodeguero" },
    ];

    [Theory]
    [InlineData("Supervisor", "Administrador", false)]
    [InlineData("Supervisor", "Gerente", false)]
    [InlineData("Supervisor", "Supervisor", false)]
    [InlineData("Supervisor", "Bodeguero", true)]
    [InlineData("Supervisor", "Empleado", true)]
    [InlineData("Administrador", "Administrador", true)]
    [InlineData("Administrador", "Gerente", true)]
    [InlineData("Gerente", "Administrador", false)]
    [InlineData("Gerente", "Supervisor", true)]
    [InlineData("Bodeguero", "Empleado", true)]
    [InlineData("Bodeguero", "Bodeguero", false)]
    [InlineData("Empleado", "Vendedor", false)]
    public void CanAssignAndCanManage_OnlyLowerLevelRoles_AdministratorSeesAll(string actor, string target, bool allowed)
    {
        Assert.Equal(allowed, EmployeeRolePolicy.CanAssign(actor, target));
        Assert.Equal(allowed, EmployeeRolePolicy.CanManage(actor, target));
    }

    [Theory]
    [InlineData(null, "Empleado")]
    [InlineData("", "Empleado")]
    [InlineData("Supervisor", null)]
    [InlineData("Supervisor", " ")]
    public void MissingRole_IsAlwaysDenied(string? actor, string? target)
    {
        Assert.False(EmployeeRolePolicy.CanAssign(actor, target));
        Assert.False(EmployeeRolePolicy.CanManage(actor, target));
    }

    [Fact]
    public void UnknownRole_IsTreatedAsOperationalLevel()
    {
        Assert.True(EmployeeRolePolicy.CanAssign("Supervisor", "Rol nuevo sin nivel"));
        Assert.False(EmployeeRolePolicy.CanAssign("Cajero", "Rol nuevo sin nivel"));
    }

    [Fact]
    public void FilterAssignable_Supervisor_DropsAdministratorGerenteAndPeers()
    {
        var names = EmployeeRolePolicy.FilterAssignable("Supervisor", Roles, role => role.Nombre).Select(role => role.Nombre);
        Assert.Equal(["Empleado", "Vendedor", "Bodeguero"], names);
        Assert.Equal(Roles.Count, EmployeeRolePolicy.FilterAssignable("Administrador", Roles, role => role.Nombre).Count);
    }

    [Fact]
    public async Task Create_SupervisorChoosingAdministrator_IsRejectedBeforeSaving()
    {
        var service = ServiceWithRoles();
        var controller = Controller(service, role: "Supervisor", userId: 20);

        var result = await controller.Create(NewModel(perfilId: 1));

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        service.Verify(s => s.CreateEmployeeAsync(It.IsAny<EmployeeFormViewModel>(), It.IsAny<int?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Create_SupervisorChoosingGerente_IsRejectedBeforeSaving()
    {
        var service = ServiceWithRoles();
        var controller = Controller(service, role: "Supervisor", userId: 20);

        await controller.Create(NewModel(perfilId: 5));

        Assert.False(controller.ModelState.IsValid);
        service.Verify(s => s.CreateEmployeeAsync(It.IsAny<EmployeeFormViewModel>(), It.IsAny<int?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Create_UnknownProfileId_IsRejected()
    {
        var service = ServiceWithRoles();
        var controller = Controller(service, role: "Administrador", userId: 1);

        await controller.Create(NewModel(perfilId: 999));

        Assert.False(controller.ModelState.IsValid);
        service.Verify(s => s.CreateEmployeeAsync(It.IsAny<EmployeeFormViewModel>(), It.IsAny<int?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Create_AdministratorChoosingAdministrator_IsAccepted()
    {
        var service = ServiceWithRoles();
        service.Setup(s => s.CreateEmployeeAsync(It.IsAny<EmployeeFormViewModel>(), It.IsAny<int?>(), It.IsAny<string?>())).ReturnsAsync(77);
        var controller = Controller(service, role: "Administrador", userId: 1);

        await controller.Create(NewModel(perfilId: 1));

        service.Verify(s => s.CreateEmployeeAsync(It.IsAny<EmployeeFormViewModel>(), 1, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task Edit_SupervisorPromotingToAdministrator_IsRejectedBeforeSaving()
    {
        var service = ServiceWithRoles();
        service.Setup(s => s.GetEmployeeDetailAsync(8)).ReturnsAsync(Detail(empleadoId: 8, usuarioId: 80, perfilId: 3, rol: "Empleado"));
        var controller = Controller(service, role: "Supervisor", userId: 20);

        var result = await controller.Edit(EditModel(empleadoId: 8, perfilId: 1));

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        service.Verify(s => s.UpdateEmployeeAsync(It.IsAny<EmployeeFormViewModel>(), It.IsAny<int?>(), It.IsAny<string?>()), Times.Never);
    }

    [Theory]
    [InlineData("Administrador", 1)]
    [InlineData("Gerente", 5)]
    public async Task Edit_SupervisorOnAdministratorOrGerenteAccount_IsRejected(string accountRole, int accountProfile)
    {
        var service = ServiceWithRoles();
        service.Setup(s => s.GetEmployeeDetailAsync(8)).ReturnsAsync(Detail(empleadoId: 8, usuarioId: 80, perfilId: accountProfile, rol: accountRole));
        var controller = Controller(service, role: "Supervisor", userId: 20);

        var result = await controller.Edit(EditModel(empleadoId: 8, perfilId: 3));

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(controller.TempData["ErrorMessage"]);
        service.Verify(s => s.UpdateEmployeeAsync(It.IsAny<EmployeeFormViewModel>(), It.IsAny<int?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Edit_NobodyDeactivatesOwnAccount()
    {
        var service = ServiceWithRoles();
        service.Setup(s => s.GetEmployeeDetailAsync(1)).ReturnsAsync(Detail(empleadoId: 1, usuarioId: 1, perfilId: 1, rol: "Administrador"));
        var controller = Controller(service, role: "Administrador", userId: 1);
        var model = EditModel(empleadoId: 1, perfilId: 1);
        model.Activo = false;

        var result = await controller.Edit(model);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        service.Verify(s => s.UpdateEmployeeAsync(It.IsAny<EmployeeFormViewModel>(), It.IsAny<int?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Edit_GetOnAdministratorAccountAsSupervisor_RedirectsWithoutForm()
    {
        var service = ServiceWithRoles();
        service.Setup(s => s.GetEmployeeFormByIdAsync(8)).ReturnsAsync(new EmployeeFormViewModel { EmpleadoId = 8, PerfilId = 1, Rol = "Administrador" });
        var controller = Controller(service, role: "Supervisor", userId: 20);

        var result = await controller.Edit(8);

        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public async Task ToggleStatus_OwnAccount_IsRejected()
    {
        var service = ServiceWithRoles();
        service.Setup(s => s.GetEmployeeDetailAsync(1)).ReturnsAsync(Detail(empleadoId: 1, usuarioId: 1, perfilId: 1, rol: "Administrador"));
        var controller = Controller(service, role: "Administrador", userId: 1);

        await controller.ToggleStatus(1, null, null);

        Assert.Equal("No puede inactivar su propia cuenta.", controller.TempData["ErrorMessage"]);
        service.Verify(s => s.ToggleEmployeeStatusAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ToggleStatus_SupervisorOnAdministrator_IsRejected()
    {
        var service = ServiceWithRoles();
        service.Setup(s => s.GetEmployeeDetailAsync(2)).ReturnsAsync(Detail(empleadoId: 2, usuarioId: 2, perfilId: 1, rol: "Administrador"));
        var controller = Controller(service, role: "Supervisor", userId: 20);

        await controller.ToggleStatus(2, null, null);

        Assert.NotNull(controller.TempData["ErrorMessage"]);
        service.Verify(s => s.ToggleEmployeeStatusAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ToggleStatus_AdministratorOnAnotherAccount_Proceeds()
    {
        var service = ServiceWithRoles();
        service.Setup(s => s.GetEmployeeDetailAsync(2)).ReturnsAsync(Detail(empleadoId: 2, usuarioId: 2, perfilId: 5, rol: "Gerente"));
        service.Setup(s => s.ToggleEmployeeStatusAsync(2)).ReturnsAsync(false);
        var controller = Controller(service, role: "Administrador", userId: 1);

        await controller.ToggleStatus(2, null, null);

        service.Verify(s => s.ToggleEmployeeStatusAsync(2), Times.Once);
    }

    private static Mock<IEmployeesService> ServiceWithRoles()
    {
        var service = new Mock<IEmployeesService>();
        service.Setup(s => s.GetEmployeeRolesAsync()).ReturnsAsync(Roles);
        service.Setup(s => s.GetEmployeeRoleSelectListAsync(It.IsAny<string?>(), It.IsAny<int?>())).ReturnsAsync([]);
        return service;
    }

    private static EmployeeFormViewModel NewModel(int perfilId) => new()
    {
        PerfilId = perfilId, NombreCompleto = "Persona de prueba", Correo = "prueba@example.invalid", Identificacion = "101110111",
        Puesto = "Pruebas", Contrasena = SyntheticSecret(), Salario = 1000m, Activo = true
    };

    // Valor sintético distinto en cada ejecución: ninguna clave literal en el repositorio.
    private static string SyntheticSecret() => Guid.NewGuid().ToString("N");

    private static EmployeeFormViewModel EditModel(int empleadoId, int perfilId)
    {
        var model = NewModel(perfilId);
        model.EmpleadoId = empleadoId;
        model.RowVersionBase64 = "AAAAAAAAB9E=";
        return model;
    }

    private static EmployeeDetailViewModel Detail(int empleadoId, int usuarioId, int perfilId, string rol) => new()
    {
        EmpleadoId = empleadoId, UsuarioId = usuarioId, PerfilId = perfilId, Rol = rol, NombreCompleto = "Cuenta de prueba"
    };

    private static EmployeesController Controller(Mock<IEmployeesService> service, string role, int userId)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = "Server=sin-uso" })
            .Build();
        var http = new DefaultHttpContext { Session = new TestSession() };
        http.Session.SetInt32("UserId", userId);
        http.Session.SetString("UserEmail", "actor@example.invalid");
        http.Session.SetString("UserRole", role);
        http.Session.SetString("UserFullName", "Actor de prueba");
        return new EmployeesController(service.Object, new AdminDbService(configuration), NullLogger<EmployeesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        };
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

        public string Id => "employee-role-policy-test";
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
