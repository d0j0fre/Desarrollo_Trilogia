namespace Proyecto_Final.Tests;

/// <summary>
/// POST-qa1-04: la plantilla del comprobante del pedido se lee del disco al comprar. En Azure faltaba porque los .html
/// fuera de wwwroot no viajan en la publicación si el proyecto no lo pide. Estas pruebas cuidan que siga pidiéndolo.
/// </summary>
public sealed class PlantillasCorreoPublicacionTests
{
    private static string SourcePath(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", "Proyecto_Final", .. parts]);

    [Fact]
    public void OrderReceiptTemplate_ExistsInTheSourceFolder()
    {
        Assert.True(File.Exists(SourcePath("EmailTemplates", "OrderReceipt.html")));
    }

    [Fact]
    public void ProjectFile_CopiesEmailTemplatesToThePublishFolder()
    {
        var csproj = File.ReadAllText(SourcePath("Proyecto_Final.csproj"));

        Assert.Contains("EmailTemplates", csproj);
        Assert.Contains("CopyToPublishDirectory=\"PreserveNewest\"", csproj);
    }
}
