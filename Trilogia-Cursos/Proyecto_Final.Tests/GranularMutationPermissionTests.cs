using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Moq;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

// Seguridad / permisos granulares de mutación (migración 0313): las acciones POST que cambian datos
// exigen el permiso de esa acción y no solo el del módulo. Se reutiliza AdminAuthorize y
// NavigationMenuService.CanSubmitAsync; no hay un segundo sistema de capacidades.
public sealed class GranularMutationPermissionTests
{
    public static TheoryData<string, string, string, string> Mutations => new()
    {
        { "Clients", "Create", "GET", "CLIENTES_CREAR" },
        { "Clients", "Create", "POST", "CLIENTES_CREAR" },
        { "Clients", "Edit", "GET", "CLIENTES_EDITAR" },
        { "Clients", "Edit", "POST", "CLIENTES_EDITAR" },
        { "Clients", "ToggleStatus", "POST", "CLIENTES_INACTIVAR" },
        { "Consultations", "UpdateStatus", "POST", "CONSULTAS_ATENDER" },
        { "Credits", "UpdateSettings", "POST", "CREDITOS_CONFIGURAR" },
        { "Credits", "RegisterMovement", "POST", "CREDITOS_MOVIMIENTOS" },
        { "AccountsReceivableAdmin", "UpdateSettings", "POST", "CREDITOS_CONFIGURAR" },
        { "AccountsReceivableAdmin", "RegisterMovement", "POST", "CREDITOS_MOVIMIENTOS" },
        { "Inventory", "Create", "GET", "INVENTARIO_CREAR" },
        { "Inventory", "Edit", "GET", "INVENTARIO_EDITAR" },
        { "Inventory", "ToggleFeatured", "POST", "INVENTARIO_EDITAR" },
        { "Inventory", "ToggleStatus", "POST", "INVENTARIO_EDITAR" },
        { "Inventory", "Delete", "POST", "INVENTARIO_EDITAR" },
        { "Inventory", "DeletePermanent", "POST", "INVENTARIO_ELIMINAR" },
        { "Employees", "CreateTask", "POST", "EMPLEADOS_TAREAS" },
        { "Employees", "UpdateTaskStatus", "POST", "EMPLEADOS_TAREAS" },
        { "Employees", "UpdateLeaveRequest", "POST", "EMPLEADOS_SOLICITUDES" },
    };

    [Theory]
    [MemberData(nameof(Mutations))]
    public void MutationActions_DeclareTheirOwnPermissionCode(string controller, string action, string verb, string code)
    {
        var access = NavigationMenu.GetScreenAccess(controller, action, verb);

        Assert.Contains(access.Permissions, permission => permission.Code == code);
    }

    [Theory]
    [MemberData(nameof(Mutations))]
    public async Task ReadOnlyProfile_PassesTheModuleCheckButIsRefusedTheMutation(string controller, string action, string verb, string code)
    {
        // Perfil de solo lectura: tiene el módulo (consulta) pero ningún permiso de mutación.
        Assert.False(code.EndsWith("_VER", StringComparison.Ordinal));
        var permissions = new Mock<IRolePermissionService>();
        permissions.Setup(p => p.HasModulePermissionAsync("Solo lectura", It.IsAny<string>())).ReturnsAsync(true);
        permissions.Setup(p => p.HasCodePermissionAsync("Solo lectura", It.IsAny<string>()))
            .ReturnsAsync((string _, string requested) => requested.EndsWith("_VER", StringComparison.Ordinal));

        // 1. El menú (botones) y 2. el filtro real (la acción) deciden igual y rechazan con 403.
        Assert.False(await new NavigationMenuService(permissions.Object).CanSubmitAsync("Solo lectura", controller, action));
        var filterContext = Context("Solo lectura");
        await BuildFilters(controller, action, verb, permissions.Object, filterContext);

        var view = Assert.IsType<ViewResult>(filterContext.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, view.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Mutations))]
    public async Task ProfileWithTheSpecificPermission_IsAllowed(string controller, string action, string verb, string code)
    {
        var permissions = new Mock<IRolePermissionService>();
        permissions.Setup(p => p.HasModulePermissionAsync("Con permiso", It.IsAny<string>())).ReturnsAsync(true);
        permissions.Setup(p => p.HasCodePermissionAsync("Con permiso", code)).ReturnsAsync(true);
        var filterContext = Context("Con permiso");

        await BuildFilters(controller, action, verb, permissions.Object, filterContext);

        Assert.Null(filterContext.Result);
    }

    [Theory]
    [InlineData("Clients", "Create", "POST")]
    [InlineData("Credits", "RegisterMovement", "POST")]
    [InlineData("Inventory", "DeletePermanent", "POST")]
    [InlineData("Employees", "UpdateLeaveRequest", "POST")]
    public async Task Administrator_KeepsEveryMutation(string controller, string action, string verb)
    {
        var permissions = new Mock<IRolePermissionService>(MockBehavior.Strict);
        var filterContext = Context("Administrador");

        await BuildFilters(controller, action, verb, permissions.Object, filterContext);

        Assert.Null(filterContext.Result);
        Assert.True(await new NavigationMenuService(permissions.Object).CanSubmitAsync("Administrador", controller, action));
    }

    [Fact]
    public void ReadingActionsKeepTheModuleLevelAccess()
    {
        // Las pantallas de consulta no se endurecen: siguen abriéndose con el permiso del módulo.
        foreach (var (controller, action) in new[] { ("Clients", "Index"), ("Consultations", "Index"), ("Credits", "Index"), ("Inventory", "Index") })
        {
            var access = NavigationMenu.GetScreenAccess(controller, action);
            Assert.All(access.Permissions, permission => Assert.True(string.IsNullOrWhiteSpace(permission.Code)));
        }
    }

    [Fact]
    public void Migration0313_AddsEliminarAndKeepsTheAdministratorsCapabilities()
    {
        var sql = File.ReadAllText(SourcePath("database", "migrations", "0313_permisos_mutacion_granulares.sql"));

        Assert.Contains("INVENTARIO_ELIMINAR", sql, StringComparison.Ordinal);
        Assert.Contains("perfil.Nombre = N'Administrador'", sql, StringComparison.Ordinal);
        // Solo conserva lo que ya hacen hoy dos perfiles; no reparte mutaciones a los demás.
        Assert.Contains("CREDITOS_MOVIMIENTOS", sql, StringComparison.Ordinal);
        Assert.Contains("EMPLEADOS_TAREAS", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
    }

    // Ejecuta los filtros reales de la acción: el del controlador (módulo) y el del método (permiso de la acción).
    private static async Task BuildFilters(string controller, string action, string verb, IRolePermissionService permissions, AuthorizationFilterContext context)
    {
        var type = typeof(NavigationMenu).Assembly.GetTypes().Single(t => t.Name == controller + "Controller");
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == action)
            .Single(m =>
            {
                var verbs = m.GetCustomAttributes(true).OfType<IActionHttpMethodProvider>().ToList();
                return verbs.Count == 0 || verbs.Any(v => v.HttpMethods.Contains(verb, StringComparer.OrdinalIgnoreCase));
            });

        foreach (var attribute in type.GetCustomAttributes(true).Concat(method.GetCustomAttributes(true)).OfType<AdminAuthorizeAttribute>())
        {
            var filter = new AdminAuthorizeFilter((string)attribute.Arguments![0], (string)attribute.Arguments[1], permissions);
            await filter.OnAuthorizationAsync(context);
            if (context.Result is not null) return;
        }
    }

    private static AuthorizationFilterContext Context(string role)
    {
        var httpContext = new DefaultHttpContext { Session = new FakeSession() };
        httpContext.Session.SetInt32("UserId", 5);
        httpContext.Session.SetString("UserEmail", "qa@example.test");
        httpContext.Session.SetString("UserRole", role);
        return new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>());
    }

    private static string SourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
        return Path.Combine([root, .. parts]);
    }

    private sealed class FakeSession : ISession
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
