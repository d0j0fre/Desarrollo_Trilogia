using System.Net;
using System.Net.Mail;

namespace Proyecto_Final.Services
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
            var host = _configuration.GetValue<string>("ConfiguracionCorreo:Host") ?? string.Empty;
            var puerto = _configuration.GetValue<int>("ConfiguracionCorreo:Puerto");
            var remitente = _configuration.GetValue<string>("ConfiguracionCorreo:Remitente") ?? string.Empty;
            var contrasenna = _configuration.GetValue<string>("ConfiguracionCorreo:Contrasenna") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException("El host del correo no esta configurado.");

            if (puerto <= 0)
                throw new InvalidOperationException("El puerto del correo no es valido.");

            if (string.IsNullOrWhiteSpace(remitente))
                throw new InvalidOperationException("El remitente del correo no esta configurado.");

            if (string.IsNullOrWhiteSpace(contrasenna))
                throw new InvalidOperationException("La contrasenna del correo no esta configurada.");

            using var mensaje = new MailMessage(remitente, destinatario, asunto, contenido)
            {
                IsBodyHtml = true
            };

            using var smtp = new SmtpClient(host, puerto)
            {
                Credentials = new NetworkCredential(remitente, contrasenna),
                EnableSsl = true
            };

            smtp.Send(mensaje);
        }
    }
}
