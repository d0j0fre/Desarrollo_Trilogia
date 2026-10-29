using Microsoft.Extensions.Configuration;
using Moq;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class PersonalChatNavigationTests
{
    [Theory]
    [InlineData(true, true, "Empleado", "ChatV2")]
    [InlineData(false, true, "Empleado", "Home")]
    [InlineData(true, false, "Empleado", "Home")]
    [InlineData(true, true, "Cliente", "Home")]
    public async Task Chat_only_internal_account_gets_a_usable_login_destination(
        bool enabled, bool permitted, string role, string expected)
    {
        var permissions = new Mock<IRolePermissionService>();
        permissions.Setup(x => x.GetPermissionCodesAsync(role)).ReturnsAsync(
            new HashSet<string>(permitted ? new[] { "CHAT_USAR" } : Array.Empty<string>()));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ChatV2:PersonalSqlEnabled"] = enabled.ToString() }).Build();
        var resolver = new WorkspaceResolver(permissions.Object, new Mock<IEmployeeRelationshipService>().Object, configuration);
        Assert.Equal(expected, (await resolver.ResolveAsync(42, role)).Controller);
    }
}
