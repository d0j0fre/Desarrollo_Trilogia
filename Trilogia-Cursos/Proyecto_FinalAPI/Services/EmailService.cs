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

            if (string.IsNullOrWhiteSpace(destinatario))
                throw new InvalidOperationException("El destinatario del correo no está indicado.");

            // Nombre visible del remitente (Gmail exige que la cuenta sea la autenticada; el nombre es libre).
            var nombreRemitente = _configuration.GetValue<string>("ConfiguracionCorreo:NombreRemitente");
            if (string.IsNullOrWhiteSpace(nombreRemitente)) nombreRemitente = "Licorera La Bodega";

            using var mensaje = new MailMessage
            {
                From = new MailAddress(remitente, nombreRemitente, System.Text.Encoding.UTF8),
                Subject = asunto,
                SubjectEncoding = System.Text.Encoding.UTF8,
                Body = contenido,
                BodyEncoding = System.Text.Encoding.UTF8,
                IsBodyHtml = true
            };
            mensaje.To.Add(destinatario.Trim());

            using var smtp = new SmtpClient(host, puerto)
            {
                Credentials = new NetworkCredential(remitente, contrasenna),
                EnableSsl = true,
                // Sin tiempo de espera propio, un servidor de correo caído deja la solicitud colgada 100 s.
                Timeout = 20000
            };

            // Solo para pruebas locales: deja el correo como .eml en una carpeta en lugar de enviarlo.
            var directorioRecogida = _configuration.GetValue<string>("ConfiguracionCorreo:DirectorioRecogida");
            if (!string.IsNullOrWhiteSpace(directorioRecogida))
            {
                Directory.CreateDirectory(directorioRecogida);
                smtp.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
                smtp.PickupDirectoryLocation = directorioRecogida;
                smtp.EnableSsl = false;
            }

            smtp.Send(mensaje);
        }
    }
}