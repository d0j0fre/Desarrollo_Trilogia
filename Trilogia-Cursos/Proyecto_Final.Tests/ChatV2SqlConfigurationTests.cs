using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Proyecto_Final.Services.ChatV2;

namespace Proyecto_Final.Tests;

// No SQL connections. Effective grants are verified separately in the restored composition.
public sealed class ChatV2SqlConfigurationTests
{
    [Fact] public void Personal_live_transport_requires_the_accredited_broker_composition()
    {
        var direct = Valid(); direct["ChatV2:PersonalSignalREnabled"] = "true";
        Assert.Throws<InvalidOperationException>(() => Validate(direct));
        var broker = Broker(); broker["ChatV2:PersonalSignalREnabled"] = "true"; Validate(broker);
    }
    private static Dictionary<string, string?> Valid()
    {
        string Setting(string user) => new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\OfflineOnly", InitialCatalog = "OfflineOnly",
            UserID = user, Password = "<runtime-secret>", PersistSecurityInfo = false
        }.ConnectionString;
        return new()
        {
            ["ChatV2:PersonalSqlEnabled"] = "true",
            ["ChatV2:LegacyReadOnly"] = "true",
            ["OriginalLogin:RemoteAuthority"] = "true",
            ["ApiSettings:BaseUrl"] = "https://localhost:5001",
            ["ConnectionStrings:DefaultConnection"] = Setting("app"),
            ["ConnectionStrings:ChatV2App"] = Setting("app"),
            ["ConnectionStrings:ChatV2Reader"] = Setting("reader")
        };
    }
    private static void Validate(Dictionary<string, string?> values) =>
        ChatV2SqlConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    private static Dictionary<string, string?> Broker()
    {
        var values = Valid(); values.Remove("ConnectionStrings:ChatV2App");
        values["ChatV2:BrokerEnabled"] = "true";
        values["ChatV2:BrokerPipe"] = "JJ-ChatV2-offline-test";
        values["ChatV2:AppWindowsSid"] = "S-1-5-21-111-222-333-1001";
        values["ChatV2:BrokerWindowsSid"] = "S-1-5-21-111-222-333-1002";
        return values;
    }
    [Fact] public void Broker_MVC_composition_has_operations_and_archive_but_no_chat_writer() => Validate(Broker());
    [Fact] public void Broker_cannot_silently_retain_the_direct_chat_writer_connection()
    {
        var values = Broker(); values["ConnectionStrings:ChatV2App"] = values["ConnectionStrings:DefaultConnection"];
        Assert.Throws<InvalidOperationException>(() => Validate(values));
    }
    [Theory]
    [InlineData("ChatV2:BrokerWindowsSid", "S-1-5-32-544")]
    [InlineData("ChatV2:BrokerWindowsSid", "S-1-5-21-111-222-333-1001")]
    [InlineData("ChatV2:BrokerWindowsSid", "S-1-5-21-444-555-666-1002")]
    [InlineData("ChatV2:BrokerWindowsSid", "S-1-5-21-9999999999-222-333-1002")]
    [InlineData("ChatV2:BrokerPipe", "\\\\other-host\\pipe\\unapproved")]
    [InlineData("ChatV2:BrokerPipe", "")]
    [InlineData("ChatV2:PersonalSqlEnabled", "false")]
    public void Unaccredited_broker_context_is_closed_before_connecting(string key, string value)
    {
        var values = Broker(); values[key] = value;
        Assert.Throws<InvalidOperationException>(() => Validate(values));
    }

    [Fact] public void Disabled_feature_preserves_existing_composition() => Validate(new());
    [Fact] public void Restricted_login_mappings_are_supported_without_containment() => Validate(Valid());
    [Fact] public void Windows_default_cannot_be_retained_behind_restricted_chat_connections()
    {
        var values = Valid();
        values["ConnectionStrings:DefaultConnection"] = "Data Source=(localdb)\\OfflineOnly;Initial Catalog=OfflineOnly;Integrated Security=True";
        Assert.Throws<InvalidOperationException>(() => Validate(values));
    }
    [Theory]
    [InlineData("ChatV2App")]
    [InlineData("ChatV2Reader")]
    [InlineData("DefaultConnection")]
    public void Missing_technical_configuration_stays_closed(string name)
    {
        var values = Valid(); values.Remove("ConnectionStrings:" + name);
        Assert.Throws<InvalidOperationException>(() => Validate(values));
    }
    [Fact] public void Reusing_the_app_identity_as_reader_is_rejected()
    {
        var values = Valid(); values["ConnectionStrings:ChatV2Reader"] = values["ConnectionStrings:ChatV2App"];
        Assert.Throws<InvalidOperationException>(() => Validate(values));
    }
    [Fact] public void Different_catalogs_are_rejected_before_connecting()
    {
        var values = Valid(); var reader = new SqlConnectionStringBuilder(values["ConnectionStrings:ChatV2Reader"]!) { InitialCatalog = "Other" };
        values["ConnectionStrings:ChatV2Reader"] = reader.ConnectionString;
        Assert.Throws<InvalidOperationException>(() => Validate(values));
    }
    [Fact] public void Shared_connection_must_use_the_reviewed_app_identity()
    {
        var values = Valid(); values["ConnectionStrings:DefaultConnection"] = values["ConnectionStrings:ChatV2Reader"];
        Assert.Throws<InvalidOperationException>(() => Validate(values));
    }
    [Fact] public void Issuer_secret_is_rejected_in_the_MVC_process()
    {
        var values = Valid(); values["ConnectionStrings:OriginalLoginIssuer"] = values["ConnectionStrings:ChatV2Reader"];
        Assert.Throws<InvalidOperationException>(() => Validate(values));
    }
    [Fact] public void Local_issuance_cannot_be_used_as_a_fallback()
    {
        var values = Valid(); values["OriginalLogin:RemoteAuthority"] = "false";
        Assert.Throws<InvalidOperationException>(() => Validate(values));
    }
    [Fact] public void Enabling_P_requires_the_independent_legacy_write_latch()
    {
        var values = Valid(); values["ChatV2:LegacyReadOnly"] = "false";
        Assert.Throws<InvalidOperationException>(() => Validate(values));
    }
    [Theory] [InlineData("http://localhost:5001")] [InlineData("https://unapproved.example.invalid")]
    public void Authority_transport_requires_HTTPS_on_the_approved_single_host(string uri)
    {
        var values = Valid(); values["ApiSettings:BaseUrl"] = uri;
        Assert.Throws<InvalidOperationException>(() => Validate(values));
    }
}
