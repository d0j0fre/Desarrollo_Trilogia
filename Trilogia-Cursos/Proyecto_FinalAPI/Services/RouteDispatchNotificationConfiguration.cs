using Microsoft.Data.SqlClient;

namespace Proyecto_FinalAPI.Services;

public static class RouteDispatchNotificationConfiguration
{
    public static void Validate(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("RouteDispatchNotifications:Enabled")) return;
        if (!configuration.GetValue<bool>("Commerce:Enabled")) throw Invalid();
        var own = Connection(configuration);
        foreach (var key in new[] { "DefaultConnection", "OriginalLoginIssuer", "CommercialPricingAuthority", "InvoiceNotificationAuthority", "DeliveryNotificationAuthority" })
        {
            var other = configuration.GetConnectionString(key);
            if (!string.IsNullOrWhiteSpace(other) && new SqlConnectionStringBuilder(other).UserID.Equals(own.UserID, StringComparison.OrdinalIgnoreCase)) throw Invalid();
        }
    }

    public static SqlConnectionStringBuilder Connection(IConfiguration configuration)
    {
        var raw = configuration["RouteDispatchNotifications:ConnectionString"];
        if (string.IsNullOrWhiteSpace(raw)) throw Invalid();
        var value = new SqlConnectionStringBuilder(raw);
        var user = configuration["RouteDispatchNotifications:ExpectedUser"];
        var source = configuration["RouteDispatchNotifications:ExpectedDataSource"];
        var database = configuration["RouteDispatchNotifications:ExpectedDatabase"];
        if (value.IntegratedSecurity || value.PersistSecurityInfo || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(source)
            || string.IsNullOrWhiteSpace(database) || value.UserID != user || value.DataSource != source || value.InitialCatalog != database
            || string.IsNullOrWhiteSpace(value.Password) || value.UserID.Equals("sa", StringComparison.OrdinalIgnoreCase)
            || !(value.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase)
                 || value.DataSource.StartsWith("np:\\\\.\\pipe\\LOCALDB#", StringComparison.OrdinalIgnoreCase))) throw Invalid();
        value.Enlist = false;
        value.ConnectRetryCount = 0;
        value.ConnectTimeout = 5;
        return value;
    }

    private static InvalidOperationException Invalid() => new("Route dispatch notifications require a separate restricted authority.");
}
