using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class AuthorizationFilterTests
{
    [Fact]
    public void SessionAuthorization_WithoutSession_RedirectsToLogin()
    {
        var filter = new SessionAuthorizeAttribute("Administrador");
        var context = CreateContext();

        filter.OnAuthorization(context);

        var result = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal("Login", result.ActionName);
        Assert.Equal("Account", result.ControllerName);
    }

    [Fact]
    public void SessionAuthorization_WithWrongRole_ReturnsForbiddenAccessDeniedPage()
    {
        var filter = new SessionAuthorizeAttribute("Administrador");
        var context = CreateContext(userId: 8, email: "qa@example.test", role: "Cliente");

        filter.OnAuthorization(context);

        AssertAccessDenied(context.Result);
    }

    [Fact]
    public async Task AdminAuthorization_WithMissingPermission_ReturnsForbiddenAccessDeniedPage()
    {
        var permissions = new Mock<IRolePermissionService>();
        permissions
            .Setup(service => service.HasCodePermissionAsync("Empleado", "ADMIN_USERS"))
            .ReturnsAsync(false);
        var filter = new AdminAuthorizeFilter("AdministraciÃ³n", "ADMIN_USERS", permissions.Object);
        var context = CreateContext(userId: 12, email: "employee@example.test", role: "Empleado");

        await filter.OnAuthorizationAsync(context);

        AssertAccessDenied(context.Result);
    }

    private static void AssertAccessDenied(IActionResult? result)
    {
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, view.StatusCode);
        Assert.Equal("~/Views/Account/AccesoDenegado.cshtml", view.ViewName);
    }

    private static AuthorizationFilterContext CreateContext(int? userId = null, string? email = null, string? role = null)
    {
        var httpContext = new DefaultHttpContext
        {
            Session = new TestSession()
        };

        if (userId.HasValue)
            httpContext.Session.SetInt32("UserId", userId.Value);
        if (email is not null)
            httpContext.Session.SetString("UserEmail", email);
        if (role is not null)
            httpContext.Session.SetString("UserRole", role);

        return new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>());
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

        public string Id => "authorization-filter-test";
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
