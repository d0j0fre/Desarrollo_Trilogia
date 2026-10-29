using Microsoft.Data.SqlClient;
using Proyecto_Final.Services.ChatV2.Broker;

namespace Proyecto_Final.Services.ChatV2;

/// <summary>Offline configuration boundary. Effective SQL grants still require a deployment preflight.</summary>
public static class ChatV2SqlConfiguration
{
    public static void Validate(IConfiguration configuration)
    {
        bool broker = configuration.GetValue<bool>("ChatV2:BrokerEnabled");
        if (configuration.GetValue<bool>("ChatV2:PersonalSignalREnabled") && (!broker || !configuration.GetValue<bool>("ChatV2:PersonalSqlEnabled"))) throw Invalid();
        if (!configuration.GetValue<bool>("ChatV2:PersonalSqlEnabled"))
        { if (broker) throw Invalid(); return; }
        var app = Read(broker ? "DefaultConnection" : "ChatV2App");
        var reader = Read("ChatV2Reader");
        var shared = Read("DefaultConnection");
        var identities = new[] { app, reader };
        if (!configuration.GetValue<bool>("OriginalLogin:RemoteAuthority")
            || !configuration.GetValue<bool>("ChatV2:LegacyReadOnly")
            || !string.IsNullOrWhiteSpace(configuration.GetConnectionString("OriginalLoginIssuer"))
            || identities.Select(x => x.UserID).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 2)
            throw Invalid();
        if (!Uri.TryCreate(configuration["ApiSettings:BaseUrl"], UriKind.Absolute, out var authority)
            || authority.Scheme != Uri.UriSchemeHttps || !authority.IsLoopback)
            throw Invalid();
        if (!identities.Append(shared).All(x =>
            string.Equals(x.DataSource, app.DataSource, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.InitialCatalog, app.InitialCatalog, StringComparison.OrdinalIgnoreCase)))
            throw Invalid();
        // Do not enable Chat while the surrounding MVC composition silently retains the Windows owner.
        // Moving the shared identity requires reviewing historical-module grants first.
        if (!string.Equals(shared.UserID, app.UserID, StringComparison.OrdinalIgnoreCase)) throw Invalid();
        if (broker)
        {
            if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("ChatV2App"))) throw Invalid();
            ChatV2BrokerConfiguration.Validate(configuration);
        }

        SqlConnectionStringBuilder Read(string name)
        {
            var value = configuration.GetConnectionString(name);
            if (string.IsNullOrWhiteSpace(value)) throw Invalid();
            SqlConnectionStringBuilder settings;
            try { settings = new(value); }
            catch (ArgumentException) { throw Invalid(); }
            if (settings.IntegratedSecurity || string.IsNullOrWhiteSpace(settings.UserID) ||
                string.IsNullOrWhiteSpace(settings.Password) || string.IsNullOrWhiteSpace(settings.DataSource) ||
                string.IsNullOrWhiteSpace(settings.InitialCatalog) || settings.PersistSecurityInfo ||
                settings.UserID.Equals("sa", StringComparison.OrdinalIgnoreCase)) throw Invalid();
            return settings;
        }
    }
    private static InvalidOperationException Invalid() => new(
        "Chat V2 SQL requiere identidades técnicas separadas y un destino coherente. Complete el gate de privilegios antes de habilitarlo.");
}
