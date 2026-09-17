using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Controllers;

namespace Proyecto_Final.Tests;

public sealed class ControllerSurfaceAuditTests
{
    [Fact]
    public void AuditScope_ContainsAll62MvcAndApiControllers()
    {
        var controllers = GetControllers();
        var apiControllers = typeof(Proyecto_FinalAPI.Controllers.AuthController).Assembly
            .GetTypes()
            .Count(type => type.IsClass && !type.IsAbstract && type.Namespace == typeof(Proyecto_FinalAPI.Controllers.AuthController).Namespace && type.Name.EndsWith("Controller", StringComparison.Ordinal));

        // 56 originales + MobileAppController (página de descarga con QR, Fase 7).
        Assert.Equal(57, controllers.Count);
        // 2 originales (Auth, Products) + 2 de la superficie móvil Fase 2 +
        // MobileAppController (compuerta de versión, Fase 7) + 6 de la
        // aplicación por rol (inventario, pedidos, ventas, gestión, personal y
        // oficina). MobileControllerBase es abstracta y no cuenta.
        Assert.Equal(11, apiControllers);
        Assert.Equal(68, controllers.Count + apiControllers);
        Assert.Contains(typeof(BudgetsController), controllers);
        Assert.Contains(typeof(ChatController), controllers);
        Assert.Contains(typeof(PayrollController), controllers);
        Assert.Contains(typeof(PurchaseOrdersController), controllers);
    }

    /// <summary>
    /// La superficie que consume la aplicación móvil vive en la API, nunca en el
    /// MVC: el MVC exige antiforgery en todo POST y un cliente nativo no
    /// participa de ese flujo.
    ///
    /// La excepción legítima es <c>MobileAppController</c> del MVC, que es una
    /// página web para personas —el QR de descarga— y no un endpoint del
    /// teléfono. Por eso la regla se expresa sobre la ruta y no sobre el nombre.
    /// </summary>
    [Fact]
    public void TheMobileApiSurfaceLivesInTheApiProjectOnly()
    {
        var mvcControllersServingTheMobileApi = Directory
            .GetFiles(SourcePath("Controllers"), "*.cs")
            .Where(path => File.ReadAllText(path).Contains("api/mobile", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Empty(mvcControllersServingTheMobileApi);

        var apiMobileControllers = typeof(Proyecto_FinalAPI.Controllers.AuthController).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.Name.StartsWith("Mobile", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(apiMobileControllers);
    }

    /// <summary>
    /// La página de descarga redirige al archivo en vez de servirlo, y exige
    /// HTTPS antes de redirigir: un <c>Redirect</c> con una dirección tomada de
    /// la base es exactamente donde no se quiere un descuido.
    /// </summary>
    [Fact]
    public void TheMobileDownloadPageRedirectsOverHttpsAndAuditsTheDownload()
    {
        var source = File.ReadAllText(SourcePath("Controllers", "MobileAppController.cs"));

        Assert.Contains("Uri.UriSchemeHttps", source, StringComparison.Ordinal);
        Assert.Contains("RegisterAuditAsync", source, StringComparison.Ordinal);
        Assert.Contains("[SessionAuthorize]", source, StringComparison.Ordinal);
        Assert.Contains("MOVIL_APP_PUBLICAR", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryFormPostRequiresAntiforgeryExceptTheJsonOfflineSyncEndpoint()
    {
        var unsafePosts = GetControllers()
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(IsMvcAction)
            .Where(method => method.GetCustomAttributes<HttpPostAttribute>(true).Any())
            .Where(method => !(method.DeclaringType == typeof(SellerOrdersController) && method.Name == nameof(SellerOrdersController.SyncOffline)))
            .Where(method => !method.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>(true).Any())
            .ToArray();

        Assert.Empty(unsafePosts);
    }

    [Fact]
    public void GlobalErrorActionLogsTheExceptionAndReturns503()
    {
        var source = File.ReadAllText(SourcePath("Controllers", "HomeController.cs"));
        Assert.Contains("IExceptionHandlerFeature", source, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status503ServiceUnavailable", source, StringComparison.Ordinal);
        Assert.Contains("TraceId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ChatTechnicalFailuresUse503InsteadOfRaw500()
    {
        var source = File.ReadAllText(SourcePath("Controllers", "ChatController.cs"));
        Assert.DoesNotContain("StatusCode(500", source, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status503ServiceUnavailable", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PriorityFlowsExpose404_422And503InsteadOfRedirectingOrThrowing()
    {
        var expenses = File.ReadAllText(SourcePath("Controllers", "ExpensesController.cs"));
        var documents = File.ReadAllText(SourcePath("Controllers", "DocumentsController.cs"));
        var purchases = File.ReadAllText(SourcePath("Controllers", "PurchaseOrdersController.cs"));

        Assert.Contains("if (id <= 0) return NotFound();", expenses, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status422UnprocessableEntity", expenses, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status503ServiceUnavailable", expenses, StringComparison.Ordinal);
        Assert.Contains("if (id <= 0) return NotFound();", documents, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status422UnprocessableEntity", documents, StringComparison.Ordinal);
        Assert.Equal(2, purchases.Split("return UnprocessableEntity(", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void DirectedClientCheckoutAndPayrollFlowsPreserveHttpSemantics()
    {
        var clients = File.ReadAllText(SourcePath("Controllers", "ClientsController.cs"));
        var credits = File.ReadAllText(SourcePath("Controllers", "CreditsController.cs"));
        var cart = File.ReadAllText(SourcePath("Controllers", "CartController.cs"));
        var payroll = File.ReadAllText(SourcePath("Controllers", "PayrollController.cs"));

        Assert.Contains("InvalidClientForm", clients, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status503ServiceUnavailable", clients, StringComparison.Ordinal);
        Assert.Contains("if (id <= 0) return NotFound();", credits, StringComparison.Ordinal);
        Assert.Contains("InvalidCheckout", cart, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status422UnprocessableEntity", cart, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status503ServiceUnavailable", payroll, StringComparison.Ordinal);
        Assert.Contains("return UnprocessableEntity", payroll, StringComparison.Ordinal);
    }

    private static IReadOnlyList<Type> GetControllers() => typeof(HomeController).Assembly
        .GetTypes()
        .Where(type => type.IsClass && !type.IsAbstract && type.Namespace == typeof(HomeController).Namespace && type.Name.EndsWith("Controller", StringComparison.Ordinal))
        .OrderBy(type => type.Name)
        .ToArray();

    private static bool IsMvcAction(MethodInfo method) => !method.IsSpecialName && !method.IsDefined(typeof(NonActionAttribute), true);

    private static string SourcePath(params string[] parts) => Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", "Proyecto_Final", .. parts]);
}
