using System.ComponentModel.DataAnnotations;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;
using Xunit;

namespace Proyecto_Final.Tests;

// POST-qa3-R3-04 (CU-222-04): los mensajes de validacion de los formularios salian en ingles
// ("The DocumentNumber field is required."). POST-qa3-R3-05 (CU-221-03): una regla de negocio de la base
// ("Ya existe un presupuesto activo...") se perdia detras de "No fue posible crear el presupuesto.".
public class ValidacionEspanolPostQa3Tests
{
    [Theory]
    [InlineData(typeof(RequiredAttribute), "obligatorio")]
    [InlineData(typeof(EmailAddressAttribute), "correo")]
    [InlineData(typeof(PhoneAttribute), "teléfono")]
    [InlineData(typeof(UrlAttribute), "dirección web")]
    public void SimpleAttributes_GetASpanishMessage(Type type, string fragment)
    {
        var attribute = (ValidationAttribute)Activator.CreateInstance(type)!;
        var message = SpanishValidationMetadataProvider.MessageFor(attribute);
        Assert.NotNull(message);
        Assert.Contains(fragment, message, StringComparison.Ordinal);
        Assert.DoesNotContain("The ", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RangeAndLengthAttributes_GetASpanishMessage()
    {
        Assert.Contains("entre {1} y {2}", SpanishValidationMetadataProvider.MessageFor(new RangeAttribute(1, 5))!, StringComparison.Ordinal);
        Assert.Contains("no puede superar {1}", SpanishValidationMetadataProvider.MessageFor(new StringLengthAttribute(80))!, StringComparison.Ordinal);
        Assert.Contains("entre {2} y {1}", SpanishValidationMetadataProvider.MessageFor(new StringLengthAttribute(80) { MinimumLength = 3 })!, StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_OnlyFillsAttributesWithoutTheirOwnMessage()
    {
        var propio = new RequiredAttribute { ErrorMessage = "Mensaje propio." };
        var sinMensaje = new RequiredAttribute();
        SpanishValidationMetadataProvider.Apply(propio);
        SpanishValidationMetadataProvider.Apply(sinMensaje);

        Assert.Equal("Mensaje propio.", propio.ErrorMessage);
        Assert.Equal("El campo {0} es obligatorio.", sinMensaje.ErrorMessage);
    }

    [Fact]
    public void ExpenseForm_FieldsHaveSpanishDisplayNames()
    {
        foreach (var (property, name) in new[] { ("DocumentNumber", "N.º de documento"), ("Description", "Descripción"), ("Subtotal", "Subtotal"), ("PaymentMethod", "Método de pago") })
        {
            var display = typeof(OperatingExpenseFormViewModel).GetProperty(property)!
                .GetCustomAttributes(typeof(DisplayAttribute), false).Cast<DisplayAttribute>().Single();
            Assert.Equal(name, display.Name);
        }
    }

    [Fact]
    public void ProviderIsRegisteredAndBusinessRulesShowTheirOwnText()
    {
        var program = File.ReadAllText(SourcePath("Proyecto_Final", "Program.cs"));
        Assert.Contains("SpanishValidationMetadataProvider", program, StringComparison.Ordinal);
        foreach (var controller in new[] { "BudgetsController.cs", "ExpensesController.cs", "DocumentsController.cs" })
        {
            var source = File.ReadAllText(SourcePath("Proyecto_Final", "Controllers", controller));
            Assert.Contains("SqlException { Number: >= 50000 } rule", source, StringComparison.Ordinal);
        }
    }

    // POST-qa3-R3-06 (CU-261-05): al pasar el limite de consultas el asistente decia "Sin conexion".
    [Fact]
    public void AssistantShowsAWaitMessageWhenTheRateLimitIsReached()
    {
        var view = File.ReadAllText(SourcePath("Proyecto_Final", "Views", "Assistant", "Index.cshtml"));
        Assert.Contains("response.status === 429", view, StringComparison.Ordinal);
        Assert.Contains("Alcanzó el límite de 20 consultas por minuto", view, StringComparison.Ordinal);
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
