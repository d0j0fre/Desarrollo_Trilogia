using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Proyecto_Final.Filters;

namespace Proyecto_Final.Services;

public sealed record NavigationMenuLink(string Controller, string Action, string Icon, string Label);

public sealed record NavigationMenuSection(string Label, string Icon, IReadOnlyList<NavigationMenuLink> Links);

/// <summary>Una exigencia de [AdminAuthorize(modulo, codigo)] tal como la evalúa AdminAuthorizeFilter.</summary>
public sealed record ScreenPermission(string Module, string Code);

/// <summary>Lo que exige de verdad una pantalla: los atributos del controlador y de la acción GET.</summary>
public sealed record ScreenAccess(IReadOnlyList<IReadOnlyList<string>> RoleLists, IReadOnlyList<ScreenPermission> Permissions);

/// <summary>
/// Opciones del panel «Administración». El permiso de cada opción no se escribe aquí:
/// se lee de los atributos de la pantalla destino, así el menú no puede mostrar
/// un botón que termine en «Acceso denegado».
/// </summary>
public static class NavigationMenu
{
    public static readonly IReadOnlyList<NavigationMenuSection> AdminPanel =
    [
        new("Inventario", "fa-boxes",
        [
            new("Inventory", "Index", "fa-boxes", "Inventario"),
            new("Inventory", "Movements", "fa-exchange-alt", "Movimientos"),
            new("Inventory", "TransformStock", "fa-recycle", "Transformar producto"),
            new("Combos", "Index", "fa-box-open", "Combos"),
            new("Returns", "Index", "fa-undo", "Devoluciones"),
            new("Returns", "Quarantine", "fa-biohazard", "Cuarentena"),
            new("Assets", "Index", "fa-snowflake", "Activos"),
        ]),
        new("Ventas y Pedidos", "fa-shopping-bag",
        [
            new("OrdersAdmin", "Index", "fa-clipboard-list", "Pedidos"),
            new("Billing", "Index", "fa-file-invoice-dollar", "Facturación"),
            new("AccountsReceivableAdmin", "Index", "fa-receipt", "Cuentas por Cobrar"),
            new("Promotions", "Index", "fa-tags", "Promociones"),
            new("WarrantyRequestsAdmin", "Index", "fa-shield-alt", "Solicitudes de Garantía"),
        ]),
        new("Logística y Flota", "fa-truck",
        [
            new("RoutesAdmin", "Index", "fa-route", "Rutas y Entregas"),
            new("DeliveryBoard", "Index", "fa-th-large", "Tablero de entregas"),
            new("Vehicles", "Index", "fa-truck", "Vehículos"),
            new("Fleet", "Mileage", "fa-tachometer-alt", "Kilometraje"),
            new("Fleet", "Maintenance", "fa-wrench", "Mantenimiento"),
            new("Fleet", "Alerts", "fa-bell", "Alertas de flota"),
            new("Comodatos", "Index", "fa-handshake", "Comodatos"),
            new("Comodatos", "Profitability", "fa-chart-pie", "Rentabilidad de comodatos"),
            new("MobileApp", "Index", "fa-mobile-alt", "Aplicación móvil"),
        ]),
        new("Finanzas", "fa-coins",
        [
            new("Finance", "Index", "fa-cash-register", "Liquidación de cobros"),
            new("Budgets", "Index", "fa-wallet", "Presupuestos anuales"),
            new("Expenses", "Index", "fa-receipt", "Gastos operativos"),
            new("BudgetComparison", "Index", "fa-chart-bar", "Presupuesto vs. real"),
            new("FinancialReportAdmin", "Index", "fa-chart-line", "Ingresos y Egresos"),
            new("Suppliers", "Index", "fa-address-book", "Proveedores"),
            new("PurchaseOrders", "Index", "fa-shopping-cart", "Órdenes de compra"),
            new("PriceHistory", "Index", "fa-chart-area", "Histórico de precios"),
        ]),
        new("Reportes y Gerencia", "fa-chart-line",
        [
            new("Admin", "Index", "fa-tachometer-alt", "Dashboard"),
            new("ManagementDashboard", "Index", "fa-chart-line", "Tablero gerencial"),
            new("SalesReports", "Index", "fa-file-alt", "Reporte de ventas"),
            new("SellerPerformance", "Index", "fa-user-chart", "Desempeño de vendedores"),
            new("Kpis", "Index", "fa-bullseye", "Metas de ventas"),
            new("Kpis", "Report", "fa-chart-bar", "Reporte de KPIs"),
            new("InventoryIntelligence", "Index", "fa-brain", "Inteligencia de inventario"),
        ]),
        new("Clientes", "fa-users",
        [
            new("Clients", "Index", "fa-users", "Clientes"),
            new("Credits", "Index", "fa-credit-card", "Créditos"),
            new("Reclamos", "Index", "fa-exclamation-circle", "Reclamos"),
            new("Consultations", "Index", "fa-comments", "Consultas"),
        ]),
        new("Personal", "fa-user-tie",
        [
            new("Employees", "Index", "fa-id-badge", "Empleados"),
            new("Employees", "LeaveRequests", "fa-calendar-check", "Solicitudes (licencias)"),
            new("Attendance", "Index", "fa-clock", "Aprobar jornadas"),
            new("Payroll", "Index", "fa-calculator", "Planilla"),
            new("PaySlips", "Index", "fa-file-invoice-dollar", "Boletas de pago"),
        ]),
        new("Seguridad", "fa-shield-alt",
        [
            new("Roles", "Index", "fa-user-shield", "Roles"),
            new("Permissions", "Index", "fa-key", "Permisos"),
            new("Documents", "Index", "fa-folder-open", "Documentos digitales"),
            new("Documents", "Alerts", "fa-bell", "Alertas documentales"),
            new("Audit", "Index", "fa-history", "Auditoría"),
        ]),
    ];

    /// <summary>
    /// Aplicación móvil la abre cualquier usuario del personal y ya tiene su botón propio en
    /// la barra; dentro del panel solo la ve el Administrador, que es quien publica versiones.
    /// </summary>
    public static bool IsPanelOnlyForAdministrator(NavigationMenuLink link) =>
        link.Controller == "MobileApp" && link.Action == "Index";

    private static readonly ConcurrentDictionary<string, ScreenAccess> AccessCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Lee [SessionAuthorize] y [AdminAuthorize] del controlador y de su acción GET (o POST, para un botón de formulario).</summary>
    public static ScreenAccess GetScreenAccess(string controller, string action, string httpMethod = "GET") =>
        AccessCache.GetOrAdd($"{httpMethod} {controller}/{action}", _ => ReadScreenAccess(controller, action, httpMethod));

    private static ScreenAccess ReadScreenAccess(string controller, string action, string httpMethod)
    {
        var controllerType = typeof(NavigationMenu).Assembly.GetTypes()
            .SingleOrDefault(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract
                                  && t.Name == controller + "Controller")
            ?? throw new InvalidOperationException($"No existe el controlador {controller}.");

        var method = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == action && m.GetCustomAttribute<NonActionAttribute>() is null)
            .SingleOrDefault(m => Accepts(m, httpMethod))
            ?? throw new InvalidOperationException($"{controller}.{action} no tiene una acción {httpMethod}.");

        var attributes = controllerType.GetCustomAttributes(true).Concat(method.GetCustomAttributes(true)).ToList();
        var roleLists = attributes.OfType<SessionAuthorizeAttribute>()
            .Where(a => a.AllowedRoles.Count > 0)
            .Select(a => a.AllowedRoles)
            .ToList();
        var permissions = attributes.OfType<AdminAuthorizeAttribute>()
            .Select(a => new ScreenPermission((string)a.Arguments![0], (string)a.Arguments[1]))
            .ToList();
        return new ScreenAccess(roleLists, permissions);
    }

    private static bool Accepts(MethodInfo method, string httpMethod)
    {
        var verbs = method.GetCustomAttributes(true).OfType<IActionHttpMethodProvider>().ToList();
        return verbs.Count == 0 || verbs.Any(v => v.HttpMethods.Contains(httpMethod, StringComparer.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Decide qué opciones del menú ve el usuario con la misma regla que SessionAuthorizeFilter y
/// AdminAuthorizeFilter, consultando IRolePermissionService como el menú de Finanzas.
/// Es Scoped: cada consulta de permiso se hace una sola vez por solicitud.
/// </summary>
public sealed class NavigationMenuService(IRolePermissionService permissionService)
{
    private readonly Dictionary<string, bool> _checks = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> CanOpenAsync(string? role, string controller, string action) =>
        CanAsync(role, controller, action, "GET");

    /// <summary>Si el rol puede enviar el formulario de una acción POST (botones como «Generar factura»).</summary>
    public Task<bool> CanSubmitAsync(string? role, string controller, string action) =>
        CanAsync(role, controller, action, "POST");

    private async Task<bool> CanAsync(string? role, string controller, string action, string httpMethod)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        var access = NavigationMenu.GetScreenAccess(controller, action, httpMethod);
        if (access.RoleLists.Any(roles => !roles.Contains(role, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var permission in access.Permissions)
        {
            if (!await HasPermissionAsync(role, permission))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Secciones del panel con solo las opciones que el rol puede abrir; las vacías no se devuelven.</summary>
    public async Task<IReadOnlyList<NavigationMenuSection>> GetAdminPanelAsync(string? role)
    {
        var isAdmin = string.Equals(role, "Administrador", StringComparison.OrdinalIgnoreCase);
        var sections = new List<NavigationMenuSection>();
        foreach (var section in NavigationMenu.AdminPanel)
        {
            var links = new List<NavigationMenuLink>();
            foreach (var link in section.Links)
            {
                if ((isAdmin || !NavigationMenu.IsPanelOnlyForAdministrator(link))
                    && await CanOpenAsync(role, link.Controller, link.Action))
                {
                    links.Add(link);
                }
            }

            if (links.Count > 0)
            {
                sections.Add(section with { Links = links });
            }
        }

        return sections;
    }

    private async Task<bool> HasPermissionAsync(string role, ScreenPermission permission)
    {
        var key = $"{role}|{permission.Module}|{permission.Code}";
        if (!_checks.TryGetValue(key, out var allowed))
        {
            allowed = string.IsNullOrWhiteSpace(permission.Code)
                ? await permissionService.HasModulePermissionAsync(role, permission.Module)
                : await permissionService.HasCodePermissionAsync(role, permission.Code);
            _checks[key] = allowed;
        }

        return allowed;
    }
}
