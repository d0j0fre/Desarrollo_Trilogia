using Microsoft.Data.SqlClient;

namespace Proyecto_FinalAPI.Services;

/// <summary>Validate the fixed local destination and separate SQL identity before opening a connection.</summary>
public static class PaySlipNotificationConfiguration
{
    public static void Validate(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("PaySlipNotifications:Enabled")) return;
        if (!configuration.GetValue<bool>("Commerce:Enabled")) throw Invalid();
        var authority = Connection(configuration);
        foreach (string key in new[] { "DefaultConnection", "OriginalLoginIssuer", "CommercialPricingAuthority",
            "InvoiceNotificationAuthority", "DeliveryNotificationAuthority", "RouteDispatchNotificationAuthority" })
        {
            string? raw = configuration.GetConnectionString(key);
            if (!string.IsNullOrWhiteSpace(raw)
                && new SqlConnectionStringBuilder(raw).UserID.Equals(authority.UserID, StringComparison.OrdinalIgnoreCase))
                throw Invalid();
        }
    }

    public static SqlConnectionStringBuilder Connection(IConfiguration configuration)
    {
        string? raw = configuration.GetConnectionString("PaySlipNotificationAuthority");
        if (string.IsNullOrWhiteSpace(raw)) throw Invalid();
        var value = new SqlConnectionStringBuilder(raw);
        string? user = configuration["PaySlipNotifications:ExpectedSqlUser"];
        string? source = configuration["PaySlipNotifications:ExpectedDataSource"];
        string? database = configuration["PaySlipNotifications:ExpectedDatabase"];
        if (value.IntegratedSecurity || value.PersistSecurityInfo || string.IsNullOrWhiteSpace(value.Password)
            || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(database)
            || !value.UserID.Equals(user, StringComparison.Ordinal) || value.UserID.Equals("sa", StringComparison.OrdinalIgnoreCase)
            || !value.DataSource.Equals(source, StringComparison.Ordinal) || !value.InitialCatalog.Equals(database, StringComparison.Ordinal)
            || !(value.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase)
                || value.DataSource.StartsWith("np:\\\\.\\pipe\\LOCALDB#", StringComparison.OrdinalIgnoreCase))) throw Invalid();
        value.Enlist = false;
        value.ConnectRetryCount = 0;
        value.ConnectTimeout = 5;
        return value;
    }

    private static InvalidOperationException Invalid() => new("Pay slip notifications require a separate restricted authority.");
}
