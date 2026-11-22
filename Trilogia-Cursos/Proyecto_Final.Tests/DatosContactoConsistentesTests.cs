using System.Text.RegularExpressions;

namespace Proyecto_Final.Tests;

/// <summary>
/// POST-qa1-05: la página Contacto y el pie de página publicaban un correo y un horario distintos
/// (info@labodega.com y 8:00 p. m. contra contacto@supermercadomayoreo.cr y 6:00 p. m.).
/// Estas pruebas cuidan que ambos lugares muestren los mismos datos.
/// </summary>
public sealed class DatosContactoConsistentesTests
{
    private static string View(params string[] parts) =>
        File.ReadAllText(Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", "Proyecto_Final", "Views", .. parts]));

    private static readonly Regex Email = new(@"[\w.+-]+@[\w-]+\.[\w.]+");

    [Fact]
    public void ContactPage_AndFooter_ShowTheSameEmail()
    {
        var contacto = Email.Matches(View("Home", "Contact.cshtml")).Select(m => m.Value).Distinct().ToList();
        var pie = Email.Matches(View("Shared", "_Layout.cshtml")).Select(m => m.Value).Where(v => !v.Contains("example")).Distinct().ToList();

        Assert.Single(contacto);
        Assert.Contains(contacto[0], pie);
    }

    [Fact]
    public void ContactPage_AndFooter_ShowTheSameHours()
    {
        const string horario = "8:00 a.m. - 6:00 p.m.";

        Assert.Contains(horario, View("Home", "Contact.cshtml"));
        Assert.Contains(horario, View("Shared", "_Layout.cshtml"));
    }
}
