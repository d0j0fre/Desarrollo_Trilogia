using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

/// <summary>BUG-20-01: la verificación diaria de vencimientos documentales es automática.</summary>
public sealed class DocumentAlertDailyWorkerTests
{
    [Fact]
    public void ShouldRun_FirstCheckAfterStartupRunsImmediately()
    {
        Assert.True(DocumentAlertDailyWorker.ShouldRun(null, new DateTime(2026, 10, 2, 15, 23, 0), 6));
        Assert.True(DocumentAlertDailyWorker.ShouldRun(null, new DateTime(2026, 10, 2, 2, 0, 0), 6));
    }

    [Theory]
    [InlineData("2026-10-02", "2026-10-02 23:30", false)] // ya corrió hoy
    [InlineData("2026-10-02", "2026-10-03 05:59", false)] // día nuevo, antes de la hora diaria
    [InlineData("2026-10-02", "2026-10-03 06:00", true)]  // día nuevo, hora diaria alcanzada
    [InlineData("2026-09-28", "2026-10-03 13:00", true)]  // varios días sin correr
    public void ShouldRun_OncePerBusinessDayFromConfiguredHour(string lastRun, string now, bool expected)
    {
        Assert.Equal(expected, DocumentAlertDailyWorker.ShouldRun(DateTime.Parse(lastRun), DateTime.Parse(now), 6));
    }

    [Fact]
    public void Worker_IsRegisteredAsHostedService()
    {
        var program = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Proyecto_Final", "Program.cs"));
        Assert.Contains("AddHostedService<DocumentAlertDailyWorker>()", program, StringComparison.Ordinal);
    }

    [Fact]
    public void BusinessDateTime_UsesCostaRicaLocalTime()
    {
        var local = DocumentExpirationPolicy.BusinessDateTime(new DateTimeOffset(2026, 10, 2, 21, 23, 0, TimeSpan.Zero));
        Assert.Equal(new DateTime(2026, 10, 2, 15, 23, 0), local);
    }

    [Fact]
    public async Task Notification_AsksTheResponsibleToUpdateTheDocument()
    {
        var candidate = new DocumentAlertNotificationCandidate
        {
            AlertId = 1, DocumentId = 9, ThresholdDays = 30, DocumentTitle = "QA-20 Verificación diaria",
            ExpirationDate = new DateTime(2026, 11, 1), Recipient = "responsable@example.invalid"
        };
        var db = new Mock<IDocumentManagementDbService>();
        db.Setup(x => x.GenerateAlertsAsync(It.IsAny<DateTime>(), It.IsAny<IReadOnlyCollection<int>>(), DocumentAlertDailyWorker.SystemUserId))
            .ReturnsAsync([candidate]);
        var email = new Mock<IDocumentAlertEmailSender>();
        var service = new DocumentAlertService(db.Object, email.Object,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentAlerts:EmailEnabled"] = "true", ["ConfiguracionCorreo:Host"] = "smtp.invalid",
                ["ConfiguracionCorreo:Puerto"] = "587", ["ConfiguracionCorreo:Remitente"] = "sender@example.invalid",
                ["ConfiguracionCorreo:Contrasenna"] = "<smtp-app-password>"
            }).Build(),
            TimeProvider.System, NullLogger<DocumentAlertService>.Instance);

        Assert.Equal(1, await service.GenerateAsync(DocumentAlertDailyWorker.SystemUserId));
        email.Verify(x => x.SendAsync(candidate.Recipient, It.IsAny<string>(),
            It.Is<string>(html => html.Contains("actualice el documento", StringComparison.Ordinal)), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz de la solución.");
    }
}
