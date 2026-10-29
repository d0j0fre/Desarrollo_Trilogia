using System.Data;
using JJ.Commerce;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Proyecto_FinalAPI.Services;

namespace Proyecto_Final.Tests;

public sealed class PaySlipNotificationStoreTests
{
    private static CommerceSession Session()
    {
        var issued = DateTime.UtcNow.AddMinutes(-1);
        return new(Guid.NewGuid(), 1040, 4, Guid.NewGuid(), Enumerable.Repeat((byte)7, 32).ToArray(), issued, issued.AddHours(8));
    }

    private static IConfiguration Config(params (string Key, string? Value)[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["PaySlipNotifications:Enabled"] = "true", ["Commerce:Enabled"] = "true",
            ["PaySlipNotifications:ExpectedSqlUser"] = "qa-authority",
            ["PaySlipNotifications:ExpectedDataSource"] = @"(localdb)\QA",
            ["PaySlipNotifications:ExpectedDatabase"] = "QA",
            ["ConnectionStrings:PaySlipNotificationAuthority"] = @"Server=(localdb)\QA;Database=QA;User ID=qa-authority;Password=<TEST_ONLY>"
        };
        foreach (var (key, value) in overrides) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Theory]
    [InlineData("Request")][InlineData("Claim")][InlineData("Receipt")]
    public void Session_command_uses_precise_original_login_and_bigint(string operation)
    {
        var session = Session(); var id = Guid.NewGuid();
        using var connection = new SqlConnection();
        using var cmd = PaySlipNotificationStore.SessionCommand(connection, operation, session, (long)int.MaxValue + 8, id, DateTime.UtcNow);
        Assert.Equal("PaySlipNotifications." + operation, cmd.CommandText);
        Assert.Equal(SqlDbType.BigInt, cmd.Parameters["@CalculationId"].SqlDbType);
        Assert.Equal((long)int.MaxValue + 8, cmd.Parameters["@CalculationId"].Value);
        Assert.Equal(id, cmd.Parameters["@OperationId"].Value);
        Assert.Equal(SqlDbType.Binary, cmd.Parameters["@BindingHash"].SqlDbType);
        Assert.Equal(32, cmd.Parameters["@BindingHash"].Size);
        foreach (string name in new[] { "@IssuedUtc", "@ExpiresUtc" })
        { Assert.Equal(SqlDbType.DateTime2, cmd.Parameters[name].SqlDbType); Assert.Equal(7, cmd.Parameters[name].Scale); }
    }

    [Fact]
    public void Session_command_rejects_expired_or_invalid_context_and_selectors()
    {
        using var connection = new SqlConnection(); var valid = Session(); var now = DateTime.UtcNow;
        Assert.Throws<InvalidOperationException>(() => PaySlipNotificationStore.SessionCommand(connection, "Request", valid with { BindingHash = new byte[32] }, 1, Guid.NewGuid(), now));
        Assert.Throws<InvalidOperationException>(() => PaySlipNotificationStore.SessionCommand(connection, "Claim", valid, 0, Guid.NewGuid(), now));
        Assert.Throws<InvalidOperationException>(() => PaySlipNotificationStore.SessionCommand(connection, "Receipt", valid, 1, Guid.Empty, now));
        Assert.Throws<InvalidOperationException>(() => PaySlipNotificationStore.SessionCommand(connection, "Unapproved", valid, 1, Guid.NewGuid(), now));
        Assert.Throws<InvalidOperationException>(() => PaySlipNotificationStore.SessionCommand(connection, "Request", valid with { IssuedUtc = now.AddHours(-9), ExpiresUtc = now.AddHours(-1) }, 1, Guid.NewGuid(), now));
    }

    [Fact]
    public void Authority_is_restricted_pinned_and_not_enlisted()
    {
        PaySlipNotificationConfiguration.Validate(Config());
        var sql = PaySlipNotificationConfiguration.Connection(Config());
        Assert.False(sql.Enlist); Assert.Equal(0, sql.ConnectRetryCount); Assert.Equal(5, sql.ConnectTimeout);
        foreach (var pair in new[] {
            ("Commerce:Enabled", "false"), ("PaySlipNotifications:ExpectedSqlUser", "other"),
            ("PaySlipNotifications:ExpectedDatabase", "other"), ("PaySlipNotifications:ExpectedDataSource", @"(localdb)\Other"),
            ("ConnectionStrings:DefaultConnection", @"Server=(localdb)\QA;Database=QA;User ID=qa-authority;Password=<TEST_ONLY>"),
            ("ConnectionStrings:PaySlipNotificationAuthority", @"Server=external.invalid;Database=QA;User ID=qa-authority;Password=<TEST_ONLY>") })
            Assert.Throws<InvalidOperationException>(() => PaySlipNotificationConfiguration.Validate(Config(pair)));
    }
}
