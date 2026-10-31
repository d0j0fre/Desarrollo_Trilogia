using System.Globalization;

namespace Proyecto_Final.Services
{
    /// <summary>
    /// Canal digital del comprobante (CU-092 escenario 2): enlace wa.me que abre WhatsApp con
    /// el mensaje listo; el envío queda a un clic de confirmación. Sin proveedor ni claves.
    /// </summary>
    public static class WhatsAppReceiptLink
    {
        private const string CostaRicaPrefix = "506";
        private static readonly CultureInfo CostaRicaCulture = CultureInfo.GetCultureInfo("es-CR");

        // Deja solo dígitos y antepone 506 a los números locales de 8 dígitos.
        public static string? NormalizePhone(string? telefono)
        {
            if (string.IsNullOrWhiteSpace(telefono))
                return null;

            var digitos = new string(telefono.Where(char.IsAsciiDigit).ToArray());

            if (digitos.Length == 8)
                digitos = CostaRicaPrefix + digitos;

            if (digitos.Length != 11 || !digitos.StartsWith(CostaRicaPrefix, StringComparison.Ordinal)
                || digitos[CostaRicaPrefix.Length..].All(c => c == '0'))
                return null;

            return digitos;
        }

        // Solo número de factura, fecha y total: sin nombre, correo ni dirección del cliente.
        public static string BuildMessage(string numeroFactura, DateTime fechaFactura, decimal total) =>
            $"Licorera La Bodega: su comprobante {numeroFactura} del {fechaFactura.ToString("dd/MM/yyyy", CostaRicaCulture)} " +
            $"por un total de ₡{total.ToString("N2", CostaRicaCulture)}. Gracias por su compra.";

        public static string? BuildUrl(string? telefono, string numeroFactura, DateTime fechaFactura, decimal total)
        {
            var numero = NormalizePhone(telefono);
            return numero == null
                ? null
                : $"https://wa.me/{numero}?text={Uri.EscapeDataString(BuildMessage(numeroFactura, fechaFactura, total))}";
        }
    }
}
