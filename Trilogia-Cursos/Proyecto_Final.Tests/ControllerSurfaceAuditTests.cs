using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Controllers;

namespace Proyecto_Final.Tests;

public sealed class ControllerSurfaceAuditTests
{
    [Fact]
    public void AuditScope_ContainsAll58MvcAndApiControllers()
    {
        var controllers = GetControllers();
        var apiControllers = typeof(Proyecto_FinalAPI.Controllers.AuthController).Assembly
            .GetTypes()
            .Count(type => type.IsClass && !type.IsAbstract && type.Namespace == typeof(Proyecto_FinalAPI.Controllers.AuthController).Namespace && type.Name.EndsWith("Controller", StringComparison.Ordinal));

        Assert.Equal(56, controllers.Count);
        Assert.Equal(2, apiControllers);
        Assert.Equal(58, controllers.Count + apiControllers);
        Assert.Contains(typeof(BudgetsController), controllers);
        Assert.Contains(typeof(ChatController), controllers);
        Assert.Contains(typeof(PayrollController), controllers);
        Assert.Contains(typeof(PurchaseOrdersController), controllers);
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

    private static IReadOnlyList<Type> GetControllers() => typeof(HomeController).Assembly
        .GetTypes()
        .Where(type => type.IsClass && !type.IsAbstract && type.Namespace == typeof(HomeController).Namespace && type.Name.EndsWith("Controller", StringComparison.Ordinal))
        .OrderBy(type => type.Name)
        .ToArray();

    private static bool IsMvcAction(MethodInfo method) => !method.IsSpecialName && !method.IsDefined(typeof(NonActionAttribute), true);

    private static string SourcePath(params string[] parts) => Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", "Proyecto_Final", .. parts]);
}
