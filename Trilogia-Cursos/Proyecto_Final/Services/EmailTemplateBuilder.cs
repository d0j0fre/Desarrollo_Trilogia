using System.Net;

namespace Proyecto_Final.Services
{
    public static class EmailTemplateBuilder
    {
        public static string BuildPasswordResetEmail(string nombreCompleto, string resetUrl)
        {
            var nombreSeguro = Encode(nombreCompleto, "Cliente");
            var resetUrlSeguro = WebUtility.HtmlEncode(resetUrl ?? string.Empty);

            var contenido = $@"
                <p style='margin:0 0 16px;color:#374151;font-size:15px;line-height:1.7;'>
                    Hola <strong>{nombreSeguro}</strong>, recibimos una solicitud para restablecer la contraseña de su cuenta.
                </p>
                <p style='margin:0 0 20px;color:#374151;font-size:15px;line-height:1.7;'>
                    Para continuar, presione el siguiente botón. Por seguridad, este enlace vence en <strong>30 minutos</strong>.
                </p>
                <div style='margin:26px 0;text-align:center;'>
                    <a href='{resetUrlSeguro}' style='display:inline-block;background:#d4af37;color:#111827;text-decoration:none;font-weight:800;padding:14px 24px;border-radius:14px;font-size:15px;'>
                        Restablecer contraseña
                    </a>
                </div>
                <p style='margin:18px 0 0;color:#6b7280;font-size:13px;line-height:1.6;'>
                    Si no solicitó este cambio, puede ignorar este correo. Su contraseña actual seguirá funcionando.
                </p>";

            return BuildBaseTemplate(
                titulo: "Recuperación de contraseña",
                subtitulo: "Solicitud de seguridad de cuenta",
                badge: "Seguridad",
                contenido: contenido,
                notaInferior: "Este mensaje fue generado automáticamente por el sistema de Licorera La Bodega.");
        }

        public static string BuildInvoiceEmail(string nombreCliente, string numeroFactura, int pedidoId, DateTime fechaFactura, decimal total)
        {
            var nombreSeguro = Encode(nombreCliente, "Cliente");
            var numeroSeguro = Encode(numeroFactura, "N/A");

            var contenido = $@"
                <p style='margin:0 0 16px;color:#374151;font-size:15px;line-height:1.7;'>
                    Hola <strong>{nombreSeguro}</strong>, su compra fue procesada correctamente y le presentamos el resumen de su comprobante.
                </p>

                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='border-collapse:collapse;margin:0 0 20px;'>
                    <tr>
                        <td style='padding:12px 14px;background:#fbfaf7;border:1px solid #ebe6dc;border-radius:14px;'>
                            <p style='margin:0 0 6px;color:#6b7280;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em;'>Número de comprobante</p>
                            <p style='margin:0;color:#111827;font-size:15px;font-weight:700;'>{numeroSeguro}</p>
                        </td>
                    </tr>
                </table>

                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='border-collapse:collapse;margin:0 0 20px;'>
                    <tr>
                        <td style='padding:12px 14px;background:#fbfaf7;border:1px solid #ebe6dc;border-radius:14px;'>
                            <p style='margin:0 0 6px;color:#6b7280;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em;'>Pedido asociado</p>
                            <p style='margin:0;color:#111827;font-size:15px;font-weight:700;'>#{pedidoId}</p>
                        </td>
                    </tr>
                </table>

                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='border-collapse:collapse;margin:0 0 20px;'>
                    <tr>
                        <td style='padding:12px 14px;background:#fbfaf7;border:1px solid #ebe6dc;border-radius:14px;'>
                            <p style='margin:0 0 6px;color:#6b7280;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em;'>Fecha</p>
                            <p style='margin:0;color:#111827;font-size:15px;font-weight:700;'>{fechaFactura:dd/MM/yyyy HH:mm}</p>
                        </td>
                    </tr>
                </table>

                <div style='padding:16px 18px;background:#ffffff;border:1px solid #ebe6dc;border-radius:16px;text-align:center;'>
                    <p style='margin:0 0 6px;color:#6b7280;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em;'>Total facturado</p>
                    <p style='margin:0;color:#111827;font-size:22px;font-weight:800;'>&#8353;{total:N2}</p>
                </div>

                <p style='margin:18px 0 0;color:#6b7280;font-size:13px;line-height:1.6;'>
                    Puede consultar el detalle completo de su comprobante desde el portal del cliente, en la sección Mis pedidos.
                </p>";

            return BuildBaseTemplate(
                titulo: "Comprobante de compra",
                subtitulo: "Confirmación de facturación",
                badge: "Facturación",
                contenido: contenido,
                notaInferior: "Este mensaje fue generado automáticamente por el sistema de Licorera La Bodega.");
        }

        public static string BuildContactNotificationEmail(string nombre, string correo, string asunto, string mensaje)
        {
            var nombreSeguro = Encode(nombre, "No indicado");
            var correoSeguro = Encode(correo, "No indicado");
            var asuntoSeguro = Encode(asunto, "Consulta web");
            var mensajeSeguro = WebUtility.HtmlEncode(mensaje ?? string.Empty).Replace("\n", "<br />");

            var contenido = $@"
                <p style='margin:0 0 18px;color:#374151;font-size:15px;line-height:1.7;'>
                    Se recibió una nueva consulta desde el formulario de contacto del sitio web.
                </p>

                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='border-collapse:collapse;margin:0 0 20px;'>
                    <tr>
                        <td style='padding:12px 14px;background:#fbfaf7;border:1px solid #ebe6dc;border-radius:14px;'>
                            <p style='margin:0 0 6px;color:#6b7280;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em;'>Nombre</p>
                            <p style='margin:0;color:#111827;font-size:15px;font-weight:700;'>{nombreSeguro}</p>
                        </td>
                    </tr>
                </table>

                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='border-collapse:collapse;margin:0 0 20px;'>
                    <tr>
                        <td style='padding:12px 14px;background:#fbfaf7;border:1px solid #ebe6dc;border-radius:14px;'>
                            <p style='margin:0 0 6px;color:#6b7280;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em;'>Correo</p>
                            <p style='margin:0;color:#111827;font-size:15px;font-weight:700;'>{correoSeguro}</p>
                        </td>
                    </tr>
                </table>

                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='border-collapse:collapse;margin:0 0 20px;'>
                    <tr>
                        <td style='padding:12px 14px;background:#fbfaf7;border:1px solid #ebe6dc;border-radius:14px;'>
                            <p style='margin:0 0 6px;color:#6b7280;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em;'>Asunto</p>
                            <p style='margin:0;color:#111827;font-size:15px;font-weight:700;'>{asuntoSeguro}</p>
                        </td>
                    </tr>
                </table>

                <div style='padding:16px 18px;background:#ffffff;border:1px solid #ebe6dc;border-radius:16px;'>
                    <p style='margin:0 0 8px;color:#6b7280;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em;'>Mensaje</p>
                    <p style='margin:0;color:#374151;font-size:15px;line-height:1.7;'>{mensajeSeguro}</p>
                </div>";

            return BuildBaseTemplate(
                titulo: "Nueva consulta web",
                subtitulo: asuntoSeguro,
                badge: "Contacto",
                contenido: contenido,
                notaInferior: "Revisar el módulo de Consultas para darle seguimiento administrativo.");
        }

        public static string BuildContactConfirmationEmail(string nombre, string asunto, string mensaje)
        {
            var nombreSeguro = Encode(nombre, "Cliente");
            var asuntoSeguro = Encode(asunto, "Consulta web");
            var mensajeSeguro = WebUtility.HtmlEncode(mensaje ?? string.Empty).Replace("\n", "<br />");

            var contenido = $@"
                <p style='margin:0 0 18px;color:#374151;font-size:15px;line-height:1.7;'>
                    Hola <strong>{nombreSeguro}</strong>, recibimos su consulta y nuestro equipo le dará seguimiento. Este es el resumen de lo que nos envió.
                </p>

                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='border-collapse:collapse;margin:0 0 20px;'>
                    <tr>
                        <td style='padding:12px 14px;background:#fbfaf7;border:1px solid #ebe6dc;border-radius:14px;'>
                            <p style='margin:0 0 6px;color:#6b7280;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em;'>Asunto</p>
                            <p style='margin:0;color:#111827;font-size:15px;font-weight:700;'>{asuntoSeguro}</p>
                        </td>
                    </tr>
                </table>

                <div style='padding:16px 18px;background:#ffffff;border:1px solid #ebe6dc;border-radius:16px;'>
                    <p style='margin:0 0 8px;color:#6b7280;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em;'>Mensaje</p>
                    <p style='margin:0;color:#374151;font-size:15px;line-height:1.7;'>{mensajeSeguro}</p>
                </div>";

            return BuildBaseTemplate(
                titulo: "Recibimos su consulta",
                subtitulo: asuntoSeguro,
                badge: "Contacto",
                contenido: contenido,
                notaInferior: "Le avisaremos por este medio cuando su consulta sea resuelta.");
        }

        public static string BuildConsultationResolvedEmail(string nombre, string asunto, string estado)
        {
            var nombreSeguro = Encode(nombre, "Cliente");
            var asuntoSeguro = Encode(asunto, "Consulta web");
            var estadoSeguro = Encode(estado, "Atendida");

            var contenido = $@"
                <p style='margin:0 0 18px;color:#374151;font-size:15px;line-height:1.7;'>
                    Hola <strong>{nombreSeguro}</strong>, su consulta fue atendida por nuestro equipo.
                </p>

                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='border-collapse:collapse;margin:0 0 20px;'>
                    <tr>
                        <td style='padding:12px 14px;background:#fbfaf7;border:1px solid #ebe6dc;border-radius:14px;'>
                            <p style='margin:0 0 6px;color:#6b7280;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em;'>Asunto</p>
                            <p style='margin:0;color:#111827;font-size:15px;font-weight:700;'>{asuntoSeguro}</p>
                        </td>
                    </tr>
                </table>

                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='border-collapse:collapse;margin:0 0 20px;'>
                    <tr>
                        <td style='padding:12px 14px;background:#fbfaf7;border:1px solid #ebe6dc;border-radius:14px;'>
                            <p style='margin:0 0 6px;color:#6b7280;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em;'>Estado</p>
                            <p style='margin:0;color:#111827;font-size:15px;font-weight:700;'>{estadoSeguro}</p>
                        </td>
                    </tr>
                </table>";

            return BuildBaseTemplate(
                titulo: "Su consulta fue resuelta",
                subtitulo: asuntoSeguro,
                badge: "Contacto",
                contenido: contenido,
                notaInferior: "Si necesita más ayuda, puede enviarnos una nueva consulta desde el sitio web.");
        }

        // ── Correos de notificación al cliente y al personal (fin-correo) ─────────────────────────

        public static string BuildOrderStatusEmail(string nombre, int pedidoId, string estado, string? motivo, string? enlacePedido)
        {
            var nombreSeguro = Encode(nombre, "Cliente");
            var (titulo, frase, notaInferior) = estado switch
            {
                "Aprobado" => ("Su pedido fue aprobado", "su pedido fue aprobado y ya lo estamos atendiendo.",
                    "Le avisaremos por este medio cuando el pedido avance en la entrega."),
                "Liberado" => ("Su pedido fue aprobado", "su pedido fue revisado y aprobado por la gerencia; ya lo estamos atendiendo.",
                    "Le avisaremos por este medio cuando el pedido avance en la entrega."),
                "Rechazado" => ("Su pedido no pudo ser aprobado", "lamentamos informarle que su pedido no fue aprobado.",
                    "Si tiene dudas, puede contactarnos desde el formulario de contacto del sitio web."),
                "Cancelado" => ("Su pedido fue cancelado", "su pedido fue cancelado.",
                    "Si tiene dudas, puede contactarnos desde el formulario de contacto del sitio web."),
                _ => ("Actualización de su pedido", $"su pedido cambió al estado {OrderStatusFlow.Name(estado)}.", "Gracias por su preferencia.")
            };

            var contenido = $@"
                <p style='margin:0 0 18px;color:#374151;font-size:15px;line-height:1.7;'>
                    Hola <strong>{nombreSeguro}</strong>, {WebUtility.HtmlEncode(frase)}
                </p>
                {InfoBox("Número de pedido", "#" + pedidoId)}
                {InfoBox("Estado", OrderStatusFlow.Name(estado))}
                {(string.IsNullOrWhiteSpace(motivo) ? string.Empty : InfoBox("Motivo", motivo!.Trim(), multiline: true))}
                {Button(enlacePedido, "Ver mi pedido")}";

            return BuildBaseTemplate(titulo, $"Pedido #{pedidoId}", "Pedidos", contenido, notaInferior);
        }

        public static string BuildClaimClosedEmail(string nombre, int reclamoId, string asunto, string resolucion)
        {
            var contenido = $@"
                <p style='margin:0 0 18px;color:#374151;font-size:15px;line-height:1.7;'>
                    Hola <strong>{Encode(nombre, "Cliente")}</strong>, su reclamo fue atendido y quedó cerrado. Esta es la resolución registrada por nuestro equipo.
                </p>
                {InfoBox("Reclamo", "#" + reclamoId)}
                {InfoBox("Asunto", asunto)}
                {InfoBox("Resolución", resolucion, multiline: true)}";

            return BuildBaseTemplate("Su reclamo fue resuelto", $"Reclamo #{reclamoId}", "Servicio al cliente", contenido,
                "Si no está conforme con la resolución, puede escribirnos desde el formulario de contacto del sitio web.");
        }

        public static string BuildWarrantyResultEmail(string nombre, int garantiaId, string producto, string estado, string resolucion)
        {
            var aprobada = string.Equals(estado, "Aprobada", StringComparison.OrdinalIgnoreCase);
            var contenido = $@"
                <p style='margin:0 0 18px;color:#374151;font-size:15px;line-height:1.7;'>
                    Hola <strong>{Encode(nombre, "Cliente")}</strong>, su solicitud de garantía fue evaluada: {(aprobada ? "<strong>fue aprobada</strong>." : "<strong>no fue aprobada</strong>.")}
                </p>
                {InfoBox("Solicitud de garantía", "#" + garantiaId)}
                {InfoBox("Producto", producto)}
                {InfoBox("Resultado", estado)}
                {InfoBox("Resolución", resolucion, multiline: true)}";

            return BuildBaseTemplate(aprobada ? "Su garantía fue aprobada" : "Su garantía no fue aprobada", $"Solicitud #{garantiaId}", "Garantías", contenido,
                "Puede ver el estado de sus solicitudes en el portal del cliente, sección Garantías.");
        }

        public static string BuildPaySlipEmail(string empleado, string? enlaceBoleta)
        {
            var contenido = $@"
                <p style='margin:0 0 18px;color:#374151;font-size:15px;line-height:1.7;'>
                    Hola <strong>{Encode(empleado, "colaborador")}</strong>, su boleta de pago ya está disponible. La encuentra adjunta en formato PDF y también en el portal privado del personal.
                </p>
                {Button(enlaceBoleta, "Consultar boleta")}";

            return BuildBaseTemplate("Boleta de pago disponible", "Planilla", "Recursos humanos", contenido,
                "Este documento es confidencial. Si no esperaba este correo, avise al departamento de Recursos Humanos.");
        }

        public static string BuildDocumentAlertEmail(string titulo, DateTime vencimiento, int diasRestantes, string? enlace)
        {
            var contenido = $@"
                <p style='margin:0 0 18px;color:#374151;font-size:15px;line-height:1.7;'>
                    Usted es el responsable de un documento que requiere atención. Por favor, actualice el documento antes de su vencimiento.
                </p>
                {InfoBox("Documento", titulo)}
                {InfoBox("Fecha de vencimiento", vencimiento.ToString("dd/MM/yyyy"))}
                {InfoBox("Situación", DescribirDias(diasRestantes))}
                {Button(enlace, "Ver alertas documentales")}";

            return BuildBaseTemplate("Alerta de vencimiento de documento", titulo, "Documentos", contenido,
                "Cuando el documento esté actualizado, marque la alerta como atendida en Seguridad > Alertas documentales.");
        }

        public static string BuildFleetDocumentAlertEmail(string placa, string tipo, DateTime vencimiento, int diasRestantes, string? enlace)
        {
            var contenido = $@"
                <p style='margin:0 0 18px;color:#374151;font-size:15px;line-height:1.7;'>
                    Un documento legal de la flota está por vencer o ya venció. Administración y Logística deben gestionar su renovación.
                </p>
                {InfoBox("Vehículo", placa)}
                {InfoBox("Documento", tipo)}
                {InfoBox("Fecha de vencimiento", vencimiento.ToString("dd/MM/yyyy"))}
                {InfoBox("Situación", DescribirDias(diasRestantes))}
                {Button(enlace, "Ver alertas de flota")}";

            return BuildBaseTemplate("Documento de flota por vencer", $"{tipo} · {placa}", "Flota", contenido,
                "Registre el documento renovado en Flota > Alertas para que deje de avisarse.");
        }

        public static string DescribirDias(int diasRestantes) =>
            diasRestantes < 0 ? $"Venció hace {-diasRestantes} día(s)"
            : diasRestantes == 0 ? "Vence hoy"
            : $"Vence en {diasRestantes} día(s)";

        private static string InfoBox(string etiqueta, string? valor, bool multiline = false)
        {
            var seguro = WebUtility.HtmlEncode(valor ?? string.Empty);
            if (multiline) seguro = seguro.Replace("\r\n", "\n").Replace("\n", "<br />");
            return $@"<table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='border-collapse:collapse;margin:0 0 16px;'>
                    <tr>
                        <td style='padding:12px 14px;background:#fbfaf7;border:1px solid #ebe6dc;border-radius:14px;'>
                            <p style='margin:0 0 6px;color:#6b7280;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.04em;'>{WebUtility.HtmlEncode(etiqueta)}</p>
                            <p style='margin:0;color:#111827;font-size:15px;font-weight:700;line-height:1.6;'>{seguro}</p>
                        </td>
                    </tr>
                </table>";
        }

        /// <summary>Botón con enlace; sin URL pública configurada no se dibuja (mejor sin botón que con un enlace a localhost).</summary>
        private static string Button(string? url, string texto)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            return $@"<div style='margin:24px 0 6px;text-align:center;'>
                    <a href='{WebUtility.HtmlEncode(url)}' style='display:inline-block;background:#d4af37;color:#111827;text-decoration:none;font-weight:800;padding:14px 24px;border-radius:14px;font-size:15px;'>{WebUtility.HtmlEncode(texto)}</a>
                </div>";
        }

        private static string BuildBaseTemplate(string titulo, string subtitulo, string badge, string contenido, string notaInferior)
        {
            var tituloSeguro = Encode(titulo, "Licorera La Bodega");
            var subtituloSeguro = Encode(subtitulo, "Notificación del sistema");
            var badgeSeguro = Encode(badge, "Sistema");
            var notaSeguro = Encode(notaInferior, string.Empty);

            return $@"<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <title>{tituloSeguro}</title>
</head>
<body style='margin:0;padding:0;background:#f4f1ea;font-family:Arial,Helvetica,sans-serif;color:#111827;'>
    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='border-collapse:collapse;background:#f4f1ea;padding:0;margin:0;'>
        <tr>
            <td align='center' style='padding:32px 14px;'>
                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='border-collapse:collapse;max-width:640px;background:#ffffff;border-radius:24px;overflow:hidden;box-shadow:0 18px 44px rgba(17,24,39,.12);'>
                    <tr>
                        <td style='background:#111114;background-image:linear-gradient(135deg,#111114 0%,#222227 70%,#111114 100%);padding:30px 32px;color:#ffffff;'>
                            <div style='display:inline-block;background:rgba(212,175,55,.16);color:#f0cf66;border:1px solid rgba(212,175,55,.32);border-radius:999px;padding:7px 12px;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.06em;margin-bottom:16px;'>
                                {badgeSeguro}
                            </div>
                            <h1 style='margin:0 0 8px;color:#ffffff;font-size:28px;line-height:1.15;font-weight:900;'>
                                {tituloSeguro}
                            </h1>
                            <p style='margin:0;color:rgba(255,255,255,.72);font-size:15px;line-height:1.6;'>
                                {subtituloSeguro}
                            </p>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:30px 32px;'>
                            {contenido}
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:0 32px 30px;'>
                            <div style='background:#fbfaf7;border:1px solid #ebe6dc;border-radius:16px;padding:14px 16px;color:#6b7280;font-size:13px;line-height:1.6;'>
                                {notaSeguro}
                            </div>
                        </td>
                    </tr>
                    <tr>
                        <td style='background:#111114;padding:20px 32px;text-align:center;'>
                            <p style='margin:0 0 6px;color:#ffffff;font-size:14px;font-weight:800;'>Licorera La Bodega</p>
                            <p style='margin:0;color:rgba(255,255,255,.62);font-size:12px;line-height:1.6;'>
                                DistribuidoraJJ - Sistema de gestión comercial - Costa Rica
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        private static string Encode(string? value, string fallback)
        {
            return WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? fallback : value.Trim());
        }
    }
}
