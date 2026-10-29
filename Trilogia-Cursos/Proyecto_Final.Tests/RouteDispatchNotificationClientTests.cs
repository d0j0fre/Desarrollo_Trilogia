using System.Net;
using System.Net.Http.Json;
using JJ.Commerce;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class RouteDispatchNotificationClientTests
{
    [Fact]
    public async Task Reads_before_send_and_uses_original_session_and_resource_tuple()
    {
        var login = Login(); var dispatch = Guid.NewGuid(); var eventGuid = Guid.NewGuid(); Guid operation = Guid.Empty;
        var (client, handler) = Client(login, async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/receipt", StringComparison.Ordinal))
            {
                var query = await request.Content!.ReadFromJsonAsync<RouteDispatchNotificationReceiptCommand>();
                Assert.Equal(login.LoginId, query!.Session.LoginId); Assert.Equal(8, query.RouteId); Assert.Equal(dispatch, query.DispatchId); Assert.Equal(99, query.EventId); Assert.Equal(eventGuid, query.EventGuid);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new RouteDispatchNotificationReceipt(Guid.Empty, eventGuid, "None")) };
            }
            var command = await request.Content!.ReadFromJsonAsync<RouteDispatchNotificationCommand>(); operation = command!.OperationId;
            Assert.NotEqual(dispatch, operation); Assert.NotEqual(eventGuid, operation); Assert.NotEqual(Guid.Empty, operation);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new RouteDispatchNotificationReceipt(operation, eventGuid, "SmtpAccepted")) };
        });
        var receipt = await client.NotifyAsync(8, dispatch, 99, eventGuid, default);
        Assert.Equal(operation, receipt.OperationId); Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Pending_reuses_original_UUID()
    {
        var dispatch = Guid.NewGuid(); var eventGuid = Guid.NewGuid(); var operation = Guid.NewGuid();
        var (client, handler) = Client(Login(), async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/receipt", StringComparison.Ordinal)) return new(HttpStatusCode.OK) { Content = JsonContent.Create(new RouteDispatchNotificationReceipt(operation, eventGuid, "Pending")) };
            Assert.Equal(operation, (await request.Content!.ReadFromJsonAsync<RouteDispatchNotificationCommand>())!.OperationId);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new RouteDispatchNotificationReceipt(operation, eventGuid, "Unknown")) };
        });
        Assert.Equal("Unknown", (await client.NotifyAsync(8, dispatch, 99, eventGuid, default)).State); Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("NoRecipient")][InlineData("Unknown")][InlineData("Sending")][InlineData("SmtpAccepted")]
    public async Task Non_sendable_state_never_calls_send(string state)
    {
        var eventGuid = Guid.NewGuid(); var operation = state == "NoRecipient" ? Guid.Empty : Guid.NewGuid();
        var (client, handler) = Client(Login(), request =>
        {
            Assert.EndsWith("/receipt", request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new RouteDispatchNotificationReceipt(operation, eventGuid, state)) });
        });
        Assert.Equal(state, (await client.NotifyAsync(8, Guid.NewGuid(), 99, eventGuid, default)).State); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Lost_send_response_is_not_replayed()
    {
        var dispatch = Guid.NewGuid(); var eventGuid = Guid.NewGuid(); Guid operation = Guid.Empty; int receipts = 0;
        var (client, handler) = Client(Login(), async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/receipt", StringComparison.Ordinal))
            {
                receipts++;
                var lookup = await request.Content!.ReadFromJsonAsync<RouteDispatchNotificationReceiptCommand>();
                Assert.Equal(Guid.Empty, lookup!.OperationId);
                return new(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(receipts == 1
                        ? new RouteDispatchNotificationReceipt(Guid.Empty, eventGuid, "None")
                        : new RouteDispatchNotificationReceipt(operation, eventGuid, "Unknown"))
                };
            }
            operation = (await request.Content!.ReadFromJsonAsync<RouteDispatchNotificationCommand>())!.OperationId;
            throw new HttpRequestException("Synthetic lost response");
        });
        await Assert.ThrowsAsync<HttpRequestException>(() => client.NotifyAsync(8, dispatch, 99, eventGuid, default));
        var recovered = await client.ReceiptAsync(8, dispatch, 99, eventGuid, Guid.Empty, default);
        Assert.Equal(operation, recovered.OperationId); Assert.Equal("Unknown", recovered.State); Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Another_request_does_not_adopt_foreign_UUID()
    {
        var eventGuid = Guid.NewGuid(); Guid requested = Guid.Empty;
        var (client, handler) = Client(Login(), async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/receipt", StringComparison.Ordinal)) return new(HttpStatusCode.OK) { Content = JsonContent.Create(new RouteDispatchNotificationReceipt(Guid.Empty, eventGuid, "None")) };
            requested = (await request.Content!.ReadFromJsonAsync<RouteDispatchNotificationCommand>())!.OperationId;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new RouteDispatchNotificationReceipt(requested, eventGuid, "AlreadyRequested")) };
        });
        var result = await client.NotifyAsync(8, Guid.NewGuid(), 99, eventGuid, default);
        Assert.Equal("AlreadyRequested", result.State); Assert.Equal(requested, result.OperationId); Assert.Equal(3, handler.Calls);
    }

    [Theory]
    [InlineData(403)][InlineData(404)][InlineData(409)][InlineData(503)]
    public async Task Numeric_rejections_are_preserved_without_retry(int status)
    {
        var (client, handler) = Client(Login(), _ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        var error = await Assert.ThrowsAsync<RouteDispatchNotificationTransportException>(() => client.ReceiptAsync(8, Guid.NewGuid(), 99, Guid.NewGuid(), Guid.Empty, default));
        Assert.Equal(status, error.Status); Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Wrong_event_is_rejected_as_503()
    {
        var (client, handler) = Client(Login(), _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new RouteDispatchNotificationReceipt(Guid.NewGuid(), Guid.NewGuid(), "Pending")) }));
        Assert.Equal(503, (await Assert.ThrowsAsync<RouteDispatchNotificationTransportException>(() => client.ReceiptAsync(8, Guid.NewGuid(), 99, Guid.NewGuid(), Guid.Empty, default))).Status);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Missing_original_context_never_calls_HTTP()
    {
        var (client, handler) = Client(null, _ => throw new Exception("Must not call"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.ReceiptAsync(8, Guid.NewGuid(), 99, Guid.NewGuid(), Guid.Empty, default)); Assert.Equal(0, handler.Calls);
    }

    private static OriginalLoginContext Login()
    {
        var issued = DateTime.UtcNow.AddMinutes(-1);
        return new(Guid.NewGuid(), 1040, 14, Guid.NewGuid(), issued, issued.AddHours(8), System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    }
    private static (RouteDispatchNotificationClient Client, Handler Handler) Client(OriginalLoginContext? login, Func<HttpRequestMessage, Task<HttpResponseMessage>> response)
    {
        var context = new DefaultHttpContext(); if (login is not null) context.Items[OriginalLoginSession.ValidatedContextItem] = login;
        var handler = new Handler(response);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["RouteDispatchNotifications:Enabled"] = "true" }).Build();
        return (new(new HttpClient(handler) { BaseAddress = new("https://localhost:7444/") }, new HttpContextAccessor { HttpContext = context }, configuration), handler);
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Calls++; return response(request); }
    }
}
