using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>Menú web según permisos: cada perfil ve las pantallas que su autorización le deja abrir.</summary>
public sealed class NavigationMenuTests
{
    // Permisos por perfil en la base demo con 0200–0265 y la 0251 (código:módulo de dbo.Permisos).
    private static readonly Dictionary<string, string> Profiles = new()
    {
        ["Empleado"] = "CONSULTAS_ATENDER:Consultas,CONSULTAS_VER:Consultas,DASHBOARD_VER:Dashboard,FACTURACION_VER:Facturación,"
            + "INVENTARIO_MOVIMIENTOS:Inventario,INVENTARIO_VER:Inventario,MOVIL_ACCESO:Movil,PEDIDOS_CAMBIAR_ESTADO:Pedidos,PEDIDOS_VER:Pedidos,"
            + "RRHH_JORNADAS_REGISTRAR:RRHH,RRHH_JORNADAS_VER_PROPIAS:RRHH,VENTA_MOVIL_CREAR_PEDIDO:Venta móvil,VENTA_MOVIL_OFFLINE_SYNC:Venta móvil",
        ["Vendedor"] = "MOVIL_ACCESO:Movil,RRHH_JORNADAS_REGISTRAR:RRHH,RRHH_JORNADAS_VER_PROPIAS:RRHH,"
            + "VENTA_MOVIL_CREAR_PEDIDO:Venta móvil,VENTA_MOVIL_OFFLINE_SYNC:Venta móvil",
        ["Gerente"] = "ACTIVOS_GESTIONAR:Activos,ASISTENTE_METRICAS:Asistente,COMODATOS_CONSULTAR:Activos,CUARENTENA_GESTIONAR:Inventario,"
            + "DEVOLUCIONES_GESTIONAR:Inventario,ENTREGAS_EVIDENCIA_VER:Entregas,ENTREGAS_TABLERO_VER:Entregas,FINANZAS_INGRESOS_EGRESOS_VER:Finanzas,"
            + "FLOTA_MANTENIMIENTO:Flota,GASTOS_VER:Gastos,INVENTARIO_VER:Inventario,LIQUIDACION_FINANCIERA:Finanzas,METAS_GESTIONAR:Metas y KPIs,"
            + "MOVIL_ACCESO:Movil,PEDIDOS_AUTORIZAR_RECHAZAR:Pedidos,PEDIDOS_VER:Pedidos,PRESUPUESTOS_APROBAR:Presupuestos,"
            + "PRESUPUESTOS_COMPARAR:Presupuestos,PRESUPUESTOS_VER:Presupuestos,PROMOCIONES_GESTIONAR:Promociones,"
            + "RECLAMOS_GESTIONAR:Servicio al cliente,REPORTE_KPI:Metas y KPIs,REPORTES_DASHBOARD:Reportes,REPORTES_VENDEDORES_VER:Reportes,"
            + "REPORTES_VENTAS_VER:Reportes,RRHH_JORNADAS_REGISTRAR:RRHH,RRHH_JORNADAS_VER_PROPIAS:RRHH,RUTAS_GESTIONAR:Rutas",
        ["Chofer"] = "ENTREGAS_ACTUALIZAR:Entregas,ENTREGAS_EVIDENCIA_REGISTRAR:Entregas,FLOTA_KILOMETRAJE:Flota,FLOTA_KILOMETRAJE_PROPIO:Flota,"
            + "MOVIL_ACCESO:Movil,RRHH_JORNADAS_REGISTRAR:RRHH,RRHH_JORNADAS_VER_PROPIAS:RRHH",
        ["Supervisor"] = "EMPLEADOS_CREAR:Empleados,EMPLEADOS_EDITAR:Empleados,EMPLEADOS_VER:Empleados,MOVIL_ACCESO:Movil,"
            + "PLANILLA_BOLETAS_GESTIONAR:Planilla,RRHH_JORNADAS_APROBAR:RRHH,RRHH_JORNADAS_REGISTRAR:RRHH,RRHH_JORNADAS_VER_PROPIAS:RRHH",
        ["Cajero"] = "FINANZAS_INGRESOS_EGRESOS_VER:Finanzas,LIQUIDACION_FINANCIERA:Finanzas,MOVIL_ACCESO:Movil,RRHH_JORNADAS_REGISTRAR:RRHH,RRHH_JORNADAS_VER_PROPIAS:RRHH",
        ["Bodeguero"] = "COMPRAS_ORDENES_RECIBIR:Compras,COMPRAS_ORDENES_VER:Compras,INVENTARIO_MOVIMIENTOS:Inventario,INVENTARIO_VER:Inventario,"
            + "MOVIL_ACCESO:Movil,PEDIDOS_PREPARAR:Pedidos,PEDIDOS_VER:Pedidos,RRHH_JORNADAS_REGISTRAR:RRHH,RRHH_JORNADAS_VER_PROPIAS:RRHH",
        ["Facturador"] = "FACTURACION_VER:Facturación,MOVIL_ACCESO:Movil,PEDIDOS_VER:Pedidos,RRHH_JORNADAS_REGISTRAR:RRHH,RRHH_JORNADAS_VER_PROPIAS:RRHH",
        ["Crédito y Cobro"] = "CLIENTES_VER:Clientes,CREDITOS_VER:Créditos,MOVIL_ACCESO:Movil,RRHH_JORNADAS_REGISTRAR:RRHH,RRHH_JORNADAS_VER_PROPIAS:RRHH",
        ["Compras"] = "COMPRAS_ORDENES_VER:Compras,COMPRAS_SUGERENCIAS_VER:Compras,INVENTARIO_VER:Inventario,MOVIL_ACCESO:Movil,"
            + "PROVEEDORES_VER:Compras,RRHH_JORNADAS_REGISTRAR:RRHH,RRHH_JORNADAS_VER_PROPIAS:RRHH",
        ["Soporte"] = "CONSULTAS_ATENDER:Consultas,CONSULTAS_VER:Consultas,MOVIL_ACCESO:Movil,RRHH_JORNADAS_REGISTRAR:RRHH,RRHH_JORNADAS_VER_PROPIAS:RRHH",
        ["Auditor Interno"] = "AUDITORIA_VER:Auditoría,COMODATOS_CONSULTAR:Activos,MOVIL_ACCESO:Movil,RRHH_JORNADAS_REGISTRAR:RRHH,RRHH_JORNADAS_VER_PROPIAS:RRHH",
        ["Cliente"] = "",
    };

    public static TheoryData<string, string> ExpectedPanels => new()
    {
        { "Empleado", "Inventario/Inventario|Inventario/Movimientos|Ventas y Pedidos/Pedidos|Ventas y Pedidos/Facturación|Reportes y Gerencia/Dashboard|Clientes/Consultas" },
        { "Vendedor", "" },
        { "Gerente", "Inventario/Inventario|Inventario/Movimientos|Inventario/Devoluciones|Inventario/Cuarentena|Inventario/Activos"
            + "|Ventas y Pedidos/Pedidos|Ventas y Pedidos/Promociones"
            + "|Logística y Flota/Rutas y Entregas|Logística y Flota/Tablero de entregas|Logística y Flota/Vehículos|Logística y Flota/Mantenimiento"
            + "|Logística y Flota/Alertas de flota|Logística y Flota/Comodatos"
            + "|Finanzas/Liquidación de cobros|Finanzas/Presupuestos anuales|Finanzas/Gastos operativos|Finanzas/Presupuesto vs. real|Finanzas/Ingresos y Egresos"
            + "|Reportes y Gerencia/Tablero gerencial|Reportes y Gerencia/Reporte de ventas|Reportes y Gerencia/Desempeño de vendedores"
            + "|Reportes y Gerencia/Metas de ventas|Reportes y Gerencia/Reporte de KPIs|Clientes/Reclamos" },
        { "Chofer", "Logística y Flota/Kilometraje" },
        { "Supervisor", "Personal/Empleados|Personal/Solicitudes (licencias)|Personal/Aprobar jornadas|Personal/Boletas de pago" },
        { "Cajero", "Finanzas/Liquidación de cobros|Finanzas/Ingresos y Egresos" },
        { "Bodeguero", "Inventario/Inventario|Inventario/Movimientos|Ventas y Pedidos/Pedidos|Finanzas/Órdenes de compra" },
        { "Facturador", "Ventas y Pedidos/Pedidos|Ventas y Pedidos/Facturación" },
        { "Crédito y Cobro", "Ventas y Pedidos/Cuentas por Cobrar|Clientes/Clientes|Clientes/Créditos" },
        { "Compras", "Inventario/Inventario|Inventario/Movimientos|Finanzas/Proveedores|Finanzas/Órdenes de compra" },
        { "Soporte", "Clientes/Consultas" },
        { "Auditor Interno", "Seguridad/Auditoría" },
        { "Cliente", "" },
    };

    [Theory]
    [MemberData(nameof(ExpectedPanels))]
    public async Task CadaPerfilVeSoloLasPantallasQuePuedeAbrir(string role, string expected)
    {
        var service = new NavigationMenuService(new FakePermissions(Profiles[role]));

        var panel = await service.GetAdminPanelAsync(role);

        var visible = panel.SelectMany(s => s.Links.Select(l => $"{s.Label}/{l.Label}"));
        Assert.Equal(expected, string.Join("|", visible));
    }

    [Fact]
    public async Task ElAdministradorConservaTodoElPanel()
    {
        var service = new NavigationMenuService(new FakePermissions(""));

        var panel = await service.GetAdminPanelAsync("Administrador");

        Assert.Equal(NavigationMenu.AdminPanel.Select(s => s.Label), panel.Select(s => s.Label));
        Assert.Equal(NavigationMenu.AdminPanel.Sum(s => s.Links.Count), panel.Sum(s => s.Links.Count));
        Assert.Equal(49, panel.Sum(s => s.Links.Count));
    }

    [Fact]
    public async Task SinPermisoNoAparecenLosBotonesQueLlevabanAAccesoDenegado()
    {
        var supervisor = await new NavigationMenuService(new FakePermissions(Profiles["Supervisor"])).GetAdminPanelAsync("Supervisor");
        var compras = await new NavigationMenuService(new FakePermissions(Profiles["Compras"])).GetAdminPanelAsync("Compras");

        Assert.DoesNotContain(supervisor.SelectMany(s => s.Links), l => l.Controller == "Payroll");
        Assert.DoesNotContain(compras.SelectMany(s => s.Links), l => l.Controller == "PriceHistory");
    }

    [Theory]
    [InlineData("Kpis", "Report", "METAS_GESTIONAR", "REPORTE_KPI")]
    [InlineData("Returns", "Quarantine", "DEVOLUCIONES_GESTIONAR", "CUARENTENA_GESTIONAR")]
    [InlineData("Employees", "Index", "", "EMPLEADOS_VER")]
    public void LaExigenciaSeLeeDelControladorYDeLaAccion(string controller, string action, string classCode, string actionCode)
    {
        var access = NavigationMenu.GetScreenAccess(controller, action);

        Assert.Equal(new[] { classCode, actionCode }, access.Permissions.Select(p => p.Code));
    }

    [Fact]
    public async Task UnaPantallaConRolesFijosNoSeMuestraAOtroRol()
    {
        var service = new NavigationMenuService(new FakePermissions(Profiles["Gerente"]));

        Assert.False(await service.CanOpenAsync("Gerente", "EmployeePortal", "Index"));
        Assert.True(await service.CanOpenAsync("Vendedor", "EmployeePortal", "Index"));
        Assert.False(await service.CanOpenAsync(null, "Inventory", "Index"));
    }

    [Fact]
    public void TodasLasOpcionesDelPanelLlevanAUnaPantallaProtegidaExistente()
    {
        foreach (var link in NavigationMenu.AdminPanel.SelectMany(s => s.Links))
        {
            var access = NavigationMenu.GetScreenAccess(link.Controller, link.Action);
            Assert.True(access.Permissions.Count > 0 || NavigationMenu.IsPanelOnlyForAdministrator(link),
                $"{link.Controller}.{link.Action} no exige permiso administrativo.");
        }
    }

    [Fact]
    public void LosAccesosDirectosDelLayoutConsultanLaMismaPantallaQueAbren()
    {
        var layout = MobileCorreccionesTests.Read("Proyecto_Final", "Views", "Shared", "_Layout.cshtml");
        var checks = Regex.Matches(layout,
            @"MenuAccess\.CanOpenAsync\(userRole, ""(\w+)"", ""(\w+)""\)\)\s*\{\s*<a asp-controller=""(\w+)"" asp-action=""(\w+)""");

        Assert.Equal(Regex.Matches(layout, @"MenuAccess\.CanOpenAsync\(").Count, checks.Count);
        Assert.True(checks.Count >= 8);
        foreach (Match check in checks)
        {
            Assert.Equal(check.Groups[1].Value, check.Groups[3].Value);
            Assert.Equal(check.Groups[2].Value, check.Groups[4].Value);
            NavigationMenu.GetScreenAccess(check.Groups[1].Value, check.Groups[2].Value);
        }

        // El panel ya no depende del rol Administrador ni de menús fijos por puesto.
        Assert.DoesNotContain("financeMenu", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("isPurchasing", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("isSupervisor", layout, StringComparison.Ordinal);
    }

    /// <summary>Imita AdminDbService: código exacto, o módulo por nombre del módulo o prefijo del código.</summary>
    private sealed class FakePermissions(string granted) : IRolePermissionService
    {
        private readonly List<(string Code, string Module)> _granted = granted
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split(':'))
            .Select(p => (p[0], p[1]))
            .ToList();

        public Task<bool> HasCodePermissionAsync(string roleName, string permissionCode) =>
            Task.FromResult(_granted.Any(g => g.Code == permissionCode));

        public Task<bool> HasModulePermissionAsync(string? roleName, string module)
        {
            var key = Normalize(module);
            var aliases = key is "admin" or "dashboard" ? new[] { "admin", "dashboard" } : new[] { key };
            var prefixes = key switch
            {
                "admin" or "dashboard" => new[] { "DASHBOARD_" },
                "seguridad" => new[] { "ROLES_", "PERMISOS_" },
                "facturacion" or "auditoria" or "creditos" or "clientes" or "consultas" or "inventario" or "pedidos"
                    => new[] { key.ToUpperInvariant() + "_" },
                _ => Array.Empty<string>(),
            };
            return Task.FromResult(_granted.Any(g =>
                aliases.Contains(Normalize(g.Module)) || prefixes.Any(p => g.Code.StartsWith(p, StringComparison.Ordinal))));
        }

        private static string Normalize(string value) =>
            new string(value.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray()).Trim().ToLowerInvariant();
    }
}
