using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JJ.Commerce;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Proyecto_Final.Services;
using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Controllers;
using Proyecto_Final.Filters;

namespace Proyecto_Final.Tests;

public sealed class InvoiceNotificationClientTests
{
    [Theory]
    [InlineData(nameof(BillingController.ResendEmail))]
    [InlineData(nameof(BillingController.NewEmailAttempt))]
    public void Invoice_mail_mutations_require_antiforgery_and_explicit_capability(string action)
    {
        var method = typeof(BillingController).GetMethod(action)!;
        Assert.NotNull(method.GetCustomAttributes(typeof(HttpPostAttribute), true).SingleOrDefault());
        Assert.NotNull(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).SingleOrDefault());
        var permission = Assert.IsType<AdminAuthorizeAttribute>(method.GetCustomAttributes(typeof(AdminAuthorizeAttribute), true).Single());
        Assert.NotNull(permission);
        // The action contract has selectors only, never a recipient, body or amount.
        Assert.All(method.GetParameters(), parameter => Assert.True(parameter.ParameterType == typeof(int) || parameter.ParameterType == typeof(Guid)));
    }
    static OriginalLoginContext Login()
    {
        var issued = DateTime.UtcNow.AddMinutes(-1);
        return new(Guid.NewGuid(), 1040, 14, Guid.NewGuid(), issued, issued.AddHours(8), System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    }
    static (InvoiceNotificationClient Client, Handler Handler) Client(OriginalLoginContext? login, Func<HttpRequestMessage, Task<HttpResponseMessage>> response, bool enabled = true)
    {
        var context = new DefaultHttpContext();
        if (login is not null) context.Items[OriginalLoginSession.ValidatedContextItem] = login;
        var handler = new Handler(response);
        return (new(new HttpClient(handler) { BaseAddress = new("https://localhost:7444/") }, new HttpContextAccessor { HttpContext = context },
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["InvoiceNotifications:Enabled"] = enabled.ToString() }).Build()), handler);
    }

    [Fact]
    public async Task Only_validated_original_context_and_selectors_cross_the_boundary()
    {
        var login = Login(); var operation = Guid.NewGuid();
        var (client, handler) = Client(login, async request =>
        {
            Assert.Equal("/internal/notifications/invoices/send", request.RequestUri!.AbsolutePath);
            var json = await request.Content!.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(new[] { "invoiceId", "isInitial", "operationId", "session" }, doc.RootElement.EnumerateObject().Select(x => x.Name).Order().ToArray());
            var command = JsonSerializer.Deserialize<InvoiceNotificationCommand>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Equal(login.LoginId, command.Session.LoginId); Assert.Equal(login.BindingHash.ToArray(), command.Session.BindingHash);
            Assert.Equal(operation, command.OperationId); Assert.Equal(12, command.InvoiceId); Assert.False(command.IsInitial);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new InvoiceNotificationReceipt(operation, "SmtpAccepted")) };
        });
        Assert.Equal("SmtpAccepted", (await client.SendAsync(12, operation, false, default)).State);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Missing_or_disabled_context_never_calls_the_authority(bool disabled)
    {
        var (client, handler) = Client(null, _ => throw new Exception("Must not call"), !disabled);
        if (disabled) await Assert.ThrowsAsync<InvoiceNotificationTransportException>(() => client.SendAsync(1, Guid.NewGuid(), false, default));
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.SendAsync(1, Guid.NewGuid(), false, default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Lost_HTTP_response_is_never_automatically_replayed()
    {
        var (client, handler) = Client(Login(), _ => throw new HttpRequestException("Synthetic lost response"));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(5, Guid.NewGuid(), false, default));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Empty_receipt_selector_recovers_durable_original_UUID_without_sending()
    {
        var operation = Guid.NewGuid();
        var (client, handler) = Client(Login(), async request =>
        {
            Assert.Equal("/internal/notifications/invoices/receipt", request.RequestUri!.AbsolutePath);
            Assert.Equal(Guid.Empty, (await request.Content!.ReadFromJsonAsync<InvoiceNotificationReceiptCommand>())!.OperationId);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new InvoiceNotificationReceipt(operation, "Unknown")) };
        });
        Assert.Equal(operation, (await client.ReceiptAsync(12, Guid.Empty, default)).OperationId);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("None")] [InlineData("Delivered")] [InlineData("Anything")]
    public async Task Send_rejects_empty_or_unapproved_receipt_states(string state)
    {
        var operation = Guid.NewGuid();
        var (client, _) = Client(Login(), _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = JsonContent.Create(new InvoiceNotificationReceipt(operation, state)) }));
        await Assert.ThrowsAsync<InvoiceNotificationTransportException>(() => client.SendAsync(12, operation, false, default));
    }

    [Theory]
    [InlineData(403)] [InlineData(404)] [InlineData(409)] [InlineData(503)]
    public async Task Rejections_preserve_numeric_status_and_do_not_retry(int status)
    {
        var (client, handler) = Client(Login(), _ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        Assert.Equal(status, (await Assert.ThrowsAsync<InvoiceNotificationTransportException>(() => client.ReceiptAsync(1, Guid.NewGuid(), default))).Status);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void Unsubmitted_attempt_survives_refresh_but_not_logout_or_new_original_login()
    {
        var session = new MemorySession(); var login = Login();
        var operation = InvoiceNotificationAttempt.ForInvoice(session, login, 1);
        Assert.Equal(operation, InvoiceNotificationAttempt.ForInvoice(session, login, 1));
        Assert.True(InvoiceNotificationAttempt.Matches(session, login, 1, operation));
        var anotherLogin = login with { LoginId = Guid.NewGuid() };
        Assert.Null(InvoiceNotificationAttempt.Peek(session, anotherLogin, 1));
        Assert.False(InvoiceNotificationAttempt.Matches(session, anotherLogin, 1, operation));
        Assert.NotEqual(operation, InvoiceNotificationAttempt.ForInvoice(session, anotherLogin, 1));
        session.Clear(); Assert.Null(InvoiceNotificationAttempt.Peek(session, login, 1));
    }

    sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return response(request); }
    }
    sealed class MemorySession : ISession
    {
        readonly Dictionary<string, byte[]> values = [];
        public string Id => "invoice-test"; public bool IsAvailable => true; public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear(); public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
