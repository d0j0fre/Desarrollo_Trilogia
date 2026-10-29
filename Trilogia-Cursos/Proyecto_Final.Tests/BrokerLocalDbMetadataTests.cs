using System.Security.Cryptography;
using System.Text;
using Proyecto_Final.Services.ChatV2.Broker;

namespace Proyecto_Final.Tests;

public sealed class BrokerLocalDbMetadataTests
{
    const string Pipe = @"\\.\pipe\LOCALDB#EXACT123\tsql\query";
    const string Owner = @"HOST\owner", Alias = @".\JJ_Approved";
    static string Hash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Pipe)));
    static string Native => "Nombre: Approved\r\r\nNombre compartido: JJ_Approved\r\r\nPropietario: HOST\\owner\r\r\nNombre de canalización de instancia: np:" + Pipe + "\r\r\n";
    [Fact] public void Restricted_shared_metadata_can_bind_only_the_bootstrap_pinned_endpoint_without_asserting_SQL_version()
        => BrokerLocalDbMetadata.VerifySharedEndpoint(Native, Owner, Alias, Hash);
    [Fact] public void A_different_owner_is_rejected() => Assert.Throws<InvalidOperationException>(() => BrokerLocalDbMetadata.VerifySharedEndpoint(Native, @"HOST\other", Alias, Hash));
    [Fact] public void A_different_shared_alias_is_rejected() => Assert.Throws<InvalidOperationException>(() => BrokerLocalDbMetadata.VerifySharedEndpoint(Native, Owner, @".\Other", Hash));
    [Fact] public void A_changed_named_pipe_is_rejected_before_connecting() => Assert.Throws<InvalidOperationException>(() => BrokerLocalDbMetadata.VerifySharedEndpoint(Native.Replace("EXACT123", "STALE123"), Owner, Alias, Hash));
    [Fact] public void An_absent_pipe_is_rejected() => Assert.Throws<InvalidOperationException>(() => BrokerLocalDbMetadata.VerifySharedEndpoint(Native.Replace(Pipe, ""), Owner, Alias, Hash));
    [Fact] public void An_unpinned_endpoint_is_rejected() => Assert.Throws<InvalidOperationException>(() => BrokerLocalDbMetadata.VerifySharedEndpoint(Native, Owner, Alias, ""));
}
