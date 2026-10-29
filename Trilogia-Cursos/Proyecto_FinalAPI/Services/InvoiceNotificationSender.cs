using System.Globalization;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using JJ.Commerce;
using Microsoft.Extensions.Options;

namespace Proyecto_FinalAPI.Services;

public sealed record InvoiceNotificationSnapshot(string Number, int OrderId, string Customer, string Email, DateTime IssuedUtc, decimal Total);
public sealed record InvoiceNotificationClaim(InvoiceNotificationReceipt Receipt, bool ShouldSend, InvoiceNotificationSnapshot? Snapshot);

public interface IInvoiceNotificationStore
{
    Task<InvoiceNotificationReceipt> RequestAsync(InvoiceNotificationCommand command, CancellationToken ct);
    Task<InvoiceNotificationClaim> ClaimAsync(InvoiceNotificationCommand command, byte[] token, CancellationToken ct);
    Task<InvoiceNotificationReceipt> CompleteAsync(Guid operation, byte[] token, string state, CancellationToken ct);
    Task<InvoiceNotificationReceipt> ReceiptAsync(InvoiceNotificationReceiptCommand command, CancellationToken ct);
}

public interface IInvoiceNotificationSender
{
    Task<InvoiceNotificationReceipt> SendAsync(InvoiceNotificationCommand command, CancellationToken ct);
    Task<InvoiceNotificationReceipt> ReceiptAsync(InvoiceNotificationReceiptCommand command, CancellationToken ct);
}

/// <summary>Only SQL can supply the destination and invoice snapshot. SMTP is never retried after a claim.</summary>
public sealed class InvoiceNotificationSender(IInvoiceNotificationStore store, EmailService email,
    IOptions<CompanyOptions> company, TimeProvider clock) : IInvoiceNotificationSender
{
    public async Task<InvoiceNotificationReceipt> SendAsync(InvoiceNotificationCommand command, CancellationToken ct)
    {
        var elapsed = Stopwatch.StartNew();
        Validate(command.Session, command.InvoiceId, command.OperationId);
        var requested = await store.RequestAsync(command, ct);
        if (requested.State == "AlreadyRequested") return requested;
        var token = RandomNumberGenerator.GetBytes(32);
        try
        {
            var claimed = await store.ClaimAsync(command, token, ct);
            if (!claimed.ShouldSend) return claimed.Receipt;
            var state = "Unknown";
            try
            {
                var snapshot = claimed.Snapshot ?? throw new InvalidOperationException("Invoice snapshot unavailable.");
                var html = BuildHtml(snapshot, company.Value);
                ct.ThrowIfCancellationRequested();
                await email.SendEmailAsync(snapshot.Email, $"Factura {SafeSubject(snapshot.Number)} — {SafeSubject(company.Value.BrandName)}", html, ct);
                state = "SmtpAccepted";
            }
            catch (Exception error) when (error is SmtpException or InvalidOperationException or ArgumentException or OperationCanceledException or IOException)
            {
                // An SMTP error/cancellation can occur after acceptance. Persist uncertainty, never replay the send.
            }
            // A cancelled browser must not prevent bounded recording of an already attempted SMTP operation.
            // Await this technical completion; it is neither a detached send nor a client-token retry.
            var remaining = TimeSpan.FromSeconds(15) - elapsed.Elapsed;
            using var completionDeadline = new CancellationTokenSource(remaining > TimeSpan.Zero
                ? TimeSpan.FromSeconds(Math.Min(5, remaining.TotalSeconds)) : TimeSpan.Zero);
            return await store.CompleteAsync(command.OperationId, token, state, completionDeadline.Token);
        }
        finally { CryptographicOperations.ZeroMemory(token); }
    }

    public Task<InvoiceNotificationReceipt> ReceiptAsync(InvoiceNotificationReceiptCommand command, CancellationToken ct)
    {
        if (command.Session is null || !command.Session.IsValid(clock.GetUtcNow().UtcDateTime)) throw new UnauthorizedAccessException();
        if (command.InvoiceId <= 0) throw new ArgumentException("Invalid invoice operation.");
        return store.ReceiptAsync(command, ct);
    }

    private void Validate(CommerceSession session, int invoice, Guid operation)
    {
        if (session is null || !session.IsValid(clock.GetUtcNow().UtcDateTime)) throw new UnauthorizedAccessException();
        if (invoice <= 0 || operation == Guid.Empty) throw new ArgumentException("Invalid invoice operation.");
    }

    internal static string BuildHtml(InvoiceNotificationSnapshot snapshot, CompanyOptions company)
    {
        if (snapshot.Email.Any(char.IsControl) || !MailAddress.TryCreate(snapshot.Email, out var address)
            || address.Address != snapshot.Email || snapshot.Total < 0 || snapshot.OrderId <= 0
            || string.IsNullOrWhiteSpace(snapshot.Number) || snapshot.IssuedUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Canonical invoice snapshot unavailable.");
        return EmailTemplateBuilder.BuildInvoiceNotificationEmail(snapshot.Customer, snapshot.Number, snapshot.OrderId,
            snapshot.IssuedUtc, snapshot.Total, company);
    }

    private static string SafeSubject(string value) => new(value.Where(c => !char.IsControl(c)).Take(150).ToArray());
}
