using Microsoft.Extensions.Configuration;
using Proyecto_Final.Controllers;
using Proyecto_Final.Middleware;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class EmailAndRecoveryFixTests
{
    [Theory]
    [InlineData(0.2, 1)]
    [InlineData(1, 1)]
    [InlineData(14.1, 15)]
    [InlineData(15, 15)]
    public void RecoveryLimit_RoundsWaitUpToWholeMinutes(double minutes, int expected)
    {
        Assert.Equal(expected, PasswordRecoveryRateLimitResponse.WaitMinutes(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void RecoveryLimit_MessageTellsHowLongToWait()
    {
        Assert.Contains("en 15 minutos", PasswordRecoveryRateLimitResponse.BuildMessage(15));
        Assert.Contains("en 1 minuto.", PasswordRecoveryRateLimitResponse.BuildMessage(1));
    }

    [Theory]
    [InlineData("Pendiente", "Cerrada", true)]
    [InlineData("Pendiente", "Atendida", true)]
    [InlineData("Atendida", "Cerrada", true)]
    [InlineData("Cerrada", "Cerrada", false)]
    [InlineData("Atendida", "Atendida", false)]
    [InlineData("Atendida", "Pendiente", false)]
    public void Consultation_NotifiesOnlyWhenItBecomesResolved(string anterior, string nuevo, bool expected)
    {
        Assert.Equal(expected, ConsultationsController.IsResolution(anterior, nuevo));
    }

    [Fact]
    public void ContactConfirmation_EncodesClientInput()
    {
        var html = EmailTemplateBuilder.BuildContactConfirmationEmail("<b>Ana</b>", "Pedido", "Hola\n<script>x</script>");

        Assert.Contains("&lt;b&gt;Ana&lt;/b&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("Hola<br />", html);
    }

    [Fact]
    public void ConsultationResolved_ShowsStateWithoutInternalNotes()
    {
        var html = EmailTemplateBuilder.BuildConsultationResolvedEmail("Ana", "QA-03 Consulta de pedido", "Cerrada");

        Assert.Contains("Cerrada", html);
        Assert.Contains("QA-03 Consulta de pedido", html);
    }

    [Fact]
    public void EmailService_WithPickupDirectory_WritesMessageInsteadOfSending()
    {
        var pickup = Path.Combine(Path.GetTempPath(), "trilogia-mail-" + Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConfiguracionCorreo:Host"] = "localhost",
            ["ConfiguracionCorreo:Puerto"] = "25",
            ["ConfiguracionCorreo:Remitente"] = "no-reply@labodega.test",
            ["ConfiguracionCorreo:Contrasenna"] = "solo-pruebas",
            ["ConfiguracionCorreo:DirectorioRecogida"] = pickup
        }).Build();

        new EmailService(configuration).SendEmail("cliente@example.com", "Prueba", "<p>Hola</p>");

        var correo = Assert.Single(Directory.GetFiles(pickup, "*.eml"));
        Assert.Contains("To: cliente@example.com", File.ReadAllText(correo));
    }
}
