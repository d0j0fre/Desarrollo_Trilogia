using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>
/// Evidencia de entrega en la base (0306): el sitio y la API la comparten aunque no compartan
/// disco (Azure). Las reglas de entrada son las mismas de la carpeta.
/// </summary>
public sealed class EvidenceDatabaseStorageTests
{
    private static IConfiguration Config(string? provider) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EvidenceStorage:Provider"] = provider,
            ["ConnectionStrings:DefaultConnection"] = "Server=no-existe;Database=x;Connect Timeout=1"
        }).Build();

    private static string Fuente(params string[] partes) =>
        File.ReadAllText(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", ".." }.Concat(partes).ToArray()));

    [Theory]
    [InlineData("Database", true)]
    [InlineData("database", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("File", false)]
    public void Se_elige_por_configuracion(string? provider, bool esperado)
    {
        Assert.Equal(esperado, DatabaseEvidenceStorageService.IsSelected(Config(provider)));
    }

    [Fact]
    public void El_sitio_y_la_api_registran_el_almacenamiento_en_base()
    {
        Assert.Contains("DatabaseEvidenceStorageService.IsSelected(builder.Configuration)", Fuente("Proyecto_Final", "Program.cs"));
        Assert.Contains("DatabaseEvidenceStorageService.IsSelected(builder.Configuration)", Fuente("Proyecto_FinalAPI", "Program.cs"));
    }

    [Fact]
    public async Task Una_clave_insegura_no_llega_a_la_base()
    {
        var storage = new DatabaseEvidenceStorageService(Config("Database"));

        Assert.Null(await storage.OpenReadAsync("../../etc/passwd"));
        Assert.Null(await storage.OpenReadAsync("abc.exe"));
    }

    [Fact]
    public async Task Una_firma_que_no_es_png_se_rechaza_antes_de_guardar()
    {
        var storage = new DatabaseEvidenceStorageService(Config("Database"));
        var noEsPng = "data:image/png;base64," + Convert.ToBase64String("hola mundo"u8.ToArray());

        var error = await Assert.ThrowsAsync<EvidenceValidationException>(() => storage.StageSignatureAsync(noEsPng));
        Assert.Contains("no corresponde a una imagen", error.Message);
    }

    [Fact]
    public async Task Una_extension_no_admitida_se_rechaza_igual_que_en_la_carpeta()
    {
        var storage = new DatabaseEvidenceStorageService(Config("Database"));
        var bytes = new byte[] { 1, 2, 3 };
        IFormFile archivo = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "archivo", "foto.gif")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/gif"
        };

        var error = await Assert.ThrowsAsync<EvidenceValidationException>(() => storage.StageUploadAsync(archivo));
        Assert.Equal("La imagen debe estar en formato JPG, JPEG, PNG o WEBP.", error.Message);
    }

    [Fact]
    public void La_firma_vacia_no_es_un_error()
    {
        Assert.Null(EvidenceInput.DecodeSignature(null));
        Assert.Null(EvidenceInput.DecodeSignature("data:image/png;base64,"));
    }

    [Fact]
    public void La_migracion_0306_solo_entrega_imagenes_confirmadas()
    {
        var sql = Fuente("database", "migrations", "0306_evidencia_archivos_en_base.sql");

        Assert.Contains("WHERE StorageKey = @StorageKey AND Confirmado = 1", sql);
        Assert.Contains("TamanoBytes <= 5242880", sql);
    }
}
