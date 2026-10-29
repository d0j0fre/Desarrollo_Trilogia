using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using JJ.Commerce;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Proyecto_Final.Controllers;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class DeliveryNotificationClientTests
{
    [Theory]
    [InlineData(nameof(DriverDeliveriesController.NotifyEmail))]
    [InlineData(nameof(DriverDeliveriesController.EmailReceipt))]
    public void Delivery_mail_actions_are_post_antiforgery_rate_limited_and_selector_only(string action)
    {
        var method = typeof(DriverDeliveriesController).GetMethod(action)!;
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Equal("delivery-notification", method.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.Collection(method.GetParameters(), parameter =>
        {
            Assert.Equal("rutaPedidoId", parameter.Name);
            Assert.Equal(typeof(int), parameter.ParameterType);
        });
        Assert.NotNull(typeof(DriverDeliveriesController).GetCustomAttribute<SessionAuthorizeAttribute>());
    }

    [Fact]
    public void Delivery_controller_role_admission_is_closed_to_driver_and_administrator()
    {
        var attribute = typeof(DriverDeliveriesController).GetCustomAttribute<SessionAuthorizeAttribute>()!;
        var roles = (string[])typeof(SessionAuthorizeAttribute)
            .GetField("_allowedRoles", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(attribute)!;
        Assert.Equal(new[] { "Chofer", "Administrador" }, roles);
        Assert.DoesNotContain("Vendedor", roles);
    }

    [Fact]
    public async Task Notify_reads_durable_receipt_then_sends_once_with_original_context_and_server_selectors()
    {
        var login = Login();
        var sync = Guid.NewGuid();
        Guid operation = Guid.Empty;
        var (client, handler) = Client(login, async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var json = await request.Content!.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            Assert.Equal(new[] { "operationId", "routeOrderId", "session", "syncGuid" },
                document.RootElement.EnumerateObject().Select(x => x.Name).Order().ToArray());
            if (path.EndsWith("/receipt", StringComparison.Ordinal))
            {
                var command = JsonSerializer.Deserialize<DeliveryNotificationReceiptCommand>(json, JsonOptions)!;
                Assert.Equal(72, command.RouteOrderId);
                Assert.Equal(Guid.Empty, command.SyncGuid);
                Assert.Equal(Guid.Empty, command.OperationId);
                Assert.Equal(login.LoginId, command.Session.LoginId);
                Assert.Equal(login.BindingHash.ToArray(), command.Session.BindingHash);
                return new(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new DeliveryNotificationReceipt(Guid.Empty, sync, "None"))
                };
            }

            Assert.EndsWith("/send", path, StringComparison.Ordinal);
            var send = JsonSerializer.Deserialize<DeliveryNotificationCommand>(json, JsonOptions)!;
            operation = send.OperationId;
            Assert.NotEqual(Guid.Empty, operation);
            Assert.NotEqual(sync, operation);
            Assert.Equal(sync, send.SyncGuid);
            Assert.Equal(72, send.RouteOrderId);
            Assert.Equal(login.LoginId, send.Session.LoginId);
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new DeliveryNotificationReceipt(operation, sync, "SmtpAccepted"))
            };
        });

        var result = await client.NotifyAsync(72, Guid.Empty, default);

        Assert.Equal("SmtpAccepted", result.State);
        Assert.Equal(operation, result.OperationId);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Pending_receipt_reuses_the_original_UUID_without_allocating_or_replaying()
    {
        var sync = Guid.NewGuid();
        var operation = Guid.NewGuid();
        var (client, handler) = Client(Login(), async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/receipt", StringComparison.Ordinal))
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new DeliveryNotificationReceipt(operation, sync, "Pending")) };
            var command = await request.Content!.ReadFromJsonAsync<DeliveryNotificationCommand>();
            Assert.Equal(operation, command!.OperationId);
            Assert.Equal(sync, command.SyncGuid);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new DeliveryNotificationReceipt(operation, sync, "Unknown")) };
        });

        Assert.Equal("Unknown", (await client.NotifyAsync(8, sync, default)).State);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("NoEvent")]
    [InlineData("Sending")]
    [InlineData("Unknown")]
    [InlineData("SmtpAccepted")]
    public async Task Non_sendable_receipt_states_never_call_send(string state)
    {
        var sync = state == "NoEvent" ? Guid.Empty : Guid.NewGuid();
        var operation = state == "NoEvent" ? Guid.Empty : Guid.NewGuid();
        var (client, handler) = Client(Login(), request =>
        {
            Assert.EndsWith("/receipt", request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(new DeliveryNotificationReceipt(operation, sync, state)) });
        });

        Assert.Equal(state, (await client.NotifyAsync(8, Guid.Empty, default)).State);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Already_requested_never_adopts_another_login_UUID()
    {
        var sync = Guid.NewGuid();
        Guid requested = Guid.Empty;
        var lookup = 0;
        var (client, handler) = Client(Login(), async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/receipt", StringComparison.Ordinal))
            {
                lookup++;
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new DeliveryNotificationReceipt(Guid.Empty, sync, "None")) };
            }
            requested = (await request.Content!.ReadFromJsonAsync<DeliveryNotificationCommand>())!.OperationId;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new DeliveryNotificationReceipt(requested, sync, "AlreadyRequested")) };
        });

        var result = await client.NotifyAsync(8, sync, default);

        Assert.Equal("AlreadyRequested", result.State);
        Assert.Equal(requested, result.OperationId);
        Assert.Equal(2, lookup);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Lost_send_response_is_never_replayed()
    {
        var sync = Guid.NewGuid();
        var (client, handler) = Client(Login(), request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/receipt", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = JsonContent.Create(new DeliveryNotificationReceipt(Guid.Empty, sync, "None")) });
            throw new HttpRequestException("Synthetic lost response");
        });

        await Assert.ThrowsAsync<HttpRequestException>(() => client.NotifyAsync(8, sync, default));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Caller_cancellation_reaches_HTTP_and_does_not_replay()
    {
        var handler = new CancellationHandler();
        var context = new DefaultHttpContext();
        context.Items[OriginalLoginSession.ValidatedContextItem] = Login();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["DeliveryNotifications:Enabled"] = bool.TrueString }).Build();
        var client = new DeliveryNotificationClient(new HttpClient(handler) { BaseAddress = new("https://localhost:7444/") },
            new HttpContextAccessor { HttpContext = context }, configuration);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ReceiptAsync(8, Guid.Empty, Guid.Empty, cancellation.Token));

        Assert.Equal(1, handler.Calls);
        Assert.True(handler.ObservedCancellation);
    }

    [Theory]
    [InlineData("BadState", false)]
    [InlineData("Pending", true)]
    public async Task Malformed_or_mismatched_receipts_fail_closed_as_503(string state, bool wrongEvent)
    {
        var sync = Guid.NewGuid();
        var operation = Guid.NewGuid();
        var returnedSync = wrongEvent ? Guid.NewGuid() : sync;
        var (client, handler) = Client(Login(), _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = JsonContent.Create(new DeliveryNotificationReceipt(operation, returnedSync, state)) }));

        var error = await Assert.ThrowsAsync<DeliveryNotificationTransportException>(() => client.ReceiptAsync(8, sync, operation, default));

        Assert.Equal(503, error.Status);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Missing_or_disabled_original_context_never_calls_the_authority(bool disabled)
    {
        var (client, handler) = Client(null, _ => throw new Exception("Must not call"), !disabled);
        if (disabled) await Assert.ThrowsAsync<DeliveryNotificationTransportException>(() => client.ReceiptAsync(1, Guid.Empty, Guid.Empty, default));
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.ReceiptAsync(1, Guid.Empty, Guid.Empty, default));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(503)]
    public async Task Rejections_preserve_numeric_status_and_do_not_retry(int status)
    {
        var (client, handler) = Client(Login(), _ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        var error = await Assert.ThrowsAsync<DeliveryNotificationTransportException>(() => client.ReceiptAsync(1, Guid.Empty, Guid.Empty, default));
        Assert.Equal(status, error.Status);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(403, 403)]
    [InlineData(404, 404)]
    [InlineData(409, 503)]
    [InlineData(503, 503)]
    public async Task MVC_action_preserves_opaque_security_rejections_and_sanitizes_other_failures(int source, int expected)
    {
        var controller = Controller(new ThrowingClient(new DeliveryNotificationTransportException(source)));

        var result = Assert.IsType<ObjectResult>(await controller.NotifyEmail(8));

        Assert.Equal(expected, result.StatusCode);
        Assert.DoesNotContain(source.ToString(), JsonSerializer.Serialize(result.Value));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static OriginalLoginContext Login()
    {
        var issued = DateTime.UtcNow.AddMinutes(-1);
        return new(Guid.NewGuid(), 1040, 14, Guid.NewGuid(), issued, issued.AddHours(8),
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    }

    private static (DeliveryNotificationClient Client, Handler Handler) Client(
        OriginalLoginContext? login,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> response,
        bool enabled = true)
    {
        var context = new DefaultHttpContext();
        if (login is not null) context.Items[OriginalLoginSession.ValidatedContextItem] = login;
        var handler = new Handler(response);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DeliveryNotifications:Enabled"] = enabled.ToString()
        }).Build();
        return (new(new HttpClient(handler) { BaseAddress = new("https://localhost:7444/") },
            new HttpContextAccessor { HttpContext = context }, configuration), handler);
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return response(request);
        }
    }

    private sealed class CancellationHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool ObservedCancellation { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            ObservedCancellation = cancellationToken.IsCancellationRequested;
            return Task.FromCanceled<HttpResponseMessage>(cancellationToken);
        }
    }

    private static DriverDeliveriesController Controller(IDeliveryNotificationClient notifications)
    {
        var controller = new DriverDeliveriesController(null!, null!, null!, NullLogger<DriverDeliveriesController>.Instance, notifications)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        return controller;
    }

    private sealed class ThrowingClient(Exception error) : IDeliveryNotificationClient
    {
        public Task<DeliveryNotificationReceipt> NotifyAsync(int routeOrder, Guid sync, CancellationToken ct) => Task.FromException<DeliveryNotificationReceipt>(error);
        public Task<DeliveryNotificationReceipt> ReceiptAsync(int routeOrder, Guid sync, Guid operation, CancellationToken ct) => Task.FromException<DeliveryNotificationReceipt>(error);
    }
}
