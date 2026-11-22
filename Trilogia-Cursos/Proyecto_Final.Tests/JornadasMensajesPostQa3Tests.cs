using System.ComponentModel.DataAnnotations;
using Proyecto_Final.Models.Admin;
using Xunit;

namespace Proyecto_Final.Tests;

// POST-qa3-R3-01 (CU-112-03): una hora negativa o mayor a 24 se rechazaba sin ningún mensaje en Mis jornadas.
// POST-qa3-R3-02 (CU-111-03): "The value '' is invalid." salía en inglés al dejar el rol vacío.
public class JornadasMensajesPostQa3Tests
{
    [Theory]
    [InlineData(-2, 0, 0, "ordinarias")]
    [InlineData(0, -1, 0, "extra")]
    [InlineData(0, 0, 30, "ausencia")]
    public void OutOfRangeHours_AlwaysCarryASpanishMessage(decimal ordinarias, decimal extra, decimal ausencia, string campo)
    {
        var model = new AttendanceFormViewModel { HorasOrdinarias = ordinarias, HorasExtra = extra, HorasAusencia = ausencia };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true));

        var message = Assert.Single(results).ErrorMessage;
        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.Contains(campo, message, StringComparison.Ordinal);
        Assert.Contains("entre 0 y 24", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeErrorMessage_RemovesDuplicatesAndNeverReturnsEmpty()
    {
        Assert.Equal("La suma de horas no puede superar 24 para una fecha.",
            AttendanceRules.ComposeErrorMessage(["La suma de horas no puede superar 24 para una fecha.", "La suma de horas no puede superar 24 para una fecha.", ""]));
        Assert.False(string.IsNullOrWhiteSpace(AttendanceRules.ComposeErrorMessage(["", null])));
        Assert.Contains("entre 0 y 24", AttendanceRules.ComposeErrorMessage([]), StringComparison.Ordinal);
    }

    [Fact]
    public void SaveUsesTheComposedMessage()
    {
        var source = File.ReadAllText(SourcePath("Proyecto_Final", "Controllers", "MyAttendanceController.cs"));
        Assert.Contains("AttendanceRules.ComposeErrorMessage(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ModelBindingMessagesAreInSpanish()
    {
        var program = File.ReadAllText(SourcePath("Proyecto_Final", "Program.cs"));
        Assert.Contains("SetValueIsInvalidAccessor", program, StringComparison.Ordinal);
        Assert.Contains("Seleccione o escriba un valor válido.", program, StringComparison.Ordinal);
        Assert.Contains("SetValueMustBeANumberAccessor", program, StringComparison.Ordinal);
    }

    private static string SourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
        return Path.Combine([root, .. parts]);
    }
}
