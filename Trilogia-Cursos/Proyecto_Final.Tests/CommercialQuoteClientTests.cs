using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JJ.Commerce;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Proyecto_Final.Controllers;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class CommercialQuoteClientTests
{
    private static IConfiguration Config(bool enabled = true) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Commerce:Enabled"] = enabled.ToString() }).Build();
    private static SellerQuoteIntent Intent() => new(Guid.NewGuid(), 2, "Vendedor", "QA", null,
        null, "Venta móvil", [new(1, 2)]);
    private static OriginalLoginContext Login()
    {
        var issued = DateTime.UtcNow.AddMinutes(-1);
        return new(Guid.NewGuid(), 10, 3, Guid.NewGuid(), issued, issued.AddHours(8),
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    }
    private static (CommercialQuoteClient Client, Handler Transport) Client(OriginalLoginContext? login,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> respond, bool enabled = true)
    {
        var context = new DefaultHttpContext();
        if (login is not null) context.Items[OriginalLoginSession.ValidatedContextItem] = login;
        var handler = new Handler(respond);
        return (new(new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") },
            new HttpContextAccessor { HttpContext = context }, Config(enabled)), handler);
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return respond(request); }
    }
    [Fact]
    public async Task Disabled_or_unvalidated_context_does_not_contact_the_authority()
    {
        Task<HttpResponseMessage> Unexpected(HttpRequestMessage _) => throw new InvalidOperationException();
        var disabled = Client(null, Unexpected, false);
        Assert.Equal(503, (await Assert.ThrowsAsync<CommercialQuoteTransportException>(() => disabled.Client.IssueAsync(Intent(), default))).Status);
        Assert.Equal(0, disabled.Transport.Calls);
        var missing = Client(null, Unexpected);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => missing.Client.IssueAsync(Intent(), default));
        Assert.Equal(0, missing.Transport.Calls);
    }
    [Fact]
    public async Task Sends_original_context_and_quantities_and_checks_the_authority_result()
    {
        var login = Login();
        var intent = Intent();
        var client = Client(login, async request =>
        {
            var command = await request.Content!.ReadFromJsonAsync<ServerQuoteCommand>();
            Assert.Equal(login.LoginId, command!.Session.LoginId);
            Assert.Equal(login.UserId, command.Session.UserId);
            Assert.Equal(login.BindingHash.ToArray(), command.Session.BindingHash);
            Assert.Equal(intent.Items, command.Intent.Items);
            IReadOnlyList<PricedQuantity> lines = [new(1, 2, 100m, 0m, "QA")];
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new ServerQuoteResult(intent.RequestId,
                DateTime.UtcNow.AddMinutes(10), 1, new string('A', 64), new string('B', 64), lines, ServerPricing.Calculate(lines))) };
        });
        Assert.Equal(226m, (await client.Client.IssueAsync(intent, default)).Money.FinalTotal);
        Assert.Equal(1, client.Transport.Calls);
    }
    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"lines\":[]}")]
    public async Task Invalid_authority_contract_is_503_not_a_browser_validation_error(string body)
    {
        var client = Client(Login(), _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
        Assert.Equal(503, (await Assert.ThrowsAsync<CommercialQuoteTransportException>(() => client.Client.IssueAsync(Intent(), default))).Status);
    }
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(409)]
    public async Task Preserves_authority_rejection_without_reading_private_error_bodies(int status)
    {
        var client = Client(Login(), _ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        Assert.Equal(status, (await Assert.ThrowsAsync<CommercialQuoteTransportException>(() => client.Client.IssueAsync(Intent(), default))).Status);
    }
    [Fact]
    public void Browser_contract_rejects_invented_actor_price_and_channel()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        const string draft = """{"requestId":"10000000-0000-0000-0000-000000000001","customer":2,"delivery":"Vendedor","address":"QA","items":[{"product":1,"quantity":1}]}""";
        foreach (var field in new[] { "\"actor\":1", "\"price\":1", "\"channel\":\"otro\"" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SellerQuoteDraft>(draft[..^1] + "," + field + "}", options));
    }
}
