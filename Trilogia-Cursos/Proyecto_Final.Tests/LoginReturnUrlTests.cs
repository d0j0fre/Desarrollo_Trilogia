using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_Final.Controllers;

namespace Proyecto_Final.Tests;

/// <summary>G3-14: iniciar sesión desde el pago vuelve al pago; un returnUrl externo no se sigue.</summary>
public sealed class LoginReturnUrlTests
{
    [Theory]
    [InlineData("/Cart/Checkout", "/Cart/Checkout")]
    [InlineData("https://sitio-ajeno.example/robo", null)]
    [InlineData("//sitio-ajeno.example", null)]
    [InlineData(null, null)]
    public void ElFormularioConservaSoloDireccionesLocales(string? returnUrl, string? esperado)
    {
        var controller = Controller(sesionAbierta: false);

        var result = Assert.IsType<ViewResult>(controller.Login(returnUrl));

        Assert.Equal(esperado, (string?)result.ViewData["ReturnUrl"]);
    }

    [Fact]
    public void ConSesionAbiertaVaDirectoAlPago()
    {
        var result = Controller(sesionAbierta: true).Login("/Cart/Checkout");

        Assert.Equal("/Cart/Checkout", Assert.IsType<LocalRedirectResult>(result).Url);
    }

    [Fact]
    public void AlIniciarSesionUsaElReturnUrl()
    {
        var source = MobileCorreccionesTests.Read("Proyecto_Final", "Controllers", "AccountController.cs");
        var post = source[source.IndexOf("public async Task<IActionResult> Login(", StringComparison.Ordinal)..];

        Assert.Contains("string? returnUrl = null", post[..200], StringComparison.Ordinal);
        Assert.Contains("returnUrl is null ? RedirectToAction(\"Index\", \"Home\") : LocalRedirect(returnUrl)", post, StringComparison.Ordinal);
    }

    private static AccountController Controller(bool sesionAbierta)
    {
        var http = new DefaultHttpContext { Session = new MemorySession() };
        if (sesionAbierta) http.Session.SetString("UserEmail", "cliente@x.test");
        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.IsLocalUrl(It.IsAny<string?>()))
            .Returns((string? u) => !string.IsNullOrEmpty(u) && u.StartsWith('/') && !u.StartsWith("//", StringComparison.Ordinal));
        return new AccountController(null!, null!, NullLogger<AccountController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            Url = url.Object
        };
    }

    private sealed class MemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);
        public string Id => "login-return-url";
        public bool IsAvailable => true;
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}
