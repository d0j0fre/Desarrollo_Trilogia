using Microsoft.Data.SqlClient;

namespace Proyecto_FinalAPI.Services;

public static class InvoiceNotificationConfiguration
{
    public static void Validate(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("InvoiceNotifications:Enabled")) return;
        if (!configuration.GetValue<bool>("Commerce:Enabled")) throw Invalid();
        var identity = Connection(configuration);
        foreach (var name in new[] { "DefaultConnection", "OriginalLoginIssuer", "CommercialPricingAuthority" })
        {
            var other = configuration.GetConnectionString(name);
            if (!string.IsNullOrWhiteSpace(other) && new SqlConnectionStringBuilder(other).UserID.Equals(identity.UserID, StringComparison.OrdinalIgnoreCase)) throw Invalid();
        }
    }
    public static SqlConnectionStringBuilder Connection(IConfiguration configuration)
    {
        var raw = configuration.GetConnectionString("InvoiceNotificationAuthority");
        if (string.IsNullOrWhiteSpace(raw)) throw Invalid();
        var value = new SqlConnectionStringBuilder(raw);
        var expected = configuration["InvoiceNotifications:ExpectedSqlUser"];
        var expectedSource = configuration["InvoiceNotifications:ExpectedDataSource"];
        var expectedDatabase = configuration["InvoiceNotifications:ExpectedDatabase"];
        if (value.IntegratedSecurity || value.PersistSecurityInfo
            || string.IsNullOrWhiteSpace(value.InitialCatalog) || string.IsNullOrWhiteSpace(value.DataSource)
            || string.IsNullOrWhiteSpace(expectedSource) || string.IsNullOrWhiteSpace(expectedDatabase)
            || value.DataSource != expectedSource || value.InitialCatalog != expectedDatabase
            || !(value.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase)
                || value.DataSource.StartsWith("np:\\\\.\\pipe\\LOCALDB#", StringComparison.OrdinalIgnoreCase))
            || string.IsNullOrWhiteSpace(value.Password) || string.IsNullOrWhiteSpace(expected)
            || value.UserID != expected || value.UserID.Equals("sa", StringComparison.OrdinalIgnoreCase)) throw Invalid();
        // A durable SMTP claim must never be enlisted in an ambient transaction.
        value.Enlist = false;
        value.ConnectRetryCount = 0; value.ConnectTimeout = 5;
        return value;
    }
    private static InvalidOperationException Invalid() => new("Invoice notifications require a separate restricted authority.");
}
