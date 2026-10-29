using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using JJ.Commerce;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Proyecto_FinalAPI.Services;

namespace Proyecto_Final.Tests;

public sealed class CommercialQuoteBoundaryTests
{
    [Fact]
    public void Empty_promotions_must_be_explicit_and_versioned()
    {
        const string valid = """{"evaluationDate":"2026-10-19","segment":"Minorista","revision":1,"sourceVersion":2,"products":[{"product":1,"quantity":1,"name":"QA","price":0.05}],"promotions":[]}""";
        Assert.Empty(QuoteSourceSnapshot.Parse(valid).Promotions);
        Assert.Throws<InvalidOperationException>(() => QuoteSourceSnapshot.Parse(valid.Replace(",\"promotions\":[]", "")));
        Assert.Throws<InvalidOperationException>(() => QuoteSourceSnapshot.Parse(valid.Replace("\"promotions\":[]", "\"promotions\":null")));
        Assert.Throws<InvalidOperationException>(() => QuoteSourceSnapshot.Parse(valid.Replace("\"sourceVersion\":2", "\"sourceVersion\":1")));
    }

    [Fact]
    public async Task No_client_certificate_never_authorizes_the_internal_endpoint()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        Assert.False(await CommercialAuthorityBoundary.IsAuthorizedAsync(context, new ConfigurationBuilder().Build(), DateTime.UtcNow));
    }

    [Fact]
    public void Pinned_self_signed_certificate_does_not_replace_a_trusted_chain()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=QA untrusted client", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.2") }, true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Commerce:ClientCertificateSha256"] = certificate.GetCertHashString(HashAlgorithmName.SHA256),
            ["Commerce:AuthorityRootSha256"] = certificate.GetCertHashString(HashAlgorithmName.SHA256)
        }).Build();
        Assert.False(CommercialAuthorityBoundary.IsAuthorizedCertificate(certificate, configuration, DateTime.UtcNow));
        Assert.False(CommercialAuthorityBoundary.IsAuthorizedCertificate(certificate, configuration, DateTime.UtcNow.AddHours(1)));
    }

    [Fact]
    public void Client_prices_and_actor_fields_are_rejected_by_the_intent_contract()
    {
        const string intent = """{"requestId":"10000000-0000-0000-0000-000000000001","customer":1,"delivery":"Vendedor","address":"QA","notes":null,"identification":null,"channel":"Venta móvil","items":[{"product":1,"quantity":1,"price":0.01}]}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SellerQuoteIntent>(intent, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SellerQuoteIntent>(intent.Replace("\"price\":0.01", "\"actor\":1"), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public async Task Expired_original_session_is_rejected_before_opening_SQL()
    {
        var authority = new CommercialQuoteAuthority(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:CommercialPricingAuthority"] = "not a connection string" }).Build(), TimeProvider.System);
        var expired = new CommerceSession(Guid.NewGuid(), 1, 1, Guid.NewGuid(), RandomNumberGenerator.GetBytes(32), DateTime.UtcNow.AddHours(-9), DateTime.UtcNow.AddHours(-1));
        var command = new ServerQuoteCommand(expired, new(Guid.NewGuid(), 2, "Vendedor", "QA", null, null, "Venta móvil", [new(1, 1)]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authority.IssueAsync(command, default));
    }

    [Fact]
    public void Intent_normalizes_quantities_and_rejects_schema_truncation()
    {
        var intent = new SellerQuoteIntent(Guid.NewGuid(), 2, " Vendedor ", " QA ", null, "1-2468-5678", "Venta móvil", [new(2, 1), new(1, 2), new(2, 3)]);
        var normalized = intent.Normalize();
        Assert.Equal(new[] { new OrderQuantity(1, 2), new OrderQuantity(2, 4) }, normalized.Items);
        Assert.Equal("124685678", normalized.Identification);
        Assert.Throws<ArgumentException>(() => (intent with { Address = new string('a', 256) }).Normalize());
    }
}
