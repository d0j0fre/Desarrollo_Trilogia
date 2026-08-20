using Proyecto_Final.Controllers;

namespace Proyecto_Final.Tests;

public sealed class LoginAndCartFlowTests
{
    [Fact]
    public void Administrator_DefaultLanding_IsAdministrativeWorkspace()
    {
        var destination = AccountController.DefaultLandingForRole("Administrador");

        Assert.Equal("Admin", destination.Controller);
        Assert.Equal("Index", destination.Action);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Cliente")]
    [InlineData("Vendedor")]
    public void OtherProfiles_DefaultLanding_RemainsStoreHome(string? role)
    {
        var destination = AccountController.DefaultLandingForRole(role);

        Assert.Equal("Home", destination.Controller);
        Assert.Equal("Index", destination.Action);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(5, -1, 0)]
    [InlineData(0, 8, 1)]
    [InlineData(10, 8, 8)]
    [InlineData(3, 8, 3)]
    public void CartQuantity_NeverCreatesValidQuantityWithoutStock(int requested, int stock, int expected) =>
        Assert.Equal(expected, CartQuantity.ClampToAvailableStock(requested, stock));

    [Fact]
    public void LoginConsumesOnlyLocalReturnUrl()
    {
        var source = File.ReadAllText(SourcePath("Controllers", "AccountController.cs"));

        Assert.Contains("Url.IsLocalUrl(returnUrl)", source, StringComparison.Ordinal);
        Assert.Contains("LocalRedirect(localReturnUrl)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Redirect(returnUrl)", source, StringComparison.Ordinal);
    }

    private static string SourcePath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz de la solución.");
        return Path.Combine(new[] { root, "Proyecto_Final" }.Concat(segments).ToArray());
    }
}
