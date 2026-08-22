using System.Text.RegularExpressions;

namespace Proyecto_Final.Tests;

public sealed class Stage2DesignSystemTests
{
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
        var sources = new[]
        {
            ReadProject("Views", "Home", "Index.cshtml"),
            ReadProject("Views", "Admin", "Index.cshtml"),
            ReadProject("Views", "Shared", "_StorefrontLayout.cshtml"),
            ReadProject("Views", "Shared", "_WorkspaceLayout.cshtml"),
            ReadProject("Views", "Shared", "_FieldLayout.cshtml")
        };
        var references = sources.SelectMany(source => Regex.Matches(source, "#djj-icon-([a-z0-9-]+)").Select(match => match.Groups[1].Value)).Distinct();

        Assert.All(references, icon => Assert.Contains($"id=\"djj-icon-{icon}\"", sprite, StringComparison.Ordinal));
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
