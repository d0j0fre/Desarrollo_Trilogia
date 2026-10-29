using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class LegacyBudgetArchiveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Monthly_archive_requires_the_middleware_validated_login_before_SQL(bool hasHttp)
    {
        // An unusable connection string makes accidental SQL access fail differently.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = "not a connection string" }).Build();
        var accessor = hasHttp ? new HttpContextAccessor { HttpContext = new DefaultHttpContext() } : null;
        var service = new ExpensesDbService(configuration, accessor);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAccountsAsync());
    }
}
