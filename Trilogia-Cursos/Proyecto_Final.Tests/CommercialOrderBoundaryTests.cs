using JJ.Commerce;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class CommercialOrderBoundaryTests
{
    private static SellerOrderCreateViewModel Draft() => new()
    {
        RequestId = Guid.NewGuid(), ClienteUsuarioId = 2, TipoEntrega = "Vendedor", DireccionEntrega = "QA",
        Productos = [new() { ProductoId = 1, Cantidad = 1 }]
    };
    private static CommercialOrderService Service(bool enabled, bool login, ICommercialQuoteClient prices)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Commerce:Enabled"] = enabled.ToString(), ["Commerce:OrdersEnabled"] = enabled.ToString(),
            ["ConnectionStrings:DefaultConnection"] = "not a connection string"
        }).Build();
        var context = new DefaultHttpContext();
        if (login)
        {
            var issued = DateTime.UtcNow.AddMinutes(-1);
            context.Items[OriginalLoginSession.ValidatedContextItem] = new OriginalLoginContext(Guid.NewGuid(), 10, 3,
                Guid.NewGuid(), issued, issued.AddHours(8), System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        }
        return new(config, new HttpContextAccessor { HttpContext = context }, prices);
    }
    [Fact]
    public async Task Disabled_commerce_cannot_fall_back_to_legacy_or_contact_pricing()
    {
        var prices = new Mock<ICommercialQuoteClient>(MockBehavior.Strict);
        await Assert.ThrowsAsync<CommercialAssignmentsUnavailableException>(() => Service(false, false, prices.Object).CreateAsync(Draft(), true, default));
        prices.VerifyNoOtherCalls();
    }
    [Theory]
    [InlineData("clients")]
    [InlineData("history")]
    [InlineData("detail")]
    [InlineData("retained")]
    [InlineData("decision")]
    [InlineData("create")]
    public async Task Missing_original_request_is_denied_before_SQL_or_pricing(string operation)
    {
        var prices = new Mock<ICommercialQuoteClient>(MockBehavior.Strict);
        var service = Service(true, false, prices.Object);
        async Task Invoke()
        {
            switch (operation)
            {
                case "clients": await service.ClientsAsync(default); break;
                case "history": await service.HistoryAsync(default); break;
                case "detail": await service.DetailsAsync(1, false, default); break;
                case "retained": await service.RetainedAsync(null, default); break;
                case "decision": await service.DecideAsync(1, Guid.NewGuid(), 1, true, null, default); break;
                default: await service.CreateAsync(Draft(), true, default); break;
            }
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(Invoke);
        prices.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task Online_order_requires_the_reviewed_quote_and_cannot_post_only_browser_prices()
    {
        var prices = new Mock<ICommercialQuoteClient>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(true, true, prices.Object).CreateAsync(Draft(), false, default));
        prices.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task Changed_quote_provenance_is_rejected_before_SQL_consumption()
    {
        var draft = Draft(); draft.QuoteFingerprint = new string('A', 64); draft.QuoteSourceHash = new string('B', 64);
        var lines = new[] { new PricedQuantity(1, 1, 100m, 0m, "QA") };
        var quote = new ServerQuoteResult(draft.RequestId, DateTime.UtcNow.AddMinutes(5), 1,
            new string('C', 64), new string('A', 64), lines, ServerPricing.Calculate(lines));
        var prices = new Mock<ICommercialQuoteClient>();
        prices.Setup(x => x.IssueAsync(It.IsAny<SellerQuoteIntent>(), It.IsAny<CancellationToken>())).ReturnsAsync(quote);
        var error = await Assert.ThrowsAsync<CommercialQuoteTransportException>(() => Service(true, true, prices.Object).CreateAsync(draft, false, default));
        Assert.Equal(409, error.Status);
    }
    [Fact]
    public async Task Unknown_pricing_result_keeps_the_original_UUID_and_payload()
    {
        var draft = Draft(); draft.QuoteFingerprint = new string('A', 64); draft.QuoteSourceHash = new string('B', 64);
        var request = draft.RequestId;
        var prices = new Mock<ICommercialQuoteClient>();
        prices.Setup(x => x.IssueAsync(It.IsAny<SellerQuoteIntent>(), It.IsAny<CancellationToken>())).ThrowsAsync(new CommercialQuoteTransportException(503));
        await Assert.ThrowsAsync<CommercialQuoteTransportException>(() => Service(true, true, prices.Object).CreateAsync(draft, false, default));
        Assert.Equal(request, draft.RequestId); Assert.Equal(1, draft.Productos.Single().Cantidad); Assert.Equal("QA", draft.DireccionEntrega);
    }
}
