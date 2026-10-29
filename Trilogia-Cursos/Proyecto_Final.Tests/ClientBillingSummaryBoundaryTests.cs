using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class ClientBillingSummaryBoundaryTests
{
    [Fact]
    public async Task Unavailable_contract_does_not_substitute_order_totals_for_invoices()
    {
        var service = new ClientBillingSummaryService(new ConfigurationBuilder().Build(), new HttpContextAccessor());
        Assert.Null(await service.ReadOwnAsync(default));
    }

    [Fact]
    public async Task Missing_original_login_is_rejected_before_parsing_SQL_credentials()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Commerce:OrdersEnabled"] = "true",
            ["ConnectionStrings:DefaultConnection"] = "invalid connection string"
        }).Build();
        var service = new ClientBillingSummaryService(configuration, new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadOwnAsync(default));
    }
}
