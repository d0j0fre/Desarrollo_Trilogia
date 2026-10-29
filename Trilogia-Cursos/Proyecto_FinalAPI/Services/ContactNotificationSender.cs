using System.Net.Mail;
using Microsoft.Extensions.Options;
using Proyecto_FinalAPI.Models;

namespace Proyecto_FinalAPI.Services;

public interface IContactNotificationSender
{
    Task SendAsync(ContactNotificationRequest request, CancellationToken ct);
}

public sealed class ContactNotificationSender(EmailService email, IConfiguration configuration,
    IOptions<CompanyOptions> company) : IContactNotificationSender
{
    public Task SendAsync(ContactNotificationRequest request, CancellationToken ct)
    {
        var configured = configuration["Contact:NotificationRecipient"];
        if (!MailAddress.TryCreate(configured, out var destination) || destination.Address != configured?.Trim())
            throw new InvalidOperationException("Falta el destinatario canónico de consultas.");
        var html = EmailTemplateBuilder.BuildContactNotificationEmail(request.Name, request.Email,
            request.Subject, request.Message, company.Value);
        return email.SendEmailAsync(destination.Address, $"Consulta web #{request.ConsultationId} — {company.Value.BrandName}", html, ct);
    }
}
