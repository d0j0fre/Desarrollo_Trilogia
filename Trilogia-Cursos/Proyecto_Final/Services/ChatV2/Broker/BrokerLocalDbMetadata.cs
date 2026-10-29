using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Proyecto_Final.Services.ChatV2.Broker;

internal static class BrokerLocalDbMetadata
{
    // Non-owner `info .\alias` omits version/state. Bootstrap pins the running endpoint; SQL must then verify its real version.
    internal static void VerifySharedEndpoint(string output, string owner, string alias, string expectedPipeHash)
    {
        string pipe = Regex.Match(output, @"\\\\\.\\pipe\\LOCALDB#[A-Za-z0-9]+\\tsql\\query").Value;
        bool ownerMatches = Regex.IsMatch(output, @"(?im)^\s*(Propietario|Owner):\s*" + Regex.Escape(owner) + @"\s*$");
        bool aliasMatches = alias.StartsWith(@".\", StringComparison.Ordinal) && Regex.IsMatch(output,
            @"(?im)^\s*(Nombre compartido|Shared name):\s*" + Regex.Escape(alias[2..]) + @"\s*$");
        if (!ownerMatches || !aliasMatches || pipe.Length == 0 || expectedPipeHash.Length != 64 ||
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pipe))) != expectedPipeHash)
            throw new InvalidOperationException("El propietario, alias y endpoint LocalDB no coinciden con el preflight acreditado.");
    }
}
