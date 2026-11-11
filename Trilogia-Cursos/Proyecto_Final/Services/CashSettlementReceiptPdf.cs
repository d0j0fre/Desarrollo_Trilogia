using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services
{
    /// <summary>BUG-10-03 · CU-106: recibo de la liquidación de cobros de una ruta, en PDF (SimplePdfDocument).</summary>
    public static class CashSettlementReceiptPdf
    {
        public const string Empresa = "Distribuidora JJ / Licorera La Bodega";

        public static string FileName(CashSettlementReceiptViewModel r) => $"recibo-liquidacion-{r.Numero}.pdf";

        public static byte[] Build(CashSettlementReceiptViewModel r)
        {
            static string OrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

            var pdf = new SimplePdfDocument($"{Empresa} · Recibo de liquidación {r.Numero}");
            pdf.Title($"Recibo de liquidación de cobros {r.Numero}");
            pdf.Text($"{Empresa} · Emitido el {r.FechaLiquidacion:dd/MM/yyyy HH:mm}");
            pdf.Rule();

            if (r.Anulada)
            {
                pdf.Text("LIQUIDACIÓN ANULADA", bold: true);
                pdf.Field("Motivo", OrDash(r.MotivoAnulacion));
                pdf.Space(4);
            }

            pdf.Heading("Ruta");
            pdf.Field("Ruta", $"{r.RutaCodigo} · {OrDash(r.Zona)}");
            pdf.Field("Chofer", OrDash(r.ChoferNombre));
            pdf.Field("Vehículo", string.IsNullOrWhiteSpace(r.VehiculoPlaca) ? "-" : $"{r.VehiculoPlaca} · {r.VehiculoDescripcion}");
            pdf.Field("Liquidó", OrDash(r.LiquidadoPorNombre));

            pdf.Heading("Cuadre");
            pdf.Mono(SimplePdfDocument.Columns(("Esperado en efectivo", 34), (SimplePdfDocument.Money(r.MontoEsperadoEfectivo), -22)));
            pdf.Mono(SimplePdfDocument.Columns(("Esperado en otros medios", 34), (SimplePdfDocument.Money(r.MontoEsperadoOtros), -22)));
            pdf.Mono(SimplePdfDocument.Columns(("Total esperado", 34), (SimplePdfDocument.Money(r.MontoEsperadoEfectivo + r.MontoEsperadoOtros), -22)), bold: true);
            pdf.Mono(SimplePdfDocument.Columns(("Efectivo recibido", 34), (SimplePdfDocument.Money(r.MontoEfectivoRecibido), -22)));
            pdf.Mono(SimplePdfDocument.Columns(("Comprobantes recibidos", 34), (SimplePdfDocument.Money(r.MontoComprobantes), -22)));
            pdf.Mono(SimplePdfDocument.Columns(("Diferencia", 34), (SimplePdfDocument.Money(r.Diferencia), -22)), bold: true);
            pdf.Mono(SimplePdfDocument.Columns(("Resultado", 34), (r.Estado, -22)), bold: true);

            pdf.Heading("Comprobantes");
            if (r.Comprobantes.Count == 0)
            {
                pdf.Text("Sin comprobantes: todo el cobro se recibió en efectivo.");
            }
            else
            {
                pdf.Mono(SimplePdfDocument.Columns(("Tipo", 18), ("Referencia", 40), ("Monto", -22)), bold: true);
                foreach (var c in r.Comprobantes)
                    pdf.Mono(SimplePdfDocument.Columns((OrDash(c.Tipo), 18), (OrDash(c.Referencia), 40), (SimplePdfDocument.Money(c.Monto), -22)));
            }

            pdf.Heading("Pedidos entregados");
            if (r.Pedidos.Count == 0)
            {
                pdf.Text("La ruta no tuvo pedidos entregados.");
            }
            else
            {
                pdf.Mono(SimplePdfDocument.Columns(("Pedido", 8), ("Cliente", 34), ("Método de pago", 20), ("Total", -22)), bold: true);
                foreach (var p in r.Pedidos)
                    pdf.Mono(SimplePdfDocument.Columns(($"#{p.PedidoId}", 8), (OrDash(p.Cliente), 34),
                        (PaymentMethodNames.Display(p.MetodoPago), 20), (SimplePdfDocument.Money(p.Total), -22)));
            }

            if (!string.IsNullOrWhiteSpace(r.Observaciones))
            {
                pdf.Heading("Observaciones");
                pdf.Paragraph(r.Observaciones.Trim());
            }

            pdf.Signatures(
                "Chofer", OrDash(r.ChoferNombre),
                "Quien liquida", OrDash(r.LiquidadoPorNombre));
            pdf.Space(8);
            pdf.Text("Montos en colones costarricenses (CRC).");
            return pdf.ToArray();
        }
    }
}
