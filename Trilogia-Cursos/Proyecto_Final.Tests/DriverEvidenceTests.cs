using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_Final.Services;
using Proyecto_FinalAPI.Controllers;
using Proyecto_FinalAPI.Models;
using Proyecto_FinalAPI.Services.Mobile;

namespace Proyecto_Final.Tests;

/// <summary>
/// G3-04: foto y firma de la entrega desde la app del chofer
/// (POST api/mobile/v1/driver/deliveries/{id}/evidence).
/// </summary>
public sealed class DriverEvidenceTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    // ── Servicio: mismo almacenamiento y mismo orden que la web ─────────────

    [Fact]
    public async Task UnaFotoValidaSeGuardaYQuedaLeibleConElServicioDeLaWeb()
    {
        await WithServiceAsync(async (service, db, storage, _) =>
        {
            var syncGuid = Guid.NewGuid();
            db.Setup(d => d.PrepareEvidenceAsync(10, 7, syncGuid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EvidenceTarget(RutaId: 3, PedidoId: 55, ExistingEvidenceId: null));
            db.Setup(d => d.RegisterEvidenceAsync(
                    55, 3, "Foto", It.IsRegex("^[a-f0-9]{32}\\.jpg$"), "image/jpeg", "Frente al local",
                    7, "Chofer", syncGuid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(901);

            var result = await service.RegisterAsync(
                10, 7, "Chofer", "foto", syncGuid, FormFile("evidencia.jpg", "image/jpeg", Jpeg), "Frente al local");

            Assert.Equal(new DriverEvidenceResult(901, "Foto", Duplicado: false), result);
            db.Verify(d => d.MarkEvidenceReadyAsync(901, 7, It.IsAny<CancellationToken>()), Times.Once);

            // El archivo definitivo existe y la web lo abriría con la misma clave.
            var key = (string)db.Invocations.First(i => i.Method.Name == nameof(IDriverMobileDbService.RegisterEvidenceAsync)).Arguments[3];
            await using var stream = await storage.OpenReadAsync(key);
            Assert.NotNull(stream);
        });
    }

    [Fact]
    public async Task LaFirmaSeGuardaComoPng()
    {
        await WithServiceAsync(async (service, db, _, _) =>
        {
            db.Setup(d => d.PrepareEvidenceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EvidenceTarget(3, 55, null));
            db.Setup(d => d.RegisterEvidenceAsync(
                    55, 3, "Firma", It.IsRegex("\\.png$"), "image/png", null, 7, "Chofer", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(902);

            var result = await service.RegisterAsync(
                10, 7, "Chofer", "Firma", Guid.NewGuid(), FormFile("firma.png", "image/png", Png), null);

            Assert.Equal(902, result.EvidenciaId);
            Assert.Equal("Firma", result.Tipo);
        });
    }

    [Fact]
    public async Task ElReenvioDeUnArchivoYaRegistradoNoLoGuardaDeNuevo()
    {
        await WithServiceAsync(async (service, db, _, root) =>
        {
            db.Setup(d => d.PrepareEvidenceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EvidenceTarget(3, 55, ExistingEvidenceId: 444));

            var result = await service.RegisterAsync(
                10, 7, "Chofer", "Foto", Guid.NewGuid(), FormFile("evidencia.jpg", "image/jpeg", Jpeg), null);

            Assert.Equal(new DriverEvidenceResult(444, "Foto", Duplicado: true), result);
            db.Verify(d => d.RegisterEvidenceAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "files")));
        });
    }

    [Fact]
    public async Task UnaParadaAjenaNoTocaElDisco()
    {
        await WithServiceAsync(async (service, db, _, root) =>
        {
            db.Setup(d => d.PrepareEvidenceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(SqlErrors.Create(52086, "No pertenece a este chofer."));

            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => service.RegisterAsync(
                10, 7, "Chofer", "Foto", Guid.NewGuid(), FormFile("evidencia.jpg", "image/jpeg", Jpeg), null));

            Assert.Empty(Directory.GetFiles(Path.Combine(root, "staging")));
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "files")));
            db.Verify(d => d.RegisterEvidenceAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);
        });
    }

    [Theory]
    [InlineData("evidencia.exe", "image/jpeg")]   // extensión no admitida
    [InlineData("evidencia.gif", "image/gif")]    // formato no admitido
    [InlineData("evidencia.jpg", "image/png")]    // tipo declarado distinto de la extensión
    public async Task UnArchivoQueNoEsUnaImagenAdmitidaSeRechazaYNoDejaNada(string name, string contentType)
    {
        await WithServiceAsync(async (service, db, _, root) =>
        {
            db.Setup(d => d.PrepareEvidenceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EvidenceTarget(3, 55, null));

            await Assert.ThrowsAsync<EvidenceValidationException>(() => service.RegisterAsync(
                10, 7, "Chofer", "Foto", Guid.NewGuid(), FormFile(name, contentType, Jpeg), null));

            Assert.Empty(Directory.GetFiles(Path.Combine(root, "files")));
        });
    }

    [Fact]
    public async Task UnArchivoQueSoloDiceSerJpegPeroNoLoEsSeRechaza()
    {
        await WithServiceAsync(async (service, db, _, _) =>
        {
            db.Setup(d => d.PrepareEvidenceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EvidenceTarget(3, 55, null));

            var disfrazado = "<script>alert(1)</script>"u8.ToArray();
            await Assert.ThrowsAsync<EvidenceValidationException>(() => service.RegisterAsync(
                10, 7, "Chofer", "Foto", Guid.NewGuid(), FormFile("evidencia.jpg", "image/jpeg", disfrazado), null));
        });
    }

    [Fact]
    public async Task UnArchivoMayorAlLimiteDeLaWebSeRechaza()
    {
        await WithServiceAsync(async (service, db, _, _) =>
        {
            db.Setup(d => d.PrepareEvidenceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EvidenceTarget(3, 55, null));

            var enorme = new byte[(int)FileEvidenceStorageService.MaximumBytes + 1];
            Jpeg.CopyTo(enorme, 0);
            await Assert.ThrowsAsync<EvidenceValidationException>(() => service.RegisterAsync(
                10, 7, "Chofer", "Foto", Guid.NewGuid(), FormFile("evidencia.jpg", "image/jpeg", enorme), null));
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Video")]
    public async Task UnTipoDeEvidenciaDesconocidoSeRechazaAntesDeConsultarLaBase(string? tipo)
    {
        await WithServiceAsync(async (service, db, _, _) =>
        {
            await Assert.ThrowsAsync<EvidenceValidationException>(() => service.RegisterAsync(
                10, 7, "Chofer", tipo, Guid.NewGuid(), FormFile("evidencia.jpg", "image/jpeg", Jpeg), null));

            db.VerifyNoOtherCalls();
        });
    }

    [Fact]
    public async Task LaFirmaDebeSerPng()
    {
        await WithServiceAsync(async (service, db, _, _) =>
        {
            await Assert.ThrowsAsync<EvidenceValidationException>(() => service.RegisterAsync(
                10, 7, "Chofer", "Firma", Guid.NewGuid(), FormFile("firma.jpg", "image/jpeg", Jpeg), null));

            db.VerifyNoOtherCalls();
        });
    }

    [Fact]
    public async Task SinArchivoSeRechaza()
    {
        await WithServiceAsync(async (service, db, _, _) =>
        {
            await Assert.ThrowsAsync<EvidenceValidationException>(() => service.RegisterAsync(
                10, 7, "Chofer", "Foto", Guid.NewGuid(), null, null));

            db.VerifyNoOtherCalls();
        });
    }

    [Fact]
    public async Task SiLaFilaNoSePuedeRegistrarNoQuedaArchivoTemporal()
    {
        await WithServiceAsync(async (service, db, _, root) =>
        {
            db.Setup(d => d.PrepareEvidenceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EvidenceTarget(3, 55, null));
            db.Setup(d => d.RegisterEvidenceAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(SqlErrors.Create(52084, "No autorizado."));

            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => service.RegisterAsync(
                10, 7, "Chofer", "Foto", Guid.NewGuid(), FormFile("evidencia.jpg", "image/jpeg", Jpeg), null));

            Assert.Empty(Directory.GetFiles(Path.Combine(root, "staging")));
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "files")));
        });
    }

    [Fact]
    public async Task SiNoSePuedeConfirmarLaFilaPendienteSeCompensa()
    {
        await WithServiceAsync(async (_, db, _, _) =>
        {
            db.Setup(d => d.PrepareEvidenceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EvidenceTarget(3, 55, null));
            db.Setup(d => d.RegisterEvidenceAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(903);

            // Un almacenamiento que falla al mover el archivo definitivo: la fila Pending no puede quedar viva.
            var failing = new Mock<IEvidenceStorageService>();
            failing.Setup(s => s.StageUploadAsync(It.IsAny<IFormFile?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StagedEvidence("0123456789abcdef0123456789abcdef.stage", "fedcba9876543210fedcba9876543210.jpg", "image/jpeg", ".jpg", 12));
            failing.Setup(s => s.CommitAsync(It.IsAny<StagedEvidence>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("disco lleno"));
            var service2 = new DriverEvidenceService(db.Object, failing.Object, NullLogger<DriverEvidenceService>.Instance);

            await Assert.ThrowsAsync<IOException>(() => service2.RegisterAsync(
                10, 7, "Chofer", "Foto", Guid.NewGuid(), FormFile("evidencia.jpg", "image/jpeg", Jpeg), null));

            db.Verify(d => d.DeletePendingEvidenceAsync(903, 7, It.IsAny<CancellationToken>()), Times.Once);
            failing.Verify(s => s.DeleteStageAsync(It.IsAny<StagedEvidence>()), Times.Once);
            db.Verify(d => d.MarkEvidenceReadyAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    [Fact]
    public async Task SinCarpetaConfiguradaLaApiNoGuardaEnSuPropioDisco()
    {
        // Guardar donde la web no lo ve sería perder la foto en silencio.
        var storage = new UnconfiguredEvidenceStorageService();
        await Assert.ThrowsAsync<EvidenceStorageNotConfiguredException>(() =>
            storage.StageUploadAsync(FormFile("evidencia.jpg", "image/jpeg", Jpeg)));
    }

    // ── Endpoint ────────────────────────────────────────────────────────────

    [Fact]
    public void ElEndpointDeEvidenciaTieneLimiteDePeticionesYTopeDeTamano()
    {
        var method = typeof(MobileDriverController).GetMethod(nameof(MobileDriverController.RegisterEvidence))!;

        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<EnableRateLimitingAttribute>());
        var limit = method.GetCustomAttribute<RequestSizeLimitAttribute>();
        Assert.NotNull(limit);
        // Los mismos 6 MB que el formulario de la web.
        Assert.Equal(6L * 1024 * 1024, Convert.ToInt64(limit!.GetType().GetField("_bytes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(limit)));
        Assert.True(typeof(MobileDriverController).GetCustomAttributes<AuthorizeAttribute>(true).Any());
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task SinSyncGuidValidoSeRechazaSinTocarNada(string syncGuid)
    {
        var evidence = new Mock<IDriverEvidenceService>(MockBehavior.Strict);
        var controller = BuildController(evidence);

        var result = await controller.RegisterEvidence(
            10,
            new RegisterEvidenceRequest { Tipo = "Foto", SyncGuid = syncGuid },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        evidence.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ElChoferSaleDelTokenNoDelCuerpo()
    {
        var evidence = new Mock<IDriverEvidenceService>();
        evidence.Setup(e => e.RegisterAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid>(),
                It.IsAny<IFormFile?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriverEvidenceResult(5, "Foto", false));
        var controller = BuildController(evidence, userId: 77);

        var result = await controller.RegisterEvidence(
            10,
            new RegisterEvidenceRequest { Tipo = "Foto", SyncGuid = Guid.NewGuid().ToString() },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(5, Assert.IsType<RegisterEvidenceResponse>(ok.Value).EvidenciaId);
        evidence.Verify(e => e.RegisterAsync(
            10, 77, "Chofer de Prueba", "Foto", It.IsAny<Guid>(), It.IsAny<IFormFile?>(), null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UnaEvidenciaDuplicadaNoSeAudita()
    {
        var evidence = new Mock<IDriverEvidenceService>();
        evidence.Setup(e => e.RegisterAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid>(),
                It.IsAny<IFormFile?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriverEvidenceResult(5, "Foto", true));
        var audit = new Mock<IMobileAuditService>();
        var controller = BuildController(evidence, audit);

        var result = await controller.RegisterEvidence(
            10, new RegisterEvidenceRequest { Tipo = "Foto", SyncGuid = Guid.NewGuid().ToString() }, CancellationToken.None);

        Assert.True(Assert.IsType<RegisterEvidenceResponse>(Assert.IsType<OkObjectResult>(result).Value).Duplicado);
        audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnaEvidenciaNuevaSeAuditaConCanalMovil()
    {
        var evidence = new Mock<IDriverEvidenceService>();
        evidence.Setup(e => e.RegisterAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid>(),
                It.IsAny<IFormFile?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriverEvidenceResult(5, "Firma", false));
        var audit = new Mock<IMobileAuditService>();
        var controller = BuildController(evidence, audit);

        await controller.RegisterEvidence(
            10, new RegisterEvidenceRequest { Tipo = "Firma", SyncGuid = Guid.NewGuid().ToString() }, CancellationToken.None);

        audit.Verify(a => a.RecordAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            "Registrar evidencia", "Entregas", It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UnArchivoInvalidoEsReglaDeNegocioParaQueLaColaNoLoReintente()
    {
        var evidence = new Mock<IDriverEvidenceService>();
        evidence.Setup(e => e.RegisterAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid>(),
                It.IsAny<IFormFile?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EvidenceValidationException("La imagen supera el tamaño máximo permitido de 5 MB."));

        var result = await BuildController(evidence).RegisterEvidence(
            10, new RegisterEvidenceRequest { Tipo = "Foto", SyncGuid = Guid.NewGuid().ToString() }, CancellationToken.None);

        Assert.IsType<UnprocessableEntityObjectResult>(result);
    }

    [Theory]
    [InlineData(52086, typeof(NotFoundObjectResult))]          // parada ajena o inexistente: igual que si no existiera
    [InlineData(52087, typeof(UnprocessableEntityObjectResult))] // ruta que ya no admite evidencia
    [InlineData(52088, typeof(BadRequestObjectResult))]         // SyncGuid de otro chofer
    public async Task LosRechazosDeSqlSeTraducenSinFiltrarDetalle(int sqlNumber, Type expected)
    {
        var evidence = new Mock<IDriverEvidenceService>();
        evidence.Setup(e => e.RegisterAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid>(),
                It.IsAny<IFormFile?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(SqlErrors.Create(sqlNumber, "detalle interno"));

        var result = await BuildController(evidence).RegisterEvidence(
            10, new RegisterEvidenceRequest { Tipo = "Foto", SyncGuid = Guid.NewGuid().ToString() }, CancellationToken.None);

        Assert.IsType(expected, result);
        Assert.DoesNotContain("detalle interno", System.Text.Json.JsonSerializer.Serialize(((ObjectResult)result).Value));
    }

    [Fact]
    public async Task SinAlmacenamientoConfiguradoResponde503ParaQueLaAppConserveElArchivo()
    {
        var evidence = new Mock<IDriverEvidenceService>();
        evidence.Setup(e => e.RegisterAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid>(),
                It.IsAny<IFormFile?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EvidenceStorageNotConfiguredException());

        var result = await BuildController(evidence).RegisterEvidence(
            10, new RegisterEvidenceRequest { Tipo = "Foto", SyncGuid = Guid.NewGuid().ToString() }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public void ElServicioMovilUsaLosMismosProcedimientosDeEvidenciaQueElMvc()
    {
        var mobile = File.ReadAllText(SourcePath("Proyecto_FinalAPI", "Services", "Mobile", "DriverMobileDbService.cs"));
        var mvc = File.ReadAllText(SourcePath("Proyecto_Final", "Services", "LogisticsDbService.cs"));

        foreach (var procedure in new[]
                 {
                     "dbo.sp_Entrega_RegisterEvidence",
                     "dbo.sp_Entrega_MarkEvidenceReady",
                     "dbo.sp_Entrega_DeletePendingEvidence"
                 })
        {
            Assert.Contains(procedure, mobile, StringComparison.Ordinal);
            Assert.Contains(procedure, mvc, StringComparison.Ordinal);
        }
    }

    // ── Ayudantes ───────────────────────────────────────────────────────────

    private static MobileDriverController BuildController(
        Mock<IDriverEvidenceService> evidence,
        Mock<IMobileAuditService>? audit = null,
        int userId = 7) =>
        new(
            new Mock<IDriverMobileDbService>().Object,
            evidence.Object,
            (audit ?? new Mock<IMobileAuditService>()).Object,
            NullLogger<MobileDriverController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(
                        [
                            new System.Security.Claims.Claim("sub", userId.ToString()),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Chofer"),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "Chofer de Prueba")
                        ], "Bearer"))
                }
            }
        };

    private static FormFile FormFile(string fileName, string contentType, byte[] bytes) =>
        new(new MemoryStream(bytes), 0, bytes.Length, "archivo", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

    private static async Task WithServiceAsync(
        Func<DriverEvidenceService, Mock<IDriverMobileDbService>, FileEvidenceStorageService, string, Task> test)
    {
        var taskRoot = Path.Combine(Path.GetTempPath(), "TrilogiaDriverEvidenceTests", Guid.NewGuid().ToString("N"));
        var evidenceRoot = Path.Combine(taskRoot, "evidence");
        Directory.CreateDirectory(taskRoot);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["EvidenceStorage:RootPath"] = evidenceRoot })
                .Build();
            var storage = new FileEvidenceStorageService(configuration, new TestEnvironment(taskRoot));
            var db = new Mock<IDriverMobileDbService>();
            var service = new DriverEvidenceService(db.Object, storage, NullLogger<DriverEvidenceService>.Instance);
            await test(service, db, storage, evidenceRoot);
        }
        finally
        {
            if (Directory.Exists(taskRoot)) Directory.Delete(taskRoot, recursive: true);
        }
    }

    private static string SourcePath(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", .. parts]);

    private sealed class TestEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Proyecto_Final.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
