using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using JJ.Commerce;

namespace Proyecto_Final.Services;

public interface ICommercialQuoteClient
{
    Task<ServerQuoteResult> IssueAsync(SellerQuoteIntent intent, CancellationToken cancellationToken);
}
public sealed class CommercialQuoteTransportException(int status) : Exception("La autoridad comercial no confirmó la operación.")
{
    public int Status { get; } = status;
}
public sealed class DisabledCommercialQuoteClient : ICommercialQuoteClient
{
    public Task<ServerQuoteResult> IssueAsync(SellerQuoteIntent intent, CancellationToken cancellationToken) =>
        throw new CommercialQuoteTransportException(503);
}

public sealed class CommercialClientCertificate : IDisposable
{
    public X509Certificate2 Certificate { get; }
    public CommercialClientCertificate(IConfiguration configuration)
    {
        var path = configuration["Commerce:ClientCertificatePath"];
        var pin = configuration["Commerce:ClientCertificateSha256"];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)
            || pin is not { Length: 64 } || !pin.All(Uri.IsHexDigit)
            || string.IsNullOrWhiteSpace(configuration["Commerce:ClientCertificatePassword"]))
            throw new InvalidOperationException("Falta la identidad comercial privada de MVC.");
        Certificate = X509CertificateLoader.LoadPkcs12FromFile(path,
            configuration["Commerce:ClientCertificatePassword"],
            // Windows Schannel needs a user key container for client authentication.
            // Without PersistKeySet the temporary container is removed on Dispose;
            // no machine key store or exportable key is requested.
            OperatingSystem.IsWindows() ? X509KeyStorageFlags.UserKeySet : X509KeyStorageFlags.EphemeralKeySet);
        if (!Certificate.HasPrivateKey || Certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow
            || Certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow
            || !CryptographicOperations.FixedTimeEquals(Certificate.GetCertHash(HashAlgorithmName.SHA256), Convert.FromHexString(pin))
            || !Certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>()
                .SelectMany(x => x.EnhancedKeyUsages.Cast<System.Security.Cryptography.Oid>()).Any(x => x.Value == "1.3.6.1.5.5.7.3.2"))
        {
            Certificate.Dispose();
            throw new InvalidOperationException("La identidad comercial privada no está acreditada.");
        }
    }
    public void Dispose() => Certificate.Dispose();
}

public sealed class CommercialQuoteClient(HttpClient http, IHttpContextAccessor accessor, IConfiguration configuration)
    : ICommercialQuoteClient
{
    public async Task<ServerQuoteResult> IssueAsync(SellerQuoteIntent intent, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Commerce:Enabled")) throw new CommercialQuoteTransportException(503);
        var context = OriginalLoginSession.ValidatedRequest(accessor.HttpContext
            ?? throw new UnauthorizedAccessException("Falta el contexto original validado."));
        var normalized = intent.Normalize();
        var session = new CommerceSession(context.LoginId, context.UserId, context.ProfileId, context.SecurityStamp,
            context.BindingHash.ToArray(), context.IssuedUtc, context.ExpiresUtc);
        using var response = await http.PostAsJsonAsync("internal/commerce/quotes", new ServerQuoteCommand(session, normalized), ct);
        if (!response.IsSuccessStatusCode) throw new CommercialQuoteTransportException((int)response.StatusCode);
        try
        {
        var quote = await response.Content.ReadFromJsonAsync<ServerQuoteResult>(cancellationToken: ct)
            ?? throw new CommercialQuoteTransportException(503);
        if (quote.RequestId != normalized.RequestId || quote.ExpiresUtc.Kind != DateTimeKind.Utc
            || quote.ExpiresUtc > context.ExpiresUtc || quote.ExpiresUtc <= DateTime.UtcNow
            || quote.CustomerRelationshipRevision <= 0
            || quote.SourceHash is not { Length: 64 } || !quote.SourceHash.All(Uri.IsHexDigit)
            || quote.IntentFingerprint is not { Length: 64 } || !quote.IntentFingerprint.All(Uri.IsHexDigit)
            || quote.Lines is null || quote.Money != ServerPricing.Calculate(quote.Lines)
            || !quote.Lines.Select(x => new OrderQuantity(x.Product, x.Quantity)).SequenceEqual(normalized.Items))
            throw new CommercialQuoteTransportException(503);
        return quote;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or OverflowException or NotSupportedException)
        {
            // A malformed authority response is an unavailable dependency, not a bad browser request.
            throw new CommercialQuoteTransportException(503);
        }
    }
}
