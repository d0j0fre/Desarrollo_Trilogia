using System.Text.RegularExpressions;

namespace Proyecto_Final.Services.ChatV2.Broker;

internal static class ChatV2BrokerConfiguration
{
    internal static void Validate(IConfiguration configuration)
    {
        string pipe = configuration["ChatV2:BrokerPipe"] ?? "";
        string broker = configuration["ChatV2:BrokerWindowsSid"] ?? "", app = configuration["ChatV2:AppWindowsSid"] ?? "";
        if (!Regex.IsMatch(pipe, "\\AJJ-ChatV2-[a-zA-Z0-9-]{8,64}\\z", RegexOptions.CultureInvariant) || broker == app)
            throw Invalid();
        foreach (string sid in new[] { broker, app })
            if (!Regex.IsMatch(sid, "\\AS-1-5-21-(?:[0-9]{1,10}-){3}[0-9]{1,10}\\z", RegexOptions.CultureInvariant) ||
                !sid[9..].Split('-').All(x => uint.TryParse(x, out var part) && part > 0)) throw Invalid();
        if (broker[..broker.LastIndexOf('-')] != app[..app.LastIndexOf('-')]) throw Invalid();
    }
    private static InvalidOperationException Invalid() => new("No se acreditó la configuración local del broker Chat V2.");
}
