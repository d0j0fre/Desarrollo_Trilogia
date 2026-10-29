using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Proyecto_Final.Services.ChatV2.Broker;

[SupportedOSPlatform("windows")]
internal sealed record BrokerPipeSettings(string Name, string BrokerSid, string AppSid)
{
    internal void Validate()
    {
        if (!Regex.IsMatch(Name ?? "", "\\AJJ-ChatV2-[a-zA-Z0-9-]{8,64}\\z", RegexOptions.CultureInvariant)
            || BrokerSid == AppSid) throw new InvalidOperationException("Broker pipe configuration.");
        foreach (string sid in new[] { BrokerSid, AppSid })
            if (!new SecurityIdentifier(sid).IsAccountSid()) throw new InvalidOperationException("Broker account SID.");
    }
}
internal interface IChatV2BrokerExchange
{
    ValueTask<BrokerResponse> ExchangeAsync(BrokerRequest request, CancellationToken ct);
}

[SupportedOSPlatform("windows")]
internal sealed class ChatV2BrokerPipeClient(BrokerPipeSettings settings) : IChatV2BrokerExchange
{
    public async ValueTask<BrokerResponse> ExchangeAsync(BrokerRequest request, CancellationToken ct)
    {
        settings.Validate();
        using var current = WindowsIdentity.GetCurrent();
        if (current.User?.Value != settings.AppSid || current.Groups?.Any(g => g.Value == "S-1-5-32-544") != false)
            throw new ChatV2Exception(ChatV2Failure.Dependency);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(ChatV2BrokerLifetime.OperationBudget);
        // Explicit rights exclude CreateNewInstance. Never use GENERIC_WRITE or CurrentUserOnly across distinct SIDs.
        await using var pipe = new NamedPipeClientStream(".", settings.Name, PipeAccessRights.ReadWrite,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Identification, HandleInheritability.None);
        try
        {
            await pipe.ConnectAsync(2000, deadline.Token);
            // A squatted pipe must not receive a login binding, UUID or body. An ordinary APP cannot set the broker's owner SID.
            if (pipe.GetAccessControl().GetOwner(typeof(SecurityIdentifier))?.Value != settings.BrokerSid)
                throw new ChatV2Exception(ChatV2Failure.Dependency);
            await pipe.WriteAsync(new byte[] { 0x4A }, deadline.Token); await pipe.FlushAsync(deadline.Token);
            byte[] ack = new byte[1]; await pipe.ReadExactlyAsync(ack, deadline.Token);
            if (ack[0] != 0x32) throw new ChatV2Exception(ChatV2Failure.Dependency);
            await BrokerFrame.WriteAsync(pipe, request, BrokerFrame.RequestLimit, deadline.Token);
            var response = await BrokerFrame.ReadAsync<BrokerResponse>(pipe, BrokerFrame.ResponseLimit, deadline.Token);
            if (response.Version != 1 || response.Correlation != request.Correlation ||
                response.Failure is not null && (response.Result is not null || !Enum.IsDefined(response.Failure.Value)))
                throw new ChatV2Exception(ChatV2Failure.Contract);
            return response;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or TimeoutException)
        { throw new ChatV2Exception(ChatV2Failure.Dependency); }
        finally { CryptographicOperations.ZeroMemory(request.Login.BindingHash); }
    }
}

[SupportedOSPlatform("windows")]
internal sealed class ChatV2BrokerPipeServer(BrokerPipeSettings settings, ChatV2BrokerDispatcher dispatcher,
    Action<BrokerObservation>? observe = null)
{
    internal async Task RunAsync(CancellationToken stop)
    {
        settings.Validate(); using var current = WindowsIdentity.GetCurrent();
        if (current.User?.Value != settings.BrokerSid || current.Groups?.Any(g => g.Value == "S-1-5-32-544") != false)
            throw new InvalidOperationException("Broker Windows identity was not accredited.");
        using var host = CancellationTokenSource.CreateLinkedTokenSource(stop);
        using var lifetime = new ChatV2BrokerLifetime();
        // Fixed workers bound accepted clients and queued SQL work. All accept/handler/watch tasks are joined.
        await Task.WhenAll(Enumerable.Range(0, 12).Select(index => SupervisedWorker(index == 0)));
        async Task SupervisedWorker(bool first)
        {
            try { await Worker(first); }
            catch { host.Cancel(); throw; } // A failed accept loop stops and joins its peers.
        }
        async Task Worker(bool first)
        {
            while (!host.IsCancellationRequested)
            {
                await using var pipe = Create(first); first = false;
                try { await pipe.WaitForConnectionAsync(host.Token); }
                catch (OperationCanceledException) when (host.IsCancellationRequested) { return; }
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(host.Token);
                deadline.CancelAfter(ChatV2BrokerLifetime.OperationBudget);
                BrokerRequest? request = null;
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    byte[] hello = new byte[1]; await pipe.ReadExactlyAsync(hello, deadline.Token);
                    if (hello[0] != 0x4A) continue;
                    string? clientSid = null;
                    pipe.RunAsClient(() => { using var client = WindowsIdentity.GetCurrent(); clientSid = client.User?.Value; });
                    if (clientSid != settings.AppSid) continue;
                    await pipe.WriteAsync(new byte[] { 0x32 }, deadline.Token); await pipe.FlushAsync(deadline.Token);
                    request = await BrokerFrame.ReadAsync<BrokerRequest>(pipe, BrokerFrame.RequestLimit, deadline.Token);
                    Mark("request-admitted", null);
                    var response = await lifetime.ExecuteAsync(token => dispatcher.DispatchAsync(request, token),
                        async token =>
                        {
                            byte[] extra = new byte[1]; int count = await pipe.ReadAsync(extra, token);
                            Mark(count == 0 ? "client-EOF" : "unexpected-extra-input", null);
                        }, deadline.Token);
                    await BrokerFrame.WriteAsync(pipe, response, BrokerFrame.ResponseLimit, deadline.Token);
                    Mark(response.Failure is null ? "response-complete" : "contract-rejected", null);
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                { Mark(BrokerObservation.FailurePhase(error, deadline.IsCancellationRequested), error.GetType().Name); }
                finally
                {
                    if (request?.Login?.BindingHash is { } binding) CryptographicOperations.ZeroMemory(binding);
                    Mark("handler-released", null);
                }
                void Mark(string phase, string? type)
                {
                    // No exception object/message, SQL, UUID, actor, proof, cursor or message body.
                    observe?.Invoke(new(phase, elapsed.Elapsed.TotalMilliseconds, lifetime.Active, lifetime.Admitted, type,
                        request?.Correlation ?? Guid.Empty));
                }
            }
        }
        NamedPipeServerStream Create(bool first)
        {
            var acl = new PipeSecurity(); acl.SetOwner(new SecurityIdentifier(settings.BrokerSid));
            acl.SetAccessRuleProtection(true, false);
            acl.AddAccessRule(new(new SecurityIdentifier("S-1-5-2"), PipeAccessRights.FullControl, AccessControlType.Deny));
            acl.AddAccessRule(new(new SecurityIdentifier(settings.BrokerSid), PipeAccessRights.FullControl, AccessControlType.Allow));
            acl.AddAccessRule(new(new SecurityIdentifier(settings.AppSid), PipeAccessRights.ReadWrite, AccessControlType.Allow));
            return NamedPipeServerStreamAcl.Create(settings.Name, PipeDirection.InOut, 12, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | (first ? PipeOptions.FirstPipeInstance : PipeOptions.None), 4096, 4096, acl, HandleInheritability.None);
        }
    }
}
internal sealed record BrokerObservation(string Phase, double ElapsedMs, int Active, int Admitted, string? ErrorType,
    Guid Correlation)
{
    internal static string FailurePhase(Exception error, bool outerCancelled) =>
        error is OperationCanceledException || outerCancelled ? "cancelled-after-work-exit" : "request-failed";
}
