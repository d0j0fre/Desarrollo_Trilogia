using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_Final.Middleware;

namespace Proyecto_Final.Tests;

public sealed class SessionStatusMiddlewareTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task SessionIsCheckedOnEveryRequest(bool valid, bool nextCalled)
    {
        var status = new Mock<ISessionStatusService>();
        status.Setup(s => s.IsCurrentAsync(7, "Auditor", It.IsAny<CancellationToken>())).ReturnsAsync(valid);
        var context = new DefaultHttpContext { Session = new TestSession() };
        context.Session.SetInt32("UserId", 7);
        context.Session.SetString("UserRole", "Auditor");
        var called = false;
        var middleware = new SessionStatusMiddleware(_ => { called = true; return Task.CompletedTask; }, NullLogger<SessionStatusMiddleware>.Instance);

        await middleware.InvokeAsync(context, status.Object);

        Assert.Equal(nextCalled, called);
        if (!valid)
        {
            Assert.Null(context.Session.GetInt32("UserId"));
            Assert.Equal("/Account/Login", context.Response.Headers.Location.ToString());
        }
    }

    [Fact]
    public async Task UnavailableDatabaseFailsClosed()
    {
        var status = new Mock<ISessionStatusService>();
        status.Setup(s => s.IsCurrentAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        var context = new DefaultHttpContext { Session = new TestSession() };
        context.Session.SetInt32("UserId", 7);
        var called = false;
        var middleware = new SessionStatusMiddleware(_ => { called = true; return Task.CompletedTask; }, NullLogger<SessionStatusMiddleware>.Instance);
        await middleware.InvokeAsync(context, status.Object);
        Assert.False(called);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new();
        public bool IsAvailable => true;
        public string Id => "test";
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}
