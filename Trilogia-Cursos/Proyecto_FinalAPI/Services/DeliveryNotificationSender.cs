using System.Diagnostics;
using System.Net.Mail;
using System.Security.Cryptography;
using JJ.Commerce;
using Microsoft.Extensions.Options;

namespace Proyecto_FinalAPI.Services;

public sealed record DeliveryNotificationSnapshot(int OrderId, string EventState, DateTime RecordedLocalTime, string Customer, string Email);
public sealed record DeliveryNotificationClaim(DeliveryNotificationReceipt Receipt, bool ShouldSend, DeliveryNotificationSnapshot? Snapshot);

public interface IDeliveryNotificationStore
{
    Task<DeliveryNotificationReceipt> RequestAsync(DeliveryNotificationCommand command, CancellationToken ct);
    Task<DeliveryNotificationClaim> ClaimAsync(DeliveryNotificationCommand command, byte[] token, CancellationToken ct);
    Task<DeliveryNotificationReceipt> CompleteAsync(Guid operation, byte[] token, string state, CancellationToken ct);
    Task<DeliveryNotificationReceipt> ReceiptAsync(DeliveryNotificationReceiptCommand command, CancellationToken ct);
}

public interface IDeliveryNotificationSender
{
    Task<DeliveryNotificationReceipt> SendAsync(DeliveryNotificationCommand command, CancellationToken ct);
    Task<DeliveryNotificationReceipt> ReceiptAsync(DeliveryNotificationReceiptCommand command, CancellationToken ct);
}

/// <summary>Business SyncGuid selects a committed event. Notification OperationId belongs only to its original login.</summary>
public sealed class DeliveryNotificationSender(IDeliveryNotificationStore store, EmailService email,
    IOptions<CompanyOptions> company, TimeProvider clock) : IDeliveryNotificationSender
{
    public async Task<DeliveryNotificationReceipt> SendAsync(DeliveryNotificationCommand command, CancellationToken ct)
    {
        var elapsed=Stopwatch.StartNew();ValidateSession(command.Session,command.RouteOrderId);
        if(command.SyncGuid==Guid.Empty||command.OperationId==Guid.Empty||command.SyncGuid==command.OperationId)throw new ArgumentException("Invalid delivery notification identifiers.");
        var requested=await store.RequestAsync(command,ct);
        Require(requested,command.OperationId,command.SyncGuid);
        if(requested.State=="AlreadyRequested")return requested;
        var token=RandomNumberGenerator.GetBytes(32);
        try
        {
            var claimed=await store.ClaimAsync(command,token,ct);
            Require(claimed.Receipt,command.OperationId,command.SyncGuid);
            if(!claimed.ShouldSend)return claimed.Receipt;
            string state="Unknown";
            try
            {
                if(claimed.Receipt.State!="Sending")throw new InvalidOperationException("Invalid durable claim state.");
                var snapshot=claimed.Snapshot??throw new InvalidOperationException("Delivery snapshot unavailable.");
                var html=BuildHtml(snapshot,company.Value);ct.ThrowIfCancellationRequested();
                await email.SendEmailAsync(snapshot.Email,$"Actualización registrada de su pedido #{snapshot.OrderId}",html,ct);
                state="SmtpAccepted";
            }
            catch(Exception error)when(error is SmtpException or InvalidOperationException or ArgumentException or OperationCanceledException or IOException)
            {
                // Unknown means acceptance may have occurred. A subsequent request must not resend this event.
            }
            var remaining=TimeSpan.FromSeconds(15)-elapsed.Elapsed;
            using var completion=new CancellationTokenSource(remaining>TimeSpan.Zero?TimeSpan.FromSeconds(Math.Min(5,remaining.TotalSeconds)):TimeSpan.Zero);
            var receipt=await store.CompleteAsync(command.OperationId,token,state,completion.Token);
            Require(receipt,command.OperationId,command.SyncGuid);return receipt;
        }
        finally{CryptographicOperations.ZeroMemory(token);}
    }

    public async Task<DeliveryNotificationReceipt> ReceiptAsync(DeliveryNotificationReceiptCommand command,CancellationToken ct)
    {
        ValidateSession(command.Session,command.RouteOrderId);
        if(command.OperationId!=Guid.Empty&&command.SyncGuid==Guid.Empty)throw new ArgumentException("Receipt requires its event selector.");
        var receipt=await store.ReceiptAsync(command,ct);
        if(!receipt.IsValid||receipt.State=="AlreadyRequested"||(command.OperationId!=Guid.Empty&&receipt.OperationId!=command.OperationId)
            ||(command.SyncGuid!=Guid.Empty&&receipt.SyncGuid!=command.SyncGuid))throw new InvalidOperationException("Delivery receipt context differs.");
        return receipt;
    }

    private void ValidateSession(CommerceSession session,int routeOrder)
    {
        if(session is null||!session.IsValid(clock.GetUtcNow().UtcDateTime))throw new UnauthorizedAccessException();
        if(routeOrder<=0)throw new ArgumentException("Invalid delivery resource.");
    }
    private static void Require(DeliveryNotificationReceipt receipt,Guid operation,Guid sync)
    {
        if(!receipt.IsValid||receipt.OperationId!=operation||receipt.SyncGuid!=sync)throw new InvalidOperationException("Delivery notification context differs.");
    }
    internal static string BuildHtml(DeliveryNotificationSnapshot snapshot,CompanyOptions company)
    {
        if(snapshot.Email.Any(char.IsControl)||!MailAddress.TryCreate(snapshot.Email,out var address)||address.Address!=snapshot.Email
            ||snapshot.OrderId<=0||snapshot.EventState is not("EnRuta" or "Entregado")||snapshot.RecordedLocalTime.Kind!=DateTimeKind.Unspecified
            ||snapshot.RecordedLocalTime==default)throw new InvalidOperationException("Canonical delivery event unavailable.");
        return EmailTemplateBuilder.BuildDeliveryNotificationEmail(snapshot.Customer,snapshot.OrderId,snapshot.EventState,snapshot.RecordedLocalTime,company);
    }
}
