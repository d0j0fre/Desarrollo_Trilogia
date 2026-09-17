using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_FinalAPI.Authorization;
using Proyecto_FinalAPI.Controllers;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services.Mobile;

namespace Proyecto_Final.Tests;

/// <summary>
/// Aplicación móvil por rol: bodega, gestión, ventas, personal y oficina.
///
/// Fija las reglas que no pueden aflojarse con el tiempo: todo endpoint exige
/// acceso y su propio permiso, la identidad sale del token, las escrituras que
/// reintenta la cola exigen identificador, y la pantalla de inicio de cada
/// perfil muestra exactamente lo que sus permisos le dejan hacer.
/// </summary>
public sealed class MobileRoleSurfaceTests
{
    private static readonly Type[] RoleControllers =
    [
        typeof(MobileInventoryController),
        typeof(MobileOrdersController),
        typeof(MobileSalesController),
        typeof(MobileManagementController),
        typeof(MobileStaffController),
        typeof(MobileOfficeController)
    ];

    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(true).Any());

    // ── Protección de la superficie ─────────────────────────────────────────

    [Fact]
    public void CadaControladorExigeAutenticacionYAccesoMovil()
    {
        foreach (var controller in RoleControllers)
        {
            Assert.True(controller.GetCustomAttributes<AuthorizeAttribute>(true).Any(), $"{controller.Name} no exige autenticación.");

            var access = controller.GetCustomAttributes<RequirePermissionAttribute>(true).SingleOrDefault();
            Assert.NotNull(access);
            Assert.Equal(MobilePermissions.Access, (string)access!.Arguments![0]);
        }
    }

    [Fact]
    public void CadaEndpointExigeUnPermisoPropioAdemasDelAcceso()
    {
        // El acceso móvil solo dice "puede abrir la aplicación". Sin permiso por
        // endpoint, cualquier perfil del personal vería la bitácora o aprobaría
        // pedidos retenidos.
        var unguarded = RoleControllers
            .SelectMany(Actions)
            .Where(action => action.GetCustomAttribute<RequirePermissionAttribute>() is null)
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}")
            .ToArray();

        Assert.Empty(unguarded);
    }

    [Fact]
    public void CadaEndpointTieneLimiteDePeticiones()
    {
        var unlimited = RoleControllers
            .SelectMany(Actions)
            .Where(action => !action.GetCustomAttributes<EnableRateLimitingAttribute>(true).Any())
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}")
            .ToArray();

        Assert.Empty(unlimited);
    }

    [Fact]
    public void NingunEndpointRecibeElIdentificadorDeQuienActua()
    {
        var offenders = RoleControllers
            .SelectMany(Actions)
            .SelectMany(action => action.GetParameters().Select(parameter => new { action, parameter }))
            .Where(entry => entry.parameter.Name is not null &&
                (entry.parameter.Name.Contains("usuarioId", StringComparison.OrdinalIgnoreCase) ||
                 entry.parameter.Name.Contains("userId", StringComparison.OrdinalIgnoreCase) ||
                 entry.parameter.Name.Contains("vendedor", StringComparison.OrdinalIgnoreCase)))
            .Select(entry => $"{entry.action.DeclaringType!.Name}.{entry.action.Name}({entry.parameter.Name})")
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void NingunContratoDeEntradaLlevaLaIdentidadDeQuienActua()
    {
        var requestTypes = typeof(RegisterMovementRequest).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(RegisterMovementRequest).Namespace && type.Name.EndsWith("Request", StringComparison.Ordinal))
            .Where(type => type != typeof(LoginApiRequest))
            .ToArray();

        Assert.Contains(typeof(CreateSaleRequest), requestTypes);

        var offenders = requestTypes
            .SelectMany(type => type.GetProperties().Select(property => new { type, property }))
            .Where(entry =>
                entry.property.Name.Contains("Usuario", StringComparison.OrdinalIgnoreCase) ||
                entry.property.Name.Contains("Vendedor", StringComparison.OrdinalIgnoreCase) ||
                entry.property.Name.Contains("Supervisor", StringComparison.OrdinalIgnoreCase))
            .Select(entry => $"{entry.type.Name}.{entry.property.Name}")
            .ToArray();

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData(nameof(MobileInventoryController.RegisterMovement), typeof(MobileInventoryController), MobilePermissions.InventoryMovements)]
    [InlineData(nameof(MobileInventoryController.ChangeProductStatus), typeof(MobileInventoryController), MobilePermissions.ProductsEdit)]
    [InlineData(nameof(MobileInventoryController.ReceiveLine), typeof(MobileInventoryController), MobilePermissions.ReceivePurchases)]
    [InlineData(nameof(MobileOrdersController.ChangeStatus), typeof(MobileOrdersController), MobilePermissions.OrdersChangeStatus)]
    [InlineData(nameof(MobileOrdersController.MarkPrepared), typeof(MobileOrdersController), MobilePermissions.PrepareOrders)]
    [InlineData(nameof(MobileOrdersController.Approve), typeof(MobileOrdersController), MobilePermissions.AuthorizeOrders)]
    [InlineData(nameof(MobileOrdersController.Reject), typeof(MobileOrdersController), MobilePermissions.AuthorizeOrders)]
    [InlineData(nameof(MobileSalesController.Create), typeof(MobileSalesController), MobilePermissions.SellerOrders)]
    [InlineData(nameof(MobileManagementController.Reassign), typeof(MobileManagementController), MobilePermissions.ManageRoutes)]
    [InlineData(nameof(MobileManagementController.Dispatch), typeof(MobileManagementController), MobilePermissions.ManageRoutes)]
    [InlineData(nameof(MobileManagementController.Dashboard), typeof(MobileManagementController), MobilePermissions.Dashboard)]
    [InlineData(nameof(MobileStaffController.ResolveAttendance), typeof(MobileStaffController), MobilePermissions.AttendanceApprove)]
    [InlineData(nameof(MobileOfficeController.Audit), typeof(MobileOfficeController), MobilePermissions.AuditView)]
    [InlineData(nameof(MobileOfficeController.UpdateConsultation), typeof(MobileOfficeController), MobilePermissions.ConsultationsAttend)]
    public void LasAccionesSensiblesExigenElPermisoDelModuloWeb(string action, Type controller, string permission)
    {
        var method = controller.GetMethod(action)!;
        var attribute = method.GetCustomAttribute<RequirePermissionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(permission, (string)attribute!.Arguments![0]);
    }

    // ── Pantalla de inicio por perfil ───────────────────────────────────────

    private static string[] EnabledModules(string role, params string[] permissions) =>
        MobileModuleCatalog.For(role, permissions).Where(module => module.Enabled).Select(module => module.Key).ToArray();

    [Fact]
    public void ElChoferVeSuOperacionYNoLaDeOtrosPerfiles()
    {
        var modules = EnabledModules("Chofer",
            MobilePermissions.Access, MobilePermissions.OwnMileage, MobilePermissions.AttendanceRegister, "ENTREGAS_ACTUALIZAR");

        Assert.Equal(new[] { "driver.routes", "driver.mileage", "driver.summary", "staff.attendance" }, modules);
    }

    [Fact]
    public void BodegaVeInventarioRecepcionYPreparacion()
    {
        var modules = EnabledModules("Bodeguero",
            MobilePermissions.Access, MobilePermissions.InventoryView, MobilePermissions.InventoryMovements,
            MobilePermissions.PurchaseOrdersView, MobilePermissions.ReceivePurchases, MobilePermissions.OrdersView,
            MobilePermissions.PrepareOrders, MobilePermissions.AttendanceRegister);

        Assert.Contains("warehouse.picking", modules);
        Assert.Contains("warehouse.receiving", modules);
        Assert.Contains("warehouse.movements", modules);
        Assert.Contains("warehouse.stock", modules);
        Assert.DoesNotContain("driver.routes", modules);
        Assert.DoesNotContain("management.approvals", modules);
        Assert.DoesNotContain("management.products", modules);
        // Sin duplicados: pedidos y compras ya están en preparación y recepción.
        Assert.DoesNotContain("management.orders", modules);
        Assert.DoesNotContain("office.purchasing", modules);
    }

    [Fact]
    public void ElAdministradorVeTodaLaGestionPeroNoLasRutasDeChofer()
    {
        // Un administrador no tiene rutas propias: verlas vacías solo confunde.
        var modules = EnabledModules("Administrador");

        Assert.Contains("management.dashboard", modules);
        Assert.Contains("management.routes", modules);
        Assert.Contains("management.orders", modules);
        Assert.Contains("management.approvals", modules);
        Assert.Contains("management.products", modules);
        Assert.Contains("warehouse.stock", modules);
        Assert.Contains("office.audit", modules);
        Assert.DoesNotContain("driver.routes", modules);
        Assert.DoesNotContain("driver.mileage", modules);
    }

    [Theory]
    [InlineData("Supervisor", MobilePermissions.AttendanceApprove, "staff.approvals")]
    [InlineData("Cajero", MobilePermissions.Settlements, "office.settlements")]
    [InlineData("Facturador", MobilePermissions.InvoicesView, "office.invoices")]
    [InlineData("Crédito y Cobro", MobilePermissions.CreditView, "office.credit")]
    [InlineData("Compras", MobilePermissions.PurchaseOrdersView, "office.purchasing")]
    [InlineData("Soporte", MobilePermissions.ConsultationsView, "office.support")]
    [InlineData("Auditor Interno", MobilePermissions.AuditView, "office.audit")]
    [InlineData("Vendedor", MobilePermissions.SellerOrders, "sales.orders")]
    [InlineData("Gerente", MobilePermissions.Dashboard, "management.dashboard")]
    public void CadaPerfilDeOficinaTieneSuSeccion(string role, string permission, string expectedModule)
    {
        var modules = EnabledModules(role, MobilePermissions.Access, permission);

        Assert.Equal(new[] { expectedModule }, modules);
    }

    [Fact]
    public void UnPerfilSinPermisosNoVeNingunModulo()
    {
        Assert.Empty(EnabledModules("Cliente"));
    }

    [Fact]
    public void TodoModuloTieneSeccionDescripcionEIcono()
    {
        foreach (var module in MobileModuleCatalog.For("Administrador", Array.Empty<string>()))
        {
            Assert.False(string.IsNullOrWhiteSpace(module.Section), module.Key);
            Assert.False(string.IsNullOrWhiteSpace(module.Description), module.Key);
            Assert.False(string.IsNullOrWhiteSpace(module.Icon), module.Key);
        }
    }

    // ── Reglas de estado de pedidos ─────────────────────────────────────────

    [Theory]
    [InlineData("Pendiente", false, "Aprobado,Cancelado")]
    [InlineData("Aprobado", false, "EnProceso,Cancelado")]
    [InlineData("EnProceso", false, "Entregado,Cancelado")]
    [InlineData("Liberado", false, "EnProceso,Cancelado")]
    [InlineData("Pendiente", true, "Entregado")]
    [InlineData("Entregado", true, "")]
    [InlineData("Cancelado", false, "")]
    [InlineData("Rechazado", false, "")]
    [InlineData("Retenido", false, "")]
    public void LasTransicionesEspejanASpAdminUpdateOrderStatus(string current, bool invoiced, string expected)
    {
        var allowed = string.Join(",", OrderStatusRules.AllowedFrom(current, invoiced));
        Assert.Equal(expected, allowed);
    }

    // ── Validaciones antes de tocar la base ─────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task UnMovimientoSinSyncGuidValido_NoLlegaALaBase(string syncGuid)
    {
        var db = new Mock<IInventoryMobileDbService>(MockBehavior.Strict);
        var controller = WithUser(new MobileInventoryController(db.Object, Mock.Of<IMobileAuditService>(),
            NullLogger<MobileInventoryController>.Instance));

        var result = await controller.RegisterMovement(
            new RegisterMovementRequest { ProductoId = 2, TipoMovimiento = "Entrada", Cantidad = 3, SyncGuid = syncGuid },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnTipoDeMovimientoDesconocido_SeRechaza()
    {
        var db = new Mock<IInventoryMobileDbService>(MockBehavior.Strict);
        var controller = WithUser(new MobileInventoryController(db.Object, Mock.Of<IMobileAuditService>(),
            NullLogger<MobileInventoryController>.Instance));

        var result = await controller.RegisterMovement(
            new RegisterMovementRequest { ProductoId = 2, TipoMovimiento = "Robo", Cantidad = 3, SyncGuid = Guid.NewGuid().ToString() },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnMovimientoUsaElUsuarioDelToken()
    {
        var db = new Mock<IInventoryMobileDbService>();
        db.Setup(service => service.RegisterMovementAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisterMovementResponse { ProductoId = 2, StockAnterior = 5, StockNuevo = 8 });

        var controller = WithUser(new MobileInventoryController(db.Object, Mock.Of<IMobileAuditService>(),
            NullLogger<MobileInventoryController>.Instance), userId: 41, role: "Bodeguero");

        await controller.RegisterMovement(
            new RegisterMovementRequest { ProductoId = 2, TipoMovimiento = "Entrada", Cantidad = 3, SyncGuid = Guid.NewGuid().ToString() },
            CancellationToken.None);

        db.Verify(service => service.RegisterMovementAsync(2, "Entrada", 3, null, It.IsAny<Guid>(), 41, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnaTransicionNoPermitida_SeResponde422SinCambiarNada()
    {
        var db = new Mock<IOrdersMobileDbService>(MockBehavior.Strict);
        db.Setup(service => service.GetOrderAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MobileOrderDetail { PedidoId = 12, Estado = "Pendiente", TieneFactura = true });

        var controller = WithUser(new MobileOrdersController(db.Object, Mock.Of<IMobileAuditService>(),
            NullLogger<MobileOrdersController>.Instance));

        var result = await controller.ChangeStatus(12, new ChangeOrderStatusRequest { Estado = "Cancelado" }, CancellationToken.None);

        var unprocessable = Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Contains("facturado", Assert.IsType<MobileError>(unprocessable.Value).Message);
        db.Verify(service => service.GetOrderAsync(12, It.IsAny<CancellationToken>()), Times.Once);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RechazarUnRetenidoSinMotivo_SeRechaza()
    {
        var db = new Mock<IOrdersMobileDbService>(MockBehavior.Strict);
        var controller = WithUser(new MobileOrdersController(db.Object, Mock.Of<IMobileAuditService>(),
            NullLogger<MobileOrdersController>.Instance));

        var result = await controller.Reject(12, new RejectOrderRequest { Motivo = "no" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnaVentaSinProductos_NoLlegaALaBase()
    {
        var db = new Mock<IOrdersMobileDbService>(MockBehavior.Strict);
        var controller = WithUser(new MobileSalesController(db.Object, Mock.Of<IMobileAuditService>(),
            NullLogger<MobileSalesController>.Instance));

        var result = await controller.Create(new CreateSaleRequest
        {
            ClienteId = 5,
            TipoEntrega = "Entrega por vendedor",
            DireccionEntrega = "San José",
            Items = new List<CreateSaleItem> { new() { ProductoId = 2, Cantidad = 0 } },
            SyncGuid = Guid.NewGuid().ToString()
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnaVentaSeRegistraAnombreDelVendedorDelToken()
    {
        var db = new Mock<IOrdersMobileDbService>();
        db.Setup(service => service.CreateSaleAsync(It.IsAny<CreateSaleRequest>(), It.IsAny<Guid>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSaleResponse { PedidoId = 90, Estado = "Pendiente" });

        var controller = WithUser(new MobileSalesController(db.Object, Mock.Of<IMobileAuditService>(),
            NullLogger<MobileSalesController>.Instance), userId: 33, role: "Vendedor");

        await controller.Create(new CreateSaleRequest
        {
            ClienteId = 5,
            TipoEntrega = "Entrega por vendedor",
            DireccionEntrega = "San José",
            Items = new List<CreateSaleItem> { new() { ProductoId = 2, Cantidad = 4 } },
            SyncGuid = Guid.NewGuid().ToString()
        }, CancellationToken.None);

        db.Verify(service => service.CreateSaleAsync(It.IsAny<CreateSaleRequest>(), It.IsAny<Guid>(), 33,
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReasignarSinMotivoSuficiente_SeRechaza()
    {
        var db = new Mock<IManagementMobileDbService>(MockBehavior.Strict);
        var controller = WithUser(new MobileManagementController(db.Object, Mock.Of<IMobileAuditService>(),
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System, NullLogger<MobileManagementController>.Instance));

        var result = await controller.Reassign(1, new ReassignRouteRequest
        {
            NuevoChoferId = 36,
            Motivo = "x",
            SyncGuid = Guid.NewGuid().ToString()
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnRangoDeMetricasDesconocido_SeRechaza()
    {
        var db = new Mock<IManagementMobileDbService>(MockBehavior.Strict);
        var controller = WithUser(new MobileManagementController(db.Object, Mock.Of<IMobileAuditService>(),
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System, NullLogger<MobileManagementController>.Instance));

        var result = await controller.Dashboard("siempre", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolverUnaJornadaConVersionInvalida_SeRechaza()
    {
        var db = new Mock<IOfficeMobileDbService>(MockBehavior.Strict);
        var controller = WithUser(new MobileStaffController(db.Object, Mock.Of<IMobileAuditService>(),
            TimeProvider.System, NullLogger<MobileStaffController>.Instance), role: "Supervisor");

        var result = await controller.ResolveAttendance(10,
            new ResolveAttendanceRequest { Decision = "Aprobada", Version = "no-base64" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnaJornadaFutura_SeRechaza()
    {
        var db = new Mock<IOfficeMobileDbService>(MockBehavior.Strict);
        var controller = WithUser(new MobileStaffController(db.Object, Mock.Of<IMobileAuditService>(),
            TimeProvider.System, NullLogger<MobileStaffController>.Instance));

        var result = await controller.SaveAttendance(new SaveAttendanceRequest
        {
            Fecha = DateTime.UtcNow.AddDays(3),
            HorasOrdinarias = 8,
            SyncGuid = Guid.NewGuid().ToString()
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        db.VerifyNoOtherCalls();
    }

    private static T WithUser<T>(T controller, int userId = 7, string role = "Administrador") where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("sub", userId.ToString()),
                    new Claim(ClaimTypes.Role, role),
                    new Claim(ClaimTypes.Name, "Usuario de Prueba")
                ], "Bearer"))
            }
        };
        return controller;
    }
}
