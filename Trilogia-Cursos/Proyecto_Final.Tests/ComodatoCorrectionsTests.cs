using System.Text;
using System.Text.RegularExpressions;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

// BUG-16-01 — contrato de comodato en PDF.
public sealed class ComodatoCorrectionsTests
{
    private static ComodatoContractViewModel Contrato(string? observaciones = null) => new()
    {
        ComodatoId = 7,
        CodigoActivo = "NEV-QA-001",
        ActivoNombre = "QA-16 Nevera vertical 2 puertas",
        ActivoTipo = "Nevera",
        ClienteNombre = "Pulpería El Tucán",
        ClienteIdentificacion = "101230456",
        Ubicacion = "QA-16 Local de prueba, San José",
        FechaAsignacion = new DateTime(2026, 10, 30),
        CondicionEntrega = "QA-16 Buen estado (sin golpes)",
        Observaciones = observaciones ?? "Incluye 3 parrillas \\ estantes",
        RegistradoPorNombre = "Admin Demo",
        FechaRegistro = new DateTime(2026, 10, 30, 9, 15, 0),
        Estado = "Activo"
    };

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    [Fact]
    public void Contract_IsAWellFormedPdfWithAValidCrossReferenceTable()
    {
        var bytes = ComodatoContractPdf.Build(Contrato());
        var text = Latin1(bytes);

        Assert.StartsWith("%PDF-1.4", text);
        Assert.EndsWith("%%EOF\n", text);

        // startxref apunta a la tabla y cada entrada apunta a "N 0 obj".
        var startxref = int.Parse(Regex.Match(text, @"startxref\n(\d+)\n%%EOF").Groups[1].Value);
        Assert.StartsWith("xref", text[startxref..]);
        var entries = Regex.Matches(text[startxref..], @"(\d{10}) 00000 n ");
        Assert.NotEmpty(entries);
        for (var i = 0; i < entries.Count; i++)
        {
            var offset = int.Parse(entries[i].Groups[1].Value);
            Assert.StartsWith($"{i + 1} 0 obj", text[offset..]);
        }

        // La longitud declarada de cada contenido coincide con sus bytes.
        foreach (Match stream in Regex.Matches(text, @"<< /Length (\d+) >>\nstream\n"))
        {
            var length = int.Parse(stream.Groups[1].Value);
            var start = stream.Index + stream.Length;
            Assert.Equal("\nendstream", text.Substring(start + length, 10));
        }
    }

    [Fact]
    public void Contract_ContainsTheComodatoDataWithSpanishCharactersInWinAnsi()
    {
        var text = Latin1(ComodatoContractPdf.Build(Contrato()));

        Assert.Contains("CONTRATO DE COMODATO DE EQUIPO", text);
        Assert.Contains("CMD-00007", text);
        Assert.Contains("NEV-QA-001", text);
        Assert.Contains("Pulpería El Tucán", text);          // í y á como bytes WinAnsi de un solo byte
        Assert.Contains("101230456", text);
        Assert.Contains("QA-16 Local de prueba, San José", text);
        Assert.Contains("30/10/2026", text);
        Assert.Contains("QA-16 Buen estado \\(sin golpes\\)", text);   // paréntesis escapados
        Assert.Contains("parrillas \\\\ estantes", text);             // barra invertida escapada
        Assert.Contains("/Encoding /WinAnsiEncoding", text);
        Assert.Contains("Comodatario", text);
        Assert.Equal("contrato-comodato-CMD-00007.pdf", ComodatoContractPdf.FileName(Contrato()));
    }

    [Fact]
    public void Contract_WithLongTextBreaksIntoSeveralPagesAndNumbersThem()
    {
        var largo = string.Join(" ", Enumerable.Repeat("Observación extensa del equipo entregado.", 400));
        var text = Latin1(ComodatoContractPdf.Build(Contrato(largo)));

        var pages = int.Parse(Regex.Match(text, @"/Type /Pages /Kids \[[^\]]*\] /Count (\d+)").Groups[1].Value);
        Assert.True(pages > 1);
        Assert.Contains($"Página {pages} de {pages}", text);
    }

    [Fact]
    public void Wrap_KeepsEveryLineInsideTheGivenWidth()
    {
        var lines = SimplePdfWriter.Wrap(
            "El comodatario usará el equipo con diligencia, lo mantendrá limpio y conectado según las indicaciones del comodante.",
            10, bold: false, width: 200);

        Assert.True(lines.Count > 1);
        Assert.All(lines, line => Assert.True(SimplePdfWriter.TextWidth(line, 10, false) <= 200));
    }
}
