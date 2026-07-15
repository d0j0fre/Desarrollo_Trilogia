using System.Net;
using System.Net.Mail;
using System.Text;
using Proyecto_Final.Models.Store;

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


        public void SendOrderReceipt(
            string destinatario,
            string cliente,
            int pedidoId,
            CheckoutViewModel checkout,
            List<CartItemViewModel> items)
        {
            if (string.IsNullOrWhiteSpace(destinatario))
            {
                throw new InvalidOperationException(
                    "No se encontró el correo electrónico del cliente.");
            }

            var rutaPlantilla = Path.Combine(
                Directory.GetCurrentDirectory(),
                "EmailTemplates",
                "OrderReceipt.html");

            if (!File.Exists(rutaPlantilla))
            {
                throw new FileNotFoundException(
                    "No se encontró la plantilla del comprobante.",
                    rutaPlantilla);
            }

            var productosHtml = new StringBuilder();

            foreach (var item in items)
            {
                var nombre = WebUtility.HtmlEncode(item.Nombre);
                var subtotal = item.Precio * item.Cantidad;

                productosHtml.Append($"""
            <tr style="border-bottom:1px solid #eeeeee">
                <td class="product" style="padding:14px">
                    {nombre}
                </td>

                <td class="qty"
                    align="center"
                    style="padding:14px">
                    {item.Cantidad}
                </td>

                <td class="price"
                    align="right"
                    style="padding:14px">
                    ₡{item.Precio:N2}
                </td>

                <td class="subtotal"
                    align="right"
                    style="padding:14px">
                    ₡{subtotal:N2}
                </td>
            </tr>
        """);
            }

            var subtotalPedido = items.Sum(
                item => item.Precio * item.Cantidad);

            const decimal costoEnvio = 0;

            var total = subtotalPedido + costoEnvio;

            var html = File.ReadAllText(rutaPlantilla);

            html = html
                .Replace(
                    "{{CLIENTE}}",
                    WebUtility.HtmlEncode(cliente))
                .Replace(
                    "{{PEDIDO}}",
                    $"#{pedidoId}")
                .Replace(
                    "{{FECHA}}",
                    DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
                .Replace(
                    "{{METODO_PAGO}}",
                    WebUtility.HtmlEncode(checkout.MetodoPago))
                .Replace(
                    "{{TIPO_ENTREGA}}",
                    WebUtility.HtmlEncode(checkout.TipoEntrega))
                .Replace(
                    "{{DIRECCION}}",
                    WebUtility.HtmlEncode(checkout.DireccionEntrega))
                .Replace(
                    "{{PRODUCTOS}}",
                    productosHtml.ToString())
                .Replace(
                    "{{SUBTOTAL}}",
                    $"₡{subtotalPedido:N2}")
                .Replace(
                    "{{ENVIO}}",
                    $"₡{costoEnvio:N2}")
                .Replace(
                    "{{TOTAL}}",
                    $"₡{total:N2}")
                .Replace(
                    "{{LINK_PEDIDO}}",
                    "#");

            var asunto = $"Confirmación del pedido #{pedidoId}";

            SendEmail(
                destinatario,
                asunto,
                html);
        }
    }
}
