using System.Net;
using System.Net.Mail;

namespace Proyecto_FinalAPI.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void SendEmail(string destinatario, string asunto, string contenido)
        {
            // SmtpClient.Timeout does not bound SendMailAsync. Preserve the existing
            // finite deadline for synchronous historical callers as well.
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            SendEmailAsync(destinatario, asunto, contenido, deadline.Token).GetAwaiter().GetResult();
        }

        public virtual async Task SendEmailAsync(string destinatario, string asunto, string contenido, CancellationToken cancellationToken)
        {
            var host = _configuration.GetValue<string>("ConfiguracionCorreo:Host") ?? string.Empty;
            var puerto = _configuration.GetValue<int>("ConfiguracionCorreo:Puerto");
            var remitente = _configuration.GetValue<string>("ConfiguracionCorreo:Remitente") ?? string.Empty;
            var contrasenna = _configuration.GetValue<string>("ConfiguracionCorreo:Contrasenna") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException("El host del correo no está configurado.");

            if (puerto <= 0)
                throw new InvalidOperationException("El puerto del correo no es válido.");

            if (string.IsNullOrWhiteSpace(remitente))
                throw new InvalidOperationException("El remitente del correo no está configurado.");

            if (string.IsNullOrWhiteSpace(contrasenna))
                throw new InvalidOperationException("La contraseña del correo no está configurada.");

            using var mensaje = new MailMessage(remitente, destinatario, asunto, contenido)
            {
                IsBodyHtml = true
            };

            using var smtp = new SmtpClient(host, puerto)
            {
                Credentials = new NetworkCredential(remitente, contrasenna),
                EnableSsl = true,
                Timeout = 15000
            };

            await smtp.SendMailAsync(mensaje, cancellationToken);
        }
    }
}
