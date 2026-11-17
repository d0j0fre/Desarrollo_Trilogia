using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_FinalAPI.Controllers;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services.Mobile;

namespace Proyecto_Final.Tests;

/// <summary>
/// La compuerta de versión debe entregar siempre un enlace de descarga cuando hay
/// una versión publicada; si el APK vive en el sistema, el enlace es la descarga del
/// sitio web. Antes devolvía vacío y la pantalla de actualización obligatoria de la
/// app quedaba sin botón.
/// </summary>
public sealed class MobileAppDownloadLinkTests
{
    private static MobileAppController Controller(string? siteUrl, AppRelease? release)
    {
        var settings = new Dictionary<string, string?>();
        if (siteUrl is not null) settings["PasswordRecovery:PublicBaseUrl"] = siteUrl;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var releases = new Mock<IAppReleaseDbService>();
        releases.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(release);

        return new MobileAppController(releases.Object, configuration, NullLogger<MobileAppController>.Instance);
    }

    private static AppRelease Release(string urlDescarga) =>
        new(2, "1.3.0", 2006, 2006, urlDescarga, new string('A', 64), 1000, null, "Actualice", DateTime.UtcNow);

    [Fact]
    public async Task Apk_en_el_sistema_apunta_a_la_descarga_del_sitio()
    {
        var result = await Controller("https://web.example.cr/", Release(string.Empty)).Version(CancellationToken.None);

        var body = Assert.IsType<MobileAppVersionResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("https://web.example.cr/MobileApp/Download", body.DownloadUrl);
        Assert.Equal(2006, body.MinSupportedBuild);
    }

    [Fact]
    public void Una_direccion_externa_se_respeta()
    {
        var url = Controller("https://web.example.cr", null).ResolveDownloadUrl("https://descargas.example.cr/app.apk");

        Assert.Equal("https://descargas.example.cr/app.apk", url);
    }

    [Fact]
    public void La_lectura_de_la_version_tolera_url_nula()
    {
        // Con el APK en el sistema la columna UrlDescarga es NULL; leerla con GetString
        // tiraba SqlNullValueException y /api/mobile/v1/app/version respondía 500.
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "Proyecto_FinalAPI", "Services", "Mobile", "AppReleaseDbService.cs"));

        Assert.Contains("reader.IsDBNull(4) ? string.Empty : reader.GetString(4)", source);
    }

    [Fact]
    public void Sin_sesion_el_login_recuerda_la_descarga_pedida()
    {
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        http.Session = new EmptySession();
        http.Request.Method = "GET";
        http.Request.Path = "/MobileApp/Download";
        var context = new Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext(
            new Microsoft.AspNetCore.Mvc.ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(),
                new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()),
            new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>());

        new Proyecto_Final.Filters.SessionAuthorizeAttribute().OnAuthorization(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("/MobileApp/Download", redirect.RouteValues?["returnUrl"]);
    }

    private sealed class EmptySession : Microsoft.AspNetCore.Http.ISession
    {
        public bool IsAvailable => true;
        public string Id => "prueba";
        public IEnumerable<string> Keys => [];
        public void Clear() { }
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) { }
        public void Set(string key, byte[] value) { }
        public bool TryGetValue(string key, out byte[] value) { value = []; return false; }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://web.example.cr")]
    [InlineData("no es una url")]
    public void Sin_sitio_https_configurado_no_inventa_un_enlace(string? siteUrl)
    {
        Assert.Equal(string.Empty, Controller(siteUrl, null).ResolveDownloadUrl(null));
    }
}
