using Proyecto_Final.Models.Admin;

namespace Proyecto_Final.Services;

public static class PaySlipPdf
{
    public static byte[] Build(PaySlipViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var pdf = new SimplePdfDocument("Boleta de pago privada");
        pdf.Title("Boleta de pago");
        pdf.Field("Empleado", model.Empleado);
        pdf.Field("Periodo", model.Periodo);
        pdf.Field("Estado", model.Estado);
        pdf.Rule();
        pdf.Heading("Desglose");
        foreach (var line in model.Lineas)
            pdf.Paragraph($"{line.Codigo} · {line.Nombre} ({line.Tipo}): {SimplePdfDocument.Money(line.Monto)}");
        pdf.Rule();
        pdf.Text($"Salario base: {SimplePdfDocument.Money(model.SalarioBase)}");
        pdf.Text($"Total bruto: {SimplePdfDocument.Money(model.TotalBruto)}");
        pdf.Text($"Deducciones: {SimplePdfDocument.Money(model.TotalDeducciones)}");
        pdf.Text($"Total neto: {SimplePdfDocument.Money(model.TotalNeto)}", bold: true);
        return pdf.ToArray();
    }
}
