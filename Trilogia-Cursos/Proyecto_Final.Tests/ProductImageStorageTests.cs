using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class ProductImageStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"trilogia-images-{Guid.NewGuid():N}");

    [Fact]
    public async Task StageAsync_AcceptsValidPngAndUsesGuidManagedName()
    {
        var storage = CreateStorage();
        var staged = await storage.StageAsync(FormFile("../../foto.png", "image/png",
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]));

        Assert.NotNull(staged);
        Assert.StartsWith(ProductImageStorageService.PublicPrefix + "producto-", staged.PublicUrl, StringComparison.Ordinal);
        Assert.EndsWith(".png", staged.PublicUrl, StringComparison.Ordinal);
        Assert.True(File.Exists(staged.PhysicalPath));
        Assert.StartsWith(Path.GetFullPath(_root), staged.PhysicalPath, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("archivo.exe", "image/png")]
    [InlineData("archivo.png", "application/octet-stream")]
    public async Task StageAsync_RejectsExtensionOrMimeMismatch(string name, string mime)
    {
        var storage = CreateStorage();
        await Assert.ThrowsAsync<ProductImageValidationException>(() =>
            storage.StageAsync(FormFile(name, mime, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])));
    }

    [Fact]
    public async Task StageAsync_RejectsSpoofedSignature()
    {
        var storage = CreateStorage();
        await Assert.ThrowsAsync<ProductImageValidationException>(() =>
            storage.StageAsync(FormFile("foto.webp", "image/webp", new byte[12])));
    }

    [Fact]
    public async Task DeleteAsync_IgnoresExternalAndTraversalUrls()
    {
        var storage = CreateStorage();
        Directory.CreateDirectory(_root);
        var sentinel = Path.Combine(_root, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "preservar");

        await storage.DeleteAsync("https://example.test/sentinel.txt");
        await storage.DeleteAsync("~/uploads/productos/../sentinel.txt");

        Assert.True(File.Exists(sentinel));
    }

    private ProductImageStorageService CreateStorage()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.WebRootPath).Returns(_root);
        return new ProductImageStorageService(environment.Object);
    }

    private static IFormFile FormFile(string name, string mime, byte[] bytes) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = mime };

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
