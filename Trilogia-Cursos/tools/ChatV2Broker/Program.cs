using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Proyecto_Final.Services.ChatV2;
using Proyecto_Final.Services.ChatV2.Broker;

if (args.Length != 1 || !Path.IsPathFullyQualified(args[0])) return 2;
string own = Path.GetFullPath(args[0]).TrimEnd(Path.DirectorySeparatorChar);
if (!Directory.Exists(own) || Path.GetFileName(own) != "broker") return 2;
string phase = "protected-configuration", status = Path.Combine(own, "host-state.json");
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
try
{
    byte[] clear = ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(own, "runtime.dpapi")), null, DataProtectionScope.CurrentUser);
    Dictionary<string, string> values;
    try { values = JsonSerializer.Deserialize<Dictionary<string, string>>(clear) ?? throw new InvalidOperationException(); }
    finally { CryptographicOperations.ZeroMemory(clear); }
    string[] allowed = ["ConnectionStrings__ChatV2App", "ConnectionStrings__ChatV2Reader", "ChatV2__BrokerPipe", "ChatV2__BrokerWindowsSid",
        "ChatV2__AppWindowsSid", "BrokerHost__BootstrapSid", "BrokerHost__ExpectedMdf", "BrokerHost__ExpectedLdf", "BrokerHost__ExpectedErrorLog",
        "BrokerHost__LocalDbAlias", "BrokerHost__LocalDbOwner", "BrokerHost__LocalDbVersion", "BrokerHost__LocalDbBinaryHash", "BrokerHost__ExpectedPipeHash"];
    if (!values.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(allowed)) throw new InvalidOperationException("Broker configuration keys.");
    var config = new ConfigurationBuilder().AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key.Replace("__", ":"), x.Value))).Build();
    ChatV2BrokerConfiguration.Validate(config);
    var pipe = new BrokerPipeSettings(config["ChatV2:BrokerPipe"]!, config["ChatV2:BrokerWindowsSid"]!, config["ChatV2:AppWindowsSid"]!); pipe.Validate();
    using var identity = WindowsIdentity.GetCurrent();
    if (identity.User?.Value != pipe.BrokerSid || identity.Groups?.Any(g => g.Value == "S-1-5-32-544") != false) throw new InvalidOperationException("Broker Windows token.");
    var acl = new DirectoryInfo(own).GetAccessControl();
    var trusted = new[] { pipe.BrokerSid, config["BrokerHost:BootstrapSid"], "S-1-5-18", "S-1-5-32-544" }.ToHashSet(StringComparer.Ordinal);
    if (!acl.AreAccessRulesProtected || acl.GetOwner(typeof(SecurityIdentifier))?.Value != config["BrokerHost:BootstrapSid"] ||
        acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().Any(x => x.AccessControlType == AccessControlType.Allow && !trusted.Contains(x.IdentityReference.Value)))
        throw new InvalidOperationException("Broker private directory.");
    phase = "official-LocalDB-metadata-before-SQL";
    const string native = @"C:\Program Files\Microsoft SQL Server\170\Tools\Binn\SqlLocalDB.exe";
    if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(native))) != config["BrokerHost:LocalDbBinaryHash"]) throw new InvalidOperationException("LocalDB binary changed.");
    string alias = config["BrokerHost:LocalDbAlias"]!;
    if (!Regex.IsMatch(alias, "\\A\\.\\\\[a-zA-Z0-9_]{4,80}\\z", RegexOptions.CultureInvariant)) throw new InvalidOperationException("LocalDB alias.");
    var nativeStart = new ProcessStartInfo(native) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    nativeStart.ArgumentList.Add("info"); nativeStart.ArgumentList.Add(alias);
    using (var info = Process.Start(nativeStart)!)
    {
        Task<string> output = info.StandardOutput.ReadToEndAsync(), errors = info.StandardError.ReadToEndAsync();
        await info.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5), stop.Token); string text = await output; await errors;
        if (info.ExitCode != 0) throw new InvalidOperationException("LocalDB metadata unavailable.");
        BrokerLocalDbMetadata.VerifySharedEndpoint(text, config["BrokerHost:LocalDbOwner"]!, alias, config["BrokerHost:ExpectedPipeHash"]!);
    }
    foreach (string key in new[] { "ChatV2App", "ChatV2Reader" })
    {
        var sql = new SqlConnectionStringBuilder(config.GetConnectionString(key));
        if (sql.DataSource != "(localdb)\\" + alias || sql.ConnectRetryCount != 0 || sql.MaxPoolSize > 4) throw new InvalidOperationException("Broker SQL destination/pool.");
    }
    if (new SqlConnectionStringBuilder(config.GetConnectionString("ChatV2App")).UserID == new SqlConnectionStringBuilder(config.GetConnectionString("ChatV2Reader")).UserID)
        throw new InvalidOperationException("Distinct broker SQL identities required.");
    phase = "restricted-SQL-manifest";
    await DistribuidoraJJ.Security.RestrictedSqlGate.VerifyAsync(config, ("ChatV2App", "BrokerApp"), ("ChatV2Reader", "BrokerReader"));
    foreach (var role in new[] { ("ChatV2App", true), ("ChatV2Reader", false) })
        await ChatV2PersonalManifest.VerifyAsync(config.GetConnectionString(role.Item1)!, role.Item2,
            config["BrokerHost:ExpectedMdf"]!, config["BrokerHost:ExpectedLdf"]!, config["BrokerHost:ExpectedErrorLog"]!, config["BrokerHost:LocalDbVersion"]!, stop.Token);
    phase = "private-cursor-keys";
    var services = new ServiceCollection(); string keys = Path.Combine(own, "keys"); Directory.CreateDirectory(keys);
    services.AddDataProtection().SetApplicationName("DistribuidoraJJ.ChatV2Broker.v1").PersistKeysToFileSystem(new DirectoryInfo(keys)).ProtectKeysWithDpapi();
    using var provider = services.BuildServiceProvider();
    var persistence = new SqlPersonalChatV2Persistence(config, provider.GetRequiredService<IDataProtectionProvider>(), TimeProvider.System);
    string logs = Path.Combine(own, "private", "diagnostics"); Directory.CreateDirectory(logs);
    var events = Channel.CreateBounded<BrokerObservation>(new BoundedChannelOptions(1024) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    long dropped = 0;
    var server = new ChatV2BrokerPipeServer(pipe, new(persistence, TimeProvider.System), value => { if (!events.Writer.TryWrite(value)) Interlocked.Increment(ref dropped); });
    string control = Path.Combine(own, "control", "stop-request"); Directory.CreateDirectory(Path.GetDirectoryName(control)!);
    if (File.Exists(control)) File.Move(control, Path.Combine(own, "control", "stop-processed-" + DateTime.UtcNow.Ticks));
    phase = "IPC-serving";
    await ChatV2BrokerHostLifetime.RunAsync(server.RunAsync, WatchControl, WriteEvents, events.Writer, stop,
        () => File.WriteAllText(status, JsonSerializer.Serialize(new { ready = true, pid = Environment.ProcessId, utc = DateTime.UtcNow, sid = pipe.BrokerSid, phase,
            database = new SqlConnectionStringBuilder(config.GetConnectionString("ChatV2App")).InitialCatalog, signalR = false })));
    File.WriteAllText(Path.Combine(own, "host-stopped.json"), JsonSerializer.Serialize(new { pid = Environment.ProcessId, utc = DateTime.UtcNow, allHandlersDrained = true, droppedDiagnostics = dropped }));
    SqlConnection.ClearAllPools(); return 0;
    async Task WatchControl(CancellationToken ct) { try { while (!ct.IsCancellationRequested) { if (File.Exists(control)) { stop.Cancel(); return; } await Task.Delay(250, ct); } } catch (OperationCanceledException) when (ct.IsCancellationRequested) { } }
    async Task WriteEvents()
    {
        using var writer = new StreamWriter(new FileStream(Path.Combine(logs, "broker-" + DateTime.UtcNow.Ticks + ".jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous));
        await foreach (var value in events.Reader.ReadAllAsync()) await writer.WriteLineAsync(JsonSerializer.Serialize(new { utc = DateTime.UtcNow, observation = value }));
        await writer.FlushAsync();
    }
}
catch (Exception e)
{
    stop.Cancel(); SqlConnection.ClearAllPools();
    File.WriteAllText(Path.Combine(own, "host-error-" + DateTime.UtcNow.Ticks + ".json"), JsonSerializer.Serialize(new { phase, type = e.GetType().Name,
        sqlCode = (e as SqlException)?.Number, manifestStage = (e as BrokerManifestException)?.Stage, hresult = e.HResult, utc = DateTime.UtcNow })); return 1;
}
