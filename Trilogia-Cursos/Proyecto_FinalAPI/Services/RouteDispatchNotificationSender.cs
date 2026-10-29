using System.Diagnostics;
using System.Net.Mail;
using System.Security.Cryptography;
using JJ.Commerce;
using Microsoft.Extensions.Options;

namespace Proyecto_FinalAPI.Services;

public sealed record RouteDispatchNotificationSnapshot(int OrderId, DateTime RecordedUtc, string Customer, string Email);
public sealed record RouteDispatchNotificationClaim(RouteDispatchNotificationReceipt Receipt, bool ShouldSend, RouteDispatchNotificationSnapshot? Snapshot);

public interface IRouteDispatchNotificationStore
{
    Task<RouteDispatchNotificationReceipt> RequestAsync(RouteDispatchNotificationCommand command, CancellationToken ct);
    Task<RouteDispatchNotificationClaim> ClaimAsync(RouteDispatchNotificationCommand command, byte[] token, CancellationToken ct);
    Task<RouteDispatchNotificationReceipt> CompleteAsync(Guid operation, byte[] token, string state, CancellationToken ct);
    Task<RouteDispatchNotificationReceipt> ReceiptAsync(RouteDispatchNotificationReceiptCommand command, CancellationToken ct);
}

public interface IRouteDispatchNotificationSender
{
    Task<RouteDispatchNotificationReceipt> SendAsync(RouteDispatchNotificationCommand command, CancellationToken ct);
    Task<RouteDispatchNotificationReceipt> ReceiptAsync(RouteDispatchNotificationReceiptCommand command, CancellationToken ct);
}

public sealed class RouteDispatchNotificationSender(IRouteDispatchNotificationStore store, EmailService email,
    IOptions<CompanyOptions> company, TimeProvider clock) : IRouteDispatchNotificationSender
{
    public async Task<RouteDispatchNotificationReceipt> SendAsync(RouteDispatchNotificationCommand command, CancellationToken ct)
    {
        var elapsed = Stopwatch.StartNew();
        Validate(command.Session, command.RouteId, command.DispatchId, command.EventId, command.EventGuid);
        if (command.OperationId == Guid.Empty || command.OperationId == command.EventGuid || command.OperationId == command.DispatchId) throw new ArgumentException("Invalid notification UUID.");
        var requested = await store.RequestAsync(command, ct);
        Require(requested, command.OperationId, command.EventGuid);
        if (requested.State is "AlreadyRequested" or "NoRecipient") return requested;
        var token = RandomNumberGenerator.GetBytes(32);
        try
        {
            var claim = await store.ClaimAsync(command, token, ct);
            Require(claim.Receipt, command.OperationId, command.EventGuid);
            if (!claim.ShouldSend) return claim.Receipt;
            string state = "Unknown";
            try
            {
                if (claim.Receipt.State != "Sending") throw new InvalidOperationException("Invalid durable claim state.");
                var snapshot = claim.Snapshot ?? throw new InvalidOperationException("Dispatch snapshot unavailable.");
                var html = BuildHtml(snapshot, company.Value);
                ct.ThrowIfCancellationRequested();
                await email.SendEmailAsync(snapshot.Email, $"Despacho registrado de su pedido #{snapshot.OrderId}", html, ct);
                state = "SmtpAccepted";
            }
            catch (Exception error) when (error is SmtpException or InvalidOperationException or ArgumentException or OperationCanceledException or IOException)
            {
                // SMTP may already have accepted the message. Never replay an uncertain attempt.
            }
            var remaining = TimeSpan.FromSeconds(15) - elapsed.Elapsed;
            using var completion = new CancellationTokenSource(remaining > TimeSpan.Zero ? TimeSpan.FromSeconds(Math.Min(5, remaining.TotalSeconds)) : TimeSpan.Zero);
            var result = await store.CompleteAsync(command.OperationId, token, state, completion.Token);
            Require(result, command.OperationId, command.EventGuid);
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(token); }
    }

    public async Task<RouteDispatchNotificationReceipt> ReceiptAsync(RouteDispatchNotificationReceiptCommand command, CancellationToken ct)
    {
        Validate(command.Session, command.RouteId, command.DispatchId, command.EventId, command.EventGuid);
        var result = await store.ReceiptAsync(command, ct);
        if (!result.IsValid || result.State == "AlreadyRequested" || result.EventGuid != command.EventGuid
            || command.OperationId != Guid.Empty && result.OperationId != command.OperationId) throw new InvalidOperationException("Dispatch receipt context differs.");
        return result;
    }

    private void Validate(CommerceSession session, int route, Guid dispatch, long eventId, Guid eventGuid)
    {
        if (session is null || !session.IsValid(clock.GetUtcNow().UtcDateTime)) throw new UnauthorizedAccessException();
        if (route <= 0 || dispatch == Guid.Empty || eventId <= 0 || eventGuid == Guid.Empty || eventGuid == dispatch) throw new ArgumentException("Invalid dispatch event.");
    }
    private static void Require(RouteDispatchNotificationReceipt receipt, Guid operation, Guid eventGuid)
    {
        if (!receipt.IsValid || receipt.EventGuid != eventGuid || receipt.OperationId != operation && !(receipt.OperationId == Guid.Empty && receipt.State == "NoRecipient"))
            throw new InvalidOperationException("Dispatch notification context differs.");
    }
    internal static string BuildHtml(RouteDispatchNotificationSnapshot snapshot, CompanyOptions company)
    {
        if (snapshot.OrderId <= 0 || snapshot.RecordedUtc == default || snapshot.RecordedUtc.Kind != DateTimeKind.Utc
            || !MailAddress.TryCreate(snapshot.Email, out var address) || address.Address != snapshot.Email
            || snapshot.Email.Any(char.IsControl) || string.IsNullOrWhiteSpace(snapshot.Customer)) throw new InvalidOperationException("Canonical dispatch snapshot unavailable.");
        return EmailTemplateBuilder.BuildRouteDispatchNotificationEmail(snapshot.Customer, snapshot.OrderId, snapshot.RecordedUtc, company);
    }
}
