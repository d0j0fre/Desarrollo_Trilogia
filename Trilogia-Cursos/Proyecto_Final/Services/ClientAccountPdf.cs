using Proyecto_Final.Models.Admin;
using Proyecto_Final.Models.Store;

namespace Proyecto_Final.Services
{
    /// <summary>BUG-12-02 · CU-123-03, CU-124-02: estado de cuenta y factura como archivo PDF.</summary>
    public static class ClientAccountPdf
    {
        private const string Empresa = "Supermercado Mayoreo · Licorera - Distribuidora";

        public static string StatementFileName(ClientAccountStatementViewModel cuenta) =>
            $"estado-de-cuenta-{cuenta.UsuarioId}-{cuenta.FechaCorte:yyyyMMdd}.pdf";

        public static string InvoiceFileName(ClientPortalInvoiceViewModel factura) => InvoiceFileName(factura.NumeroFactura);

        public static string InvoiceFileName(string numeroFactura) => $"factura-{SafeFilePart(numeroFactura)}.pdf";

        public static byte[] BuildStatement(ClientAccountStatementViewModel cuenta, IEnumerable<ClientPortalOrderListItemViewModel> pedidos)
        {
            var pdf = new SimplePdfDocument($"{Empresa} · Estado de cuenta de {cuenta.NombreCompleto} al {cuenta.FechaCorte:dd/MM/yyyy HH:mm}");
            pdf.Title("Estado de cuenta");
            pdf.Text(Empresa);
            pdf.Rule();
            pdf.Text($"Cliente: {cuenta.NombreCompleto} (#{cuenta.UsuarioId})", bold: true);
            pdf.Text($"Correo: {cuenta.Correo}");
            if (!string.IsNullOrWhiteSpace(cuenta.Telefono)) pdf.Text($"Teléfono: {cuenta.Telefono}");
            if (!string.IsNullOrWhiteSpace(cuenta.Direccion)) pdf.Text($"Dirección: {cuenta.Direccion}");
            pdf.Text($"Fecha de corte: {cuenta.FechaCorte:dd/MM/yyyy HH:mm}");

            pdf.Heading("Resumen");
            pdf.Mono(SimplePdfDocument.Columns(("Límite de crédito", 30), (SimplePdfDocument.Money(cuenta.LimiteCredito), -20)));
            pdf.Mono(SimplePdfDocument.Columns(("Saldo pendiente", 30), (SimplePdfDocument.Money(cuenta.Saldo), -20)), bold: true);
            pdf.Mono(SimplePdfDocument.Columns(("Saldo vencido", 30), (SimplePdfDocument.Money(cuenta.DeudaVencida), -20)));
            pdf.Mono(SimplePdfDocument.Columns(("Crédito disponible", 30), (SimplePdfDocument.Money(cuenta.CreditoDisponible), -20)));
            pdf.Mono(SimplePdfDocument.Columns(($"Facturas ({cuenta.CantidadFacturas})", 30), (SimplePdfDocument.Money(cuenta.TotalFacturado), -20)));
            pdf.Mono(SimplePdfDocument.Columns(("Pagos registrados", 30), (SimplePdfDocument.Money(cuenta.TotalPagado), -20)));
            pdf.Mono(SimplePdfDocument.Columns(("Plazo de crédito", 30), ($"{cuenta.PlazoCreditoDias} días", -20)));

            pdf.Heading("Facturas");
            pdf.Mono(SimplePdfDocument.Columns(("Factura", 24), ("Pedido", 7), ("Fecha", 10), ("Estado", 11), ("Pago", 16), ("Total", -20)), bold: true);
            if (cuenta.Facturas.Count == 0) pdf.Text("No hay facturas registradas.");
            foreach (var f in cuenta.Facturas)
            {
                pdf.Mono(SimplePdfDocument.Columns(
                    (f.NumeroFactura, 24), ($"#{f.PedidoId}", 7), (f.FechaFactura.ToString("dd/MM/yyyy"), 10),
                    (f.Estado, 11), (f.MetodoPago ?? "-", 16), (SimplePdfDocument.Money(f.Total), -20)));
            }

            pdf.Heading("Cargos y pagos");
            pdf.Mono(SimplePdfDocument.Columns(("Fecha", 10), ("Tipo", 8), ("Detalle", 30), ("Cargo", -15), ("Pago", -15), ("Saldo", -15)), bold: true);
            if (cuenta.Movimientos.Count == 0) pdf.Text("No hay cargos ni pagos registrados.");
            foreach (var m in cuenta.Movimientos)
            {
                var detalle = string.IsNullOrWhiteSpace(m.Referencia) ? m.Descripcion : $"{m.Descripcion} ({m.Referencia})";
                pdf.Mono(SimplePdfDocument.Columns(
                    (m.FechaMovimiento.ToString("dd/MM/yyyy"), 10), (m.TipoTexto, 8), (detalle, 30),
                    (m.Cargo > 0 ? Amount(m.Cargo) : "", -15), (m.Abono > 0 ? Amount(m.Abono) : "", -15), (Amount(m.SaldoAcumulado), -15)));
            }

            var listaPedidos = pedidos.ToList();
            pdf.Heading("Pedidos");
            pdf.Mono(SimplePdfDocument.Columns(("Pedido", 8), ("Fecha", 17), ("Estado", 14), ("Entrega", 30), ("Total", -20)), bold: true);
            if (listaPedidos.Count == 0) pdf.Text("No hay pedidos registrados.");
            foreach (var p in listaPedidos)
            {
                pdf.Mono(SimplePdfDocument.Columns(
                    ($"#{p.PedidoId}", 8), (p.FechaPedido.ToString("dd/MM/yyyy HH:mm"), 17), (p.Estado, 14),
                    (string.IsNullOrWhiteSpace(p.TipoEntrega) ? "No indicado" : p.TipoEntrega, 30), (SimplePdfDocument.Money(p.Total), -20)));
            }

            pdf.Space(8);
            pdf.Text("Montos en colones costarricenses (CRC). El saldo coincide con el registrado en Créditos.");
            return pdf.ToArray();
        }

        public static byte[] BuildInvoice(ClientPortalInvoiceViewModel factura)
        {
            var pdf = new SimplePdfDocument($"{Empresa} · Factura {factura.NumeroFactura}");
            pdf.Title($"Factura {factura.NumeroFactura}");
            pdf.Text(Empresa);
            pdf.Rule();
            pdf.Text($"Cliente: {factura.Cliente}", bold: true);
            pdf.Text($"Correo: {factura.Correo}");
            pdf.Text($"Pedido: #{factura.PedidoId}");
            pdf.Text($"Fecha: {factura.FechaFactura:dd/MM/yyyy HH:mm}");
            pdf.Text($"Estado: {factura.Estado}");

            pdf.Heading("Detalle");
            pdf.Mono(SimplePdfDocument.Columns(("Producto", 44), ("Cant.", -6), ("Precio unitario", -20), ("Subtotal", -20)), bold: true);
            foreach (var linea in factura.Lineas)
            {
                pdf.Mono(SimplePdfDocument.Columns(
                    (linea.Producto, 44), (linea.Cantidad.ToString(), -6),
                    (SimplePdfDocument.Money(linea.PrecioUnitario), -20), (SimplePdfDocument.Money(linea.Subtotal), -20)));
            }

            pdf.Rule();
            pdf.Mono(SimplePdfDocument.Columns(("Subtotal", 72), (SimplePdfDocument.Money(factura.Subtotal), -20)));
            pdf.Mono(SimplePdfDocument.Columns(("Impuesto (IVA)", 72), (SimplePdfDocument.Money(factura.Impuesto), -20)));
            pdf.Mono(SimplePdfDocument.Columns(("Total", 72), (SimplePdfDocument.Money(factura.Total), -20)), bold: true);
            pdf.Space(8);
            pdf.Text("Comprobante del portal del cliente. No sustituye una factura electrónica oficial.");
            return pdf.ToArray();
        }

        private static string Amount(decimal amount) =>
            amount.ToString("#,##0.00", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

        private static string SafeFilePart(string value)
        {
            var clean = new string(value.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
            return string.IsNullOrEmpty(clean) ? "comprobante" : clean;
        }
    }
}
