using System.Text.RegularExpressions;

namespace Proyecto_Final.Tests;

public sealed class Stage2DesignSystemTests
{
    private static readonly string[][] MigratedStorefrontViews =
    [
        ["Views", "Home", "Index.cshtml"], ["Views", "Home", "Shop.cshtml"], ["Views", "Home", "Detail.cshtml"], ["Views", "Home", "ComboDetail.cshtml"],
        ["Views", "Cart", "Index.cshtml"], ["Views", "Cart", "Checkout.cshtml"], ["Views", "Cart", "Confirmation.cshtml"],
        ["Views", "ClientPortal", "Index.cshtml"], ["Views", "ClientPortal", "Detail.cshtml"], ["Views", "ClientPortal", "Invoice.cshtml"], ["Views", "ClientPortal", "Statement.cshtml"], ["Views", "ClientPortal", "Warranty.cshtml"], ["Views", "ClientPortal", "Warranties.cshtml"],
        ["Views", "Profile", "Edit.cshtml"], ["Views", "Account", "Login.cshtml"], ["Views", "Account", "Registro.cshtml"], ["Views", "Account", "ForgotPassword.cshtml"], ["Views", "Account", "ResetPassword.cshtml"]
    ];
    private static readonly string[][] MigratedBackofficeViews =
    [
        ["Views", "Inventory", "Index.cshtml"], ["Views", "Inventory", "Create.cshtml"], ["Views", "Inventory", "Edit.cshtml"],
        ["Views", "Inventory", "Movements.cshtml"], ["Views", "Inventory", "RegisterMovement.cshtml"], ["Views", "Inventory", "TransformStock.cshtml"],
        ["Views", "InventoryIntelligence", "Index.cshtml"], ["Views", "OrdersAdmin", "Index.cshtml"], ["Views", "OrdersAdmin", "Detail.cshtml"],
        ["Views", "Suppliers", "Index.cshtml"], ["Views", "PurchaseOrders", "Index.cshtml"], ["Views", "PurchaseOrders", "Create.cshtml"],
        ["Views", "PurchaseOrders", "Detail.cshtml"], ["Views", "PriceHistory", "Index.cshtml"]
        , ["Views", "Combos", "Index.cshtml"], ["Views", "Returns", "Index.cshtml"], ["Views", "Returns", "Quarantine.cshtml"]
    ];

    [Fact]
    public void PilotViews_UseOnlyTheirStage2Layouts()
    {
        var home = ReadProject("Views", "Home", "Index.cshtml");
        var admin = ReadProject("Views", "Admin", "Index.cshtml");

        Assert.Contains("Layout = \"_StorefrontLayout\"", home, StringComparison.Ordinal);
        Assert.Contains("Layout = \"_WorkspaceLayout\"", admin, StringComparison.Ordinal);
        Assert.DoesNotContain("custom-theme.css", home, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("custom-theme.css", admin, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fa fa-", home, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fa fa-", admin, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Stage2Layouts_AreAccessibleAndDoNotLoadLegacyVisualDependencies()
    {
        var layouts = new[] { "_StorefrontLayout.cshtml", "_WorkspaceLayout.cshtml", "_FieldLayout.cshtml" }
            .Select(name => ReadProject("Views", "Shared", name))
            .ToArray();

        Assert.All(layouts, layout =>
        {
            Assert.Contains("djj-skip-link", layout, StringComparison.Ordinal);
            Assert.Contains("id=\"djj-main-content\"", layout, StringComparison.Ordinal);
            Assert.Contains("_DjjIconSprite", layout, StringComparison.Ordinal);
            Assert.DoesNotContain("bootstrap", layout, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("font-awesome", layout, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("custom-theme.css", layout, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains("aria-expanded=\"false\"", layouts[0], StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"djj-workspace-sidebar\"", layouts[1], StringComparison.Ordinal);
        Assert.Contains("viewport-fit=cover", layouts[2], StringComparison.Ordinal);
    }

    [Fact]
    public void Stage2Css_UsesPrefixedTokensAndIntroducesNoImportantOverrides()
    {
        var cssRoot = ProjectPath("wwwroot", "css", "stage2");
        var files = Directory.EnumerateFiles(cssRoot, "*.css").ToArray();

        Assert.Equal(7, files.Length);
        Assert.All(files, path => Assert.DoesNotContain("!important", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase));
        Assert.All(files, path => Assert.DoesNotMatch(new Regex(@"(?m)^\s*--(?!djj-)[a-zA-Z]", RegexOptions.CultureInvariant), File.ReadAllText(path)));
    }

    [Fact]
    public void EveryPilotPhosphorReference_ExistsInTheLocalSprite()
    {
        var sprite = ReadProject("Views", "Shared", "_DjjIconSprite.cshtml");
        var sources = MigratedStorefrontViews.Select(ReadProject).Concat(new[]
        {
            ReadProject("Views", "Admin", "Index.cshtml"),
            ReadProject("Views", "Shared", "_StorefrontLayout.cshtml"),
            ReadProject("Views", "Shared", "_WorkspaceLayout.cshtml"),
            ReadProject("Views", "Shared", "_FieldLayout.cshtml")
        });
        var references = sources.SelectMany(source => Regex.Matches(source, "#djj-icon-([a-z0-9-]+)").Select(match => match.Groups[1].Value)).Distinct();

        Assert.All(references, icon => Assert.Contains($"id=\"djj-icon-{icon}\"", sprite, StringComparison.Ordinal));
    }

    [Fact]
    public void MigratedStorefrontFlow_HasNoLegacyVisualDependencies()
    {
        Assert.All(MigratedStorefrontViews.Select(ReadProject), view =>
        {
            Assert.Contains("Layout", view, StringComparison.Ordinal);
            Assert.DoesNotContain("s3-", view, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("s4-", view, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("fa fa-", view, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("font-awesome", view, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("custom-theme.css", view, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("logo-icon.png", view, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void StorefrontForms_ExposeAccessibleValidationAndPreservePostContracts()
    {
        var checkout = ReadProject("Views", "Cart", "Checkout.cshtml");
        var warranty = ReadProject("Views", "ClientPortal", "Warranty.cshtml");
        var login = ReadProject("Views", "Account", "Login.cshtml");

        Assert.All(new[] { checkout, warranty, login }, view =>
        {
            Assert.Contains("asp-validation-summary", view, StringComparison.Ordinal);
            Assert.Contains("role=\"alert\"", view, StringComparison.Ordinal);
            Assert.Contains("data-djj-validation-summary", view, StringComparison.Ordinal);
        });
        Assert.Contains("asp-for=\"OperationToken\"", checkout, StringComparison.Ordinal);
        Assert.Contains("id=\"provincia\"", checkout, StringComparison.Ordinal);
        Assert.Contains("id=\"canton\"", checkout, StringComparison.Ordinal);
        Assert.Contains("id=\"distrito\"", checkout, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"Provincia-error\"", checkout, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"ReferenciaPago-hint ReferenciaPago-error\"", checkout, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminDashboard_HasThreeProminentActionsAndElevenSecondaryDestinations()
    {
        var admin = ReadProject("Views", "Admin", "Index.cshtml");
        Assert.Equal(3, Regex.Matches(admin, "djj-admin-quick-link--primary").Count);
        Assert.Contains("Ver los otros 11 módulos", admin, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"AccountsReceivableAdmin\"", admin, StringComparison.Ordinal);
    }

    [Fact]
    public void MigratedBackofficeCore_UsesWorkspaceAndHasNoLegacyVisualDependencies()
    {
        Assert.All(MigratedBackofficeViews.Select(ReadProject), view =>
        {
            Assert.Contains("_WorkspaceLayout", view, StringComparison.Ordinal);
            Assert.DoesNotContain("s3-", view, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("s4-", view, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("fa fa-", view, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("custom-theme.css", view, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("logo-icon.png", view, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void BackofficeCore_PreservesOperationalPostContractsAndAccessibleTables()
    {
        var movement = ReadProject("Views", "Inventory", "RegisterMovement.cshtml");
        var purchase = ReadProject("Views", "PurchaseOrders", "Create.cshtml");
        var receipt = ReadProject("Views", "PurchaseOrders", "Detail.cshtml");

        Assert.Contains("id=\"tipoMovimiento\"", movement, StringComparison.Ordinal);
        Assert.Contains("id=\"generaGasto\"", movement, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"TokenOperacion\"", purchase, StringComparison.Ordinal);
        Assert.Contains("id=\"purchase-lines\"", purchase, StringComparison.Ordinal);
        Assert.Contains("name=\"CantidadRecibidaAhora\"", receipt, StringComparison.Ordinal);
        Assert.All(MigratedBackofficeViews.Select(ReadProject), view => Assert.DoesNotContain("<table class=\"table", view, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WorkspaceNavigation_GroupsOperationalDomainsWithoutBypassingPermissions()
    {
        var layout = ReadProject("Views", "Shared", "_WorkspaceLayout.cshtml");
        Assert.Contains("Ventas y pedidos", layout, StringComparison.Ordinal);
        Assert.Contains("<li class=\"djj-workspace-nav__label\">Inventario</li>", layout, StringComparison.Ordinal);
        Assert.Contains("<li class=\"djj-workspace-nav__label\">Compras</li>", layout, StringComparison.Ordinal);
        Assert.Contains("@if (canInventory)", layout, StringComparison.Ordinal);
        Assert.Contains("@if (canPurchases)", layout, StringComparison.Ordinal);
    }

    private static string ReadProject(params string[] parts) => File.ReadAllText(ProjectPath(parts));

    private static string ProjectPath(params string[] parts) =>
        Path.Combine(new[] { RepositoryRoot(), "Proyecto_Final" }.Concat(parts).ToArray());

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz de la solución.");
    }
}
