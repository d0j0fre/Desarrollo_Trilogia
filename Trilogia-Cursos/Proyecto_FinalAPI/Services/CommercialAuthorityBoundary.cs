using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient;

namespace Proyecto_FinalAPI.Services;

public static class CommercialAuthorityBoundary
{
    public static void ValidateConfiguration(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Commerce:Enabled")) return;
        foreach (var key in new[] { "Commerce:ClientCertificateSha256", "Commerce:AuthorityRootSha256" })
            if (!IsHash(configuration[key])) throw new InvalidOperationException("Falta la identidad TLS comercial acreditada.");
        var pricing = Restricted(configuration.GetConnectionString("CommercialPricingAuthority"));
        foreach (var key in new[] { "DefaultConnection", "OriginalLoginIssuer" })
        {
            var other = Restricted(configuration.GetConnectionString(key));
            if (pricing.UserID.Equals(other.UserID, StringComparison.OrdinalIgnoreCase)
                || !pricing.DataSource.Equals(other.DataSource, StringComparison.OrdinalIgnoreCase)
                || !pricing.InitialCatalog.Equals(other.InitialCatalog, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La autoridad de precios requiere una identidad SQL propia en el mismo destino.");
        }
    }
    public static Task<bool> IsAuthorizedAsync(HttpContext context, IConfiguration configuration, DateTime now)
        => IsAuthorizedAsync(context, configuration, now, CancellationToken.None);

    public static async Task<bool> IsAuthorizedAsync(HttpContext context, IConfiguration configuration, DateTime now, CancellationToken ct)
    {
        if (!context.Request.IsHttps || context.Connection.RemoteIpAddress is not IPAddress address || !IPAddress.IsLoopback(address))
            return false;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, ct);
        cancellation.Token.ThrowIfCancellationRequested();
        // Bound the wait even if a TLS feature does not cooperate with its cancellation token.
        var certificate = await context.Connection.GetClientCertificateAsync(cancellation.Token).WaitAsync(cancellation.Token);
        cancellation.Token.ThrowIfCancellationRequested();
        return certificate is not null && IsAuthorizedCertificate(certificate, configuration, now);
    }
    public static bool IsAuthorizedCertificate(X509Certificate2 certificate, IConfiguration configuration, DateTime now)
    {
        if (!IsHash(configuration["Commerce:ClientCertificateSha256"]) || !IsHash(configuration["Commerce:AuthorityRootSha256"]))
            return false;
        if (certificate.NotBefore.ToUniversalTime() > now || certificate.NotAfter.ToUniversalTime() <= now
            || !CryptographicOperations.FixedTimeEquals(certificate.GetCertHash(HashAlgorithmName.SHA256),
                Convert.FromHexString(configuration["Commerce:ClientCertificateSha256"]!))) return false;
        if (!certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>()
            .SelectMany(x => x.EnhancedKeyUsages.Cast<Oid>()).Any(x => x.Value == "1.3.6.1.5.5.7.3.2")) return false;
        using var chain = new X509Chain();
        // Private local CA has no online CRL. Revocation is the explicit per-request pin,
        // not a global trust-store exception or acceptance of an untrusted certificate.
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime = now;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.2"));
        return chain.Build(certificate) && chain.ChainElements.Count > 1
            && CryptographicOperations.FixedTimeEquals(chain.ChainElements[^1].Certificate.GetCertHash(HashAlgorithmName.SHA256),
                Convert.FromHexString(configuration["Commerce:AuthorityRootSha256"]!));
    }
    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private static SqlConnectionStringBuilder Restricted(string? value)
    {
        var parsed = new SqlConnectionStringBuilder(value ?? "");
        if (parsed.IntegratedSecurity || parsed.PersistSecurityInfo || string.IsNullOrWhiteSpace(parsed.UserID)
            || parsed.UserID.Equals("sa", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(parsed.Password)
            || string.IsNullOrWhiteSpace(parsed.DataSource) || string.IsNullOrWhiteSpace(parsed.InitialCatalog))
            throw new InvalidOperationException("Identidad comercial restringida ausente.");
        return parsed;
    }
}
