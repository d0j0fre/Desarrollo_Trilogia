namespace Proyecto_Final.Tests;

// Seguridad / datos privados sin conexión: lo que la venta móvil y el chofer guardan en el navegador
// queda aislado por usuario, vence y se borra al cerrar sesión (antes eran claves globales sin dueño ni vencimiento).
public sealed class OfflineStoragePrivacyTests
{
    private static readonly string[] Consumers =
    [
        Path.Combine("Proyecto_Final", "wwwroot", "js", "driver-deliveries.js"),
        Path.Combine("Proyecto_Final", "Views", "DriverDeliveries", "Route.cshtml"),
        Path.Combine("Proyecto_Final", "Views", "SellerOrders", "Index.cshtml"),
    ];

    [Fact]
    public void SecureStorage_ScopesEveryKeyByUserAndExpiresEnvelopes()
    {
        var js = File.ReadAllText(SourcePath("Proyecto_Final", "wwwroot", "js", "secure-offline-storage.js"));

        Assert.Contains("PREFIX + userId + \":\" + scope", js, StringComparison.Ordinal);
        Assert.Contains("envelope.ownerUserId !== userId", js, StringComparison.Ordinal);
        Assert.Contains("Date.now() >= envelope.expiresAt", js, StringComparison.Ordinal);
        // Sin usuario autenticado no se lee ni se escribe.
        Assert.Contains("if (userId <= 0) return null;", js, StringComparison.Ordinal);
        Assert.Contains("No existe un usuario autenticado", js, StringComparison.Ordinal);
    }

    [Fact]
    public void SecureStorage_PurgesLegacyGlobalKeysAndClearsOnLogout()
    {
        var js = File.ReadAllText(SourcePath("Proyecto_Final", "wwwroot", "js", "secure-offline-storage.js"));

        foreach (var legacy in new[] { "distribuidorajj_seller_offline_orders_v1", "distribuidorajj_seller_catalog_v1", "driverDeliveryQueue", "driverRoute_" })
            Assert.Contains(legacy, js, StringComparison.Ordinal);
        Assert.Contains("form[data-clear-private-storage='true']", js, StringComparison.Ordinal);
        Assert.Contains("clearCurrentUser", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Consumers_NoLongerTouchLocalStorageDirectly()
    {
        foreach (var consumer in Consumers)
        {
            var source = File.ReadAllText(SourcePath(consumer.Split(Path.DirectorySeparatorChar)));

            Assert.Contains("DistribuidoraJJOfflineStorage", source, StringComparison.Ordinal);
            Assert.DoesNotContain("localStorage.getItem", source, StringComparison.Ordinal);
            Assert.DoesNotContain("localStorage.setItem", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Layout_ExposesTheUserAndWiresTheLogoutClear()
    {
        var layout = File.ReadAllText(SourcePath("Proyecto_Final", "Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("data-authenticated-user-id=", layout, StringComparison.Ordinal);
        Assert.Contains("~/js/secure-offline-storage.js", layout, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Logout\" method=\"post\" class=\"m-0\" data-clear-private-storage=\"true\"", layout, StringComparison.Ordinal);
    }

    private static string SourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
        return Path.Combine([root, .. parts]);
    }
}
