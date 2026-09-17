using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_FinalAPI.Authorization;
using Proyecto_FinalAPI.Controllers;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services.Mobile;

namespace Proyecto_Final.Tests;

/// <summary>
/// Fase 2: superficie móvil del chofer.
/// Verifica que ningún endpoint quede abierto, que la identidad salga del token
/// y que la idempotencia sea obligatoria en toda escritura.
/// </summary>
public sealed class MobileDriverSurfaceTests
{
    private static readonly Type[] MobileControllers =
    [
        typeof(MobileDriverController),
        typeof(MobileMeController)
    ];

    // ── Protección de la superficie ─────────────────────────────────────────

    [Fact]
    public void TodoControladorMovilExigeAutenticacionYPermisoDeAcceso()
    {
        foreach (var controller in MobileControllers)
        {
            Assert.True(
                controller.GetCustomAttributes<AuthorizeAttribute>(true).Any(),
                $"{controller.Name} no exige autenticación.");

            var permissions = controller.GetCustomAttributes<RequirePermissionAttribute>(true).ToArray();
            Assert.True(permissions.Length > 0, $"{controller.Name} no exige ningún permiso.");
        }
    }

    [Fact]
    public void TodaEscrituraMovilTieneLimiteDePeticiones()
    {
        var writesWithoutLimit = MobileControllers
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttributes<HttpPostAttribute>(true).Any())
            .Where(method => !method.GetCustomAttributes<EnableRateLimitingAttribute>(true).Any())
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .ToArray();

        Assert.Empty(writesWithoutLimit);
    }

    [Fact]
    public void NingunEndpointMovilRecibeElIdentificadorDeUsuarioPorParametro()
    {
        // Es el riesgo exacto que documentó docs/api-auth-futura.md: un cliente
        // mandando un UsuarioId ajeno. La identidad sale del token, siempre.
        var offenders = MobileControllers
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetParameters().Select(parameter => new { method, parameter }))
            .Where(entry =>
                entry.parameter.Name is not null &&
                (entry.parameter.Name.Contains("usuarioId", StringComparison.OrdinalIgnoreCase) ||
                 entry.parameter.Name.Contains("userId", StringComparison.OrdinalIgnoreCase) ||
                 entry.parameter.Name.Contains("choferUsuarioId", StringComparison.OrdinalIgnoreCase)))
            .Select(entry => $"{entry.method.DeclaringType!.Name}.{entry.method.Name}({entry.parameter.Name})")
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void NingunContratoDeEntradaMovilLlevaIdentificadorDeUsuario()
    {
        var offenders = new[]
            {
                typeof(UpdateDeliveryStatusRequest),
                typeof(OpenMileageRequest),
                typeof(CloseMileageRequest)
            }
            .SelectMany(type => type.GetProperties().Select(property => new { type, property }))
            .Where(entry =>
                entry.property.Name.Contains("Usuario", StringComparison.OrdinalIgnoreCase) ||
                entry.property.Name.Contains("Chofer", StringComparison.OrdinalIgnoreCase))
            .Select(entry => $"{entry.type.Name}.{entry.property.Name}")
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ElKilometrajeExigeSuPropioPermisoAdemasDelDeAcceso()
    {
        var mileageActions = typeof(MobileDriverController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.Name.Contains("Mileage", StringComparison.Ordinal)
                          || method.Name == nameof(MobileDriverController.Vehicles))
            .ToArray();

        Assert.NotEmpty(mileageActions);

        foreach (var action in mileageActions)
        {
            var permission = action.GetCustomAttribute<RequirePermissionAttribute>();
            Assert.NotNull(permission);
            Assert.Equal(MobilePermissions.OwnMileage, (string)permission!.Arguments![0]);
        }
    }

    // ── Idempotencia obligatoria ────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task CambiarEstadoSinSyncGuidValido_SeRechaza(string syncGuid)
    {
        var db = new Mock<IDriverMobileDbService>(MockBehavior.Strict);
        var controller = BuildDriverController(db);

        var result = await controller.UpdateDeliveryStatus(
            10,
            new UpdateDeliveryStatusRequest { Estado = "Entregado", SyncGuid = syncGuid },
            CancellationToken.None);

        // Sin idempotencia un reintento duplicaría la entrega: no se deja pasar.
        Assert.IsType<BadRequestObjectResult>(result);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CambiarEstadoAUnValorDesconocido_SeRechaza()
    {
        var db = new Mock<IDriverMobileDbService>(MockBehavior.Strict);
        var controller = BuildDriverController(db);

        var result = await controller.UpdateDeliveryStatus(
            10,
            new UpdateDeliveryStatusRequest { Estado = "Cancelado", SyncGuid = Guid.NewGuid().ToString() },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MarcarFallidoSinMotivo_SeRechazaComoReglaDeNegocio()
    {
        var db = new Mock<IDriverMobileDbService>(MockBehavior.Strict);
        var controller = BuildDriverController(db);

        var result = await controller.UpdateDeliveryStatus(
            10,
            new UpdateDeliveryStatusRequest { Estado = "Fallido", SyncGuid = Guid.NewGuid().ToString() },
            CancellationToken.None);

        var unprocessable = Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, unprocessable.StatusCode);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CambiarEstadoUsaElUsuarioDelToken_NoUnoDelCuerpo()
    {
        var db = new Mock<IDriverMobileDbService>();
        db.Setup(service => service.UpdateDeliveryStatusAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateDeliveryStatusResponse
            {
                RutaPedidoId = 10, PedidoId = 99, EstadoEntrega = "Entregado"
            });

        var controller = BuildDriverController(db, userId: 77);

        await controller.UpdateDeliveryStatus(
            10,
            new UpdateDeliveryStatusRequest { Estado = "Entregado", SyncGuid = Guid.NewGuid().ToString() },
            CancellationToken.None);

        db.Verify(service => service.UpdateDeliveryStatusAsync(
            10, "Entregado", It.IsAny<Guid>(), null, 77, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UnaEntregaDuplicadaNoSeAudita()
    {
        var db = new Mock<IDriverMobileDbService>();
        db.Setup(service => service.UpdateDeliveryStatusAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateDeliveryStatusResponse
            {
                RutaPedidoId = 10, PedidoId = 99, EstadoEntrega = "Entregado", Duplicado = true
            });

        var audit = new Mock<IMobileAuditService>();
        var controller = BuildDriverController(db, audit: audit);

        await controller.UpdateDeliveryStatus(
            10,
            new UpdateDeliveryStatusRequest { Estado = "Entregado", SyncGuid = Guid.NewGuid().ToString() },
            CancellationToken.None);

        // La bitácora registra hechos, no reintentos de red.
        audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnaEntregaNuevaSeAuditaConCanalMovil()
    {
        var db = new Mock<IDriverMobileDbService>();
        db.Setup(service => service.UpdateDeliveryStatusAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateDeliveryStatusResponse
            {
                RutaPedidoId = 10, PedidoId = 99, EstadoEntrega = "Entregado"
            });

        var audit = new Mock<IMobileAuditService>();
        var controller = BuildDriverController(db, audit: audit);

        await controller.UpdateDeliveryStatus(
            10,
            new UpdateDeliveryStatusRequest { Estado = "Entregado", SyncGuid = Guid.NewGuid().ToString() },
            CancellationToken.None);

        audit.Verify(service => service.RecordAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            "Actualizar entrega", "Entregas", It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void ElServicioMovilDelChoferInvocaLosMismosProcedimientosQueElMvc()
    {
        // Paridad barata contra la deriva: si alguien crea un procedimiento
        // paralelo para el móvil, las reglas de negocio se bifurcan en silencio.
        var mobileSource = File.ReadAllText(SourcePath(
            "Proyecto_FinalAPI", "Services", "Mobile", "DriverMobileDbService.cs"));
        var mvcSource = File.ReadAllText(SourcePath(
            "Proyecto_Final", "Services", "LogisticsDbService.cs"));

        foreach (var procedure in new[]
                 {
                     "dbo.sp_Chofer_GetMyRoutes",
                     "dbo.sp_Chofer_GetRouteDeliveries",
                     "dbo.sp_Chofer_UpdateDeliveryStatus"
                 })
        {
            Assert.Contains(procedure, mobileSource, StringComparison.Ordinal);
            Assert.Contains(procedure, mvcSource, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ElMovilNuncaInvocaLosProcedimientosDeFlotaSinAlcanceDeChofer()
    {
        var mobileSource = File.ReadAllText(SourcePath(
            "Proyecto_FinalAPI", "Services", "Mobile", "DriverMobileDbService.cs"));

        // Estos son los de administración de flota: no validan pertenencia.
        Assert.DoesNotContain("dbo.sp_Kilometraje_Abrir", mobileSource, StringComparison.Ordinal);
        Assert.DoesNotContain("dbo.sp_Kilometraje_Cerrar", mobileSource, StringComparison.Ordinal);

        Assert.Contains("dbo.sp_Chofer_Kilometraje_Abrir", mobileSource, StringComparison.Ordinal);
        Assert.Contains("dbo.sp_Chofer_Kilometraje_Cerrar", mobileSource, StringComparison.Ordinal);
    }

    // ── Ayudantes ───────────────────────────────────────────────────────────

    private static MobileDriverController BuildDriverController(
        Mock<IDriverMobileDbService> db,
        Mock<IMobileAuditService>? audit = null,
        int userId = 7)
    {
        var controller = new MobileDriverController(
            db.Object,
            (audit ?? new Mock<IMobileAuditService>()).Object,
            NullLogger<MobileDriverController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(
                        [
                            new System.Security.Claims.Claim("sub", userId.ToString()),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Chofer"),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "Chofer de Prueba")
                        ], "Bearer"))
                }
            }
        };

        return controller;
    }

    private static string SourcePath(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", .. parts]);
}
