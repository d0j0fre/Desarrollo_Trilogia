using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class CommercialRelationsTests
{
    [Theory]
    [InlineData("read")]
    [InlineData("SC")]
    [InlineData("SS")]
    public async Task Missing_original_login_is_rejected_before_SQL(string operation)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = "not a connection string" }).Build();
        var service = new CommercialRelationsService(configuration,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, new BusinessClock(TimeProvider.System));
        if (operation == "read") await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadAsync(1, default));
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(operation, new(), default));
    }

    [Theory]
    [InlineData(2028, 2, 29, 2028, 3, 1)]
    [InlineData(2026, 12, 31, 2027, 1, 1)]
    public void Relationship_date_ends_at_the_next_Costa_Rica_midnight(int year, int month, int day,
        int nextYear, int nextMonth, int nextDay)
    {
        var clock = new BusinessClock(TimeProvider.System);
        var date = new DateTime(year, month, day);
        var until = clock.EndOfDayExclusiveUtc(date);
        Assert.Equal(new DateTime(nextYear, nextMonth, nextDay, 6, 0, 0, DateTimeKind.Utc), until);
        Assert.Equal(date, clock.ToLocalDate(until.AddTicks(-1)));
    }
}
