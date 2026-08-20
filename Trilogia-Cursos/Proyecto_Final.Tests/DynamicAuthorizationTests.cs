using Proyecto_Final.Controllers;
using Proyecto_Final.Filters;

namespace Proyecto_Final.Tests;

public sealed class DynamicAuthorizationTests
{
    [Theory]
    [InlineData(typeof(SellerOrdersController), "VENTA_MOVIL_CREAR")]
    [InlineData(typeof(ChatController), "CHAT_USAR")]
    [InlineData(typeof(MyGoalController), "METAS_PROPIAS_VER")]
    public void FormerRoleOnlyControllers_RequireExactCapability(Type controller, string permission)
    {
        var attribute = Assert.IsType<AdminAuthorizeAttribute>(Assert.Single(
            controller.GetCustomAttributes(typeof(AdminAuthorizeAttribute), true)));

        Assert.Equal(permission, attribute.Arguments![1]);
        Assert.Empty(controller.GetCustomAttributes(typeof(SessionAuthorizeAttribute), true));
    }

    [Theory]
    [InlineData(typeof(EmployeePortalController))]
    [InlineData(typeof(MyAttendanceController))]
    [InlineData(typeof(MyPaySlipsController))]
    public void EmployeeSelfService_RequiresActiveEmployeeRelationship(Type controller) =>
        Assert.Single(controller.GetCustomAttributes(typeof(EmployeeRelationshipAuthorizeAttribute), true));

    [Fact]
    public void Layout_UsesEffectiveCapabilitiesAndValidWarrantyRoute()
    {
        var source = File.ReadAllText(SourcePath("Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("GetPermissionCodesAsync", source, StringComparison.Ordinal);
        Assert.Contains("HasPermission(\"VENTA_MOVIL_CREAR\")", source, StringComparison.Ordinal);
        Assert.Contains("HasPermission(\"METAS_PROPIAS_VER\")", source, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Warranties\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-action=\"Warranty\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0027_DeclaresAndVerifiesDynamicCapabilities()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(root, "database", "migrations", "0027_dynamic_authorization_capabilities.sql"));
        var verify = File.ReadAllText(Path.Combine(root, "database", "migrations", "0027_dynamic_authorization_capabilities.verify.sql"));

        foreach (var permission in new[] { "VENTA_MOVIL_CREAR", "CHAT_USAR", "METAS_PROPIAS_VER" })
        {
            Assert.Contains(permission, migration, StringComparison.Ordinal);
            Assert.Contains(permission, verify, StringComparison.Ordinal);
        }
    }

    private static string SourcePath(params string[] segments) =>
        Path.Combine(new[] { FindRepositoryRoot(), "Proyecto_Final" }.Concat(segments).ToArray());

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz de la solución.");
    }
}
