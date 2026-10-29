using Microsoft.Extensions.Configuration;
using Proyecto_Final.Services;
using Proyecto_Final.Services.ChatV2;

namespace Proyecto_Final.Tests;

public sealed class LegacyPersonalArchiveTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("Server=invalid;Database=synthetic;Integrated Security=true")]
    [InlineData("Server=invalid;Database=synthetic;User ID=sa;Password=<TEST_ONLY>")]
    public async Task Missing_or_owner_configuration_fails_before_any_connection(string? value)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["ConnectionStrings:ChatV2Reader"] = value }).Build();
        var reader = new SqlLegacyPersonalArchive(config);
        var login = new OriginalLoginContext(Guid.NewGuid(), 1, 1, Guid.NewGuid(),
            DateTime.UtcNow, DateTime.UtcNow.AddHours(8), new byte[32]);
        var error = await Assert.ThrowsAsync<ChatV2Exception>(() => reader.ListAsync(login, CancellationToken.None));
        Assert.Equal(ChatV2Failure.Dependency, error.Failure);
    }
}
