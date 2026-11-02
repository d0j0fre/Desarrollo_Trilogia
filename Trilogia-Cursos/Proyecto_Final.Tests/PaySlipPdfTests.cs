using System.Text;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class PaySlipPdfTests
{
    [Fact]
    public void BuildProducesPdfWithEmployeeAndBreakdown()
    {
        var model = new PaySlipViewModel
        {
            Empleado = "Empleado QA", Periodo = "Octubre", Estado = "Pagada", SalarioBase = 1000,
            TotalBruto = 1200, TotalDeducciones = 100, TotalNeto = 1100,
            Lineas = [new PaySlipLineViewModel { Codigo = "EXTRA", Nombre = "Horas extra", Tipo = "Ingreso", Monto = 200 }]
        };
        var pdf = PaySlipPdf.Build(model);
        var content = Encoding.Latin1.GetString(pdf);
        Assert.StartsWith("%PDF-", content);
        Assert.Contains("Empleado QA", content);
        Assert.Contains("Horas extra", content);
        Assert.Contains("Total neto", content);
    }
}
