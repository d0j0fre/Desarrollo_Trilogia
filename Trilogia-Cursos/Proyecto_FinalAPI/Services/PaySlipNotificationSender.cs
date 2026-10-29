using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using JJ.Commerce;
using Microsoft.Extensions.Options;

namespace Proyecto_FinalAPI.Services;

public sealed record PaySlipNotificationSnapshot(long CalculationId, int RecipientUserId, string Email);
public sealed record PaySlipNotificationClaim(PaySlipNotificationReceipt Receipt, bool ShouldSend,
    PaySlipNotificationSnapshot? Snapshot);

public interface IPaySlipNotificationStore
{
    Task<PaySlipNotificationReceipt> RequestAsync(PaySlipNotificationCommand command, CancellationToken ct);
    Task<PaySlipNotificationClaim> ClaimAsync(PaySlipNotificationCommand command, byte[] token, CancellationToken ct);
    Task<PaySlipNotificationReceipt> CompleteAsync(Guid operation, byte[] token, string state, CancellationToken ct);
    Task<PaySlipNotificationReceipt> ReceiptAsync(PaySlipNotificationReceiptCommand command, CancellationToken ct);
}

public interface IPaySlipNotificationSender
{
    Task<PaySlipNotificationReceipt> SendAsync(PaySlipNotificationCommand command, CancellationToken ct);
    Task<PaySlipNotificationReceipt> ReceiptAsync(PaySlipNotificationReceiptCommand command, CancellationToken ct);
}

/// <summary>The restricted store authorizes and supplies the recipient; an SMTP claim is never replayed.</summary>
public sealed class PaySlipNotificationSender(IPaySlipNotificationStore store, EmailService email,
    IConfiguration configuration, IOptions<CompanyOptions> company, TimeProvider clock) : IPaySlipNotificationSender
{
    public async Task<PaySlipNotificationReceipt> SendAsync(PaySlipNotificationCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        Validate(command.Session, command.CalculationId, command.OperationId);
        var publicBase = PublicBase(configuration);
        var elapsed = Stopwatch.StartNew();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        deadline.Token.ThrowIfCancellationRequested();
        var requested = ValidateReceipt(await store.RequestAsync(command, deadline.Token), command.OperationId);
        if (requested.State != "Pending") return requested;

        byte[] token = RandomNumberGenerator.GetBytes(32);
        try
        {
            var claimed = await store.ClaimAsync(command, token, deadline.Token);
            var receipt = ValidateReceipt(claimed.Receipt, command.OperationId);
            if (!claimed.ShouldSend)
            {
                if (claimed.Snapshot is not null) throw new InvalidOperationException("Unexpected notification snapshot.");
                return receipt;
            }
            if (receipt.State != "Sending") throw new InvalidOperationException("Notification claim unavailable.");
            string state = "Unknown";
            try
            {
                var snapshot = claimed.Snapshot ?? throw new InvalidOperationException("Notification snapshot unavailable.");
                ValidateSnapshot(snapshot, command.CalculationId);
                string html = BuildHtml(snapshot, publicBase, company.Value);
                deadline.Token.ThrowIfCancellationRequested();
                await email.SendEmailAsync(snapshot.Email, "Boleta de pago disponible — " + SafeSubject(company.Value.BrandName),
                    html, deadline.Token);
                state = "SmtpAccepted";
            }
            catch (Exception error) when (error is SmtpException or InvalidOperationException or ArgumentException
                or OperationCanceledException or IOException)
            {
                // Acceptance may precede an SMTP error or cancellation. Preserve uncertainty without retrying.
            }

            // Completion has its own finite budget so cancellation by the browser does not erase the outcome.
            // A lost completion leaves the durable Sending claim; neither this method nor Receipt sends again.
            var remaining = TimeSpan.FromSeconds(15) - elapsed.Elapsed;
            using var completion = new CancellationTokenSource(remaining > TimeSpan.Zero
                ? TimeSpan.FromSeconds(Math.Min(5, remaining.TotalSeconds)) : TimeSpan.Zero);
            var completed = ValidateReceipt(await store.CompleteAsync(command.OperationId, token, state, completion.Token), command.OperationId);
            if (completed.State != state) throw new InvalidOperationException("Notification completion differs.");
            return completed;
        }
        finally { CryptographicOperations.ZeroMemory(token); }
    }

    public async Task<PaySlipNotificationReceipt> ReceiptAsync(PaySlipNotificationReceiptCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        Validate(command.Session, command.CalculationId, command.OperationId);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        deadline.Token.ThrowIfCancellationRequested();
        return ValidateReceipt(await store.ReceiptAsync(command, deadline.Token), command.OperationId);
    }

    private void Validate(CommerceSession session, long calculation, Guid operation)
    {
        if (!configuration.GetValue<bool>("PaySlipNotifications:Enabled"))
            throw new InvalidOperationException("Pay slip notification authority unavailable.");
        if (session is null || !session.IsValid(clock.GetUtcNow().UtcDateTime)) throw new UnauthorizedAccessException();
        if (calculation <= 0 || operation == Guid.Empty) throw new ArgumentException("Invalid pay slip operation.");
    }

    internal static Uri PublicBase(IConfiguration configuration)
    {
        string value = configuration["PaySlipNotifications:PublicBaseUrl"] ?? "";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(uri.Host) || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException("A fixed HTTPS pay slip portal is required.");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
    }

    internal static void ValidateSnapshot(PaySlipNotificationSnapshot snapshot, long expectedCalculation)
    {
        if (snapshot.CalculationId != expectedCalculation || snapshot.CalculationId <= 0 || snapshot.RecipientUserId <= 0
            || string.IsNullOrWhiteSpace(snapshot.Email) || snapshot.Email.Any(char.IsControl)
            || !MailAddress.TryCreate(snapshot.Email, out var address) || address.Address != snapshot.Email)
            throw new InvalidOperationException("Canonical pay slip snapshot unavailable.");
    }

    internal static string BuildHtml(PaySlipNotificationSnapshot snapshot, Uri publicBase, CompanyOptions company)
    {
        ValidateSnapshot(snapshot, snapshot.CalculationId);
        var link = new Uri(publicBase, "MyPaySlips/Details/" + snapshot.CalculationId.ToString(CultureInfo.InvariantCulture));
        if (link.Scheme != Uri.UriSchemeHttps || link.GetLeftPart(UriPartial.Authority) != publicBase.GetLeftPart(UriPartial.Authority))
            throw new InvalidOperationException("Canonical pay slip portal unavailable.");
        return "<p>" + WebUtility.HtmlEncode(company.BrandName) + "</p><p>Tu boleta de pago está disponible en el portal privado.</p>"
            + "<p><a href=\"" + WebUtility.HtmlEncode(link.AbsoluteUri) + "\">Consultar boleta</a></p>"
            + "<p>El acceso requiere iniciar sesión. Este correo no contiene importes ni adjuntos.</p>";
    }

    private static PaySlipNotificationReceipt ValidateReceipt(PaySlipNotificationReceipt receipt, Guid operation)
    {
        if (receipt is null || !receipt.IsValid || receipt.OperationId != operation)
            throw new InvalidOperationException("Notification receipt unavailable.");
        return receipt;
    }

    private static string SafeSubject(string value) => new((value ?? "").Where(c => !char.IsControl(c)).Take(150).ToArray());
}
