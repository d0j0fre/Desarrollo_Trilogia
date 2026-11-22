using System.Data;
using System.Text.RegularExpressions;
using Proyecto_Final.Services;
using Xunit;

namespace Proyecto_Final.Tests;

// POST-qa3-06: las horas del sitio salían en UTC (6 horas adelante de Costa Rica) porque Azure corre en UTC.
// Estas pruebas fijan la convención de CostaRicaClock y vigilan que no vuelva a entrar DateTime.Now/Today
// ni una lectura de fecha sin pasar por el reloj.
[Collection("CostaRicaClock")]
public sealed class HorasCostaRicaTests : IDisposable
{
    private readonly TimeProvider _providerOriginal = CostaRicaClock.Provider;
    private readonly TimeSpan _offsetOriginal = CostaRicaClock.DatabaseUtcOffset;

    public void Dispose()
    {
        CostaRicaClock.Provider = _providerOriginal;
        CostaRicaClock.DatabaseUtcOffset = _offsetOriginal;
    }

    // ---- Reloj ----------------------------------------------------------------------------------------------

    [Fact]
    public void ServerInUtc_At0509Utc_ShowsTheCostaRicaNightBefore()
    {
        // El caso de POST-qa3-06: 11:09 p. m. del 21/11 en Costa Rica = 05:09 UTC del 22/11.
        CostaRicaClock.Provider = new FixedTimeProvider(new DateTimeOffset(2026, 11, 22, 5, 9, 0, TimeSpan.Zero));

        Assert.Equal(new DateTime(2026, 11, 21, 23, 9, 0), CostaRicaClock.Now);
        Assert.Equal(new DateTime(2026, 11, 21), CostaRicaClock.Today);
        Assert.Equal(DateTimeKind.Unspecified, CostaRicaClock.Now.Kind);
    }

    [Fact]
    public void Today_ChangesAtMidnightOfCostaRica_NotAtMidnightUtc()
    {
        CostaRicaClock.Provider = new FixedTimeProvider(new DateTimeOffset(2026, 11, 22, 5, 59, 59, TimeSpan.Zero));
        Assert.Equal(new DateTime(2026, 11, 21), CostaRicaClock.Today);

        CostaRicaClock.Provider = new FixedTimeProvider(new DateTimeOffset(2026, 11, 22, 6, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateTime(2026, 11, 22), CostaRicaClock.Today);
    }

    [Fact]
    public void CostaRica_HasNoDaylightSaving_SameOffsetAllYear()
    {
        Assert.Equal(new DateTime(2026, 1, 15, 8, 0, 0), CostaRicaClock.FromUtc(new DateTime(2026, 1, 15, 14, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 7, 15, 8, 0, 0), CostaRicaClock.FromUtc(new DateTime(2026, 7, 15, 14, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void FromUtc_TreatsUnspecifiedAndUtcTheSame_AndLeavesSentinelValuesAlone()
    {
        var unspecified = new DateTime(2026, 11, 22, 5, 9, 0, DateTimeKind.Unspecified);
        var utc = new DateTime(2026, 11, 22, 5, 9, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 11, 21, 23, 9, 0), CostaRicaClock.FromUtc(unspecified));
        Assert.Equal(new DateTime(2026, 11, 21, 23, 9, 0), CostaRicaClock.FromUtc(utc));
        Assert.Equal(DateTime.MinValue, CostaRicaClock.FromUtc(DateTime.MinValue));
        Assert.Null(CostaRicaClock.FromUtc((DateTime?)null));
    }

    [Fact]
    public void ToCostaRica_ConvertsUtcPropertiesAndDateTimeOffsets()
    {
        Assert.Equal(new DateTime(2026, 11, 21, 23, 9, 0), new DateTime(2026, 11, 22, 5, 9, 0, DateTimeKind.Utc).ToCostaRica());
        Assert.Equal(new DateTime(2026, 11, 21, 23, 9, 0), new DateTimeOffset(2026, 11, 22, 5, 9, 0, TimeSpan.Zero).ToCostaRica());
        // Con otro desplazamiento sigue siendo el mismo instante.
        Assert.Equal(new DateTime(2026, 11, 21, 23, 9, 0), new DateTimeOffset(2026, 11, 22, 7, 9, 0, TimeSpan.FromHours(2)).ToCostaRica());
        DateTime? none = null;
        Assert.Null(none.ToCostaRica());
    }

    [Fact]
    public void ToUtc_IsTheInverseOfFromUtc()
    {
        var cr = new DateTime(2026, 11, 21, 23, 9, 0);

        var utc = CostaRicaClock.ToUtc(cr);

        Assert.Equal(new DateTime(2026, 11, 22, 5, 9, 0), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(cr, CostaRicaClock.FromUtc(utc));
    }

    // ---- Marcas de la base (GETDATE / SYSDATETIME) ---------------------------------------------------------

    [Fact]
    public void Database_InAzure_StampsAreUtcAndShowSixHoursLess()
    {
        CostaRicaClock.DatabaseUtcOffset = TimeSpan.Zero; // Azure SQL: GETDATE() = UTC

        Assert.Equal(new DateTime(2026, 11, 21, 23, 9, 0), CostaRicaClock.FromDatabase(new DateTime(2026, 11, 22, 5, 9, 0)));
    }

    [Fact]
    public void Database_InLocalDbWithCostaRicaClock_StampsAreAlreadyLocalAndDoNotMove()
    {
        CostaRicaClock.DatabaseUtcOffset = TimeSpan.FromHours(-6); // LocalDB en Windows con hora de Costa Rica

        Assert.Equal(new DateTime(2026, 11, 21, 23, 9, 0), CostaRicaClock.FromDatabase(new DateTime(2026, 11, 21, 23, 9, 0)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-360)]
    public void Database_DateWithoutTime_IsACalendarDayAndIsNeverConverted(int offsetMinutes)
    {
        CostaRicaClock.DatabaseUtcOffset = TimeSpan.FromMinutes(offsetMinutes);
        var vencimiento = new DateTime(2026, 12, 21); // 00:00:00

        // Convertirlo lo pasaría al 20/12 a las 6 p. m.
        Assert.Equal(vencimiento, CostaRicaClock.FromDatabase(vencimiento));
    }

    [Fact]
    public void ToDatabase_ShiftsADayChosenByTheUserToTheDatabaseClock()
    {
        var dia = new DateTime(2026, 11, 21);

        CostaRicaClock.DatabaseUtcOffset = TimeSpan.Zero; // Azure
        Assert.Equal(new DateTime(2026, 11, 21, 6, 0, 0), CostaRicaClock.ToDatabase(dia));

        CostaRicaClock.DatabaseUtcOffset = TimeSpan.FromHours(-6); // LocalDB en Costa Rica
        Assert.Equal(dia, CostaRicaClock.ToDatabase(dia));
    }

    [Fact]
    public void DateFilter_ADayRange_IncludesTheEveningSaleStoredAsNextDayUtc()
    {
        CostaRicaClock.DatabaseUtcOffset = TimeSpan.Zero; // Azure
        // Venta a las 8:00 p. m. del 21/11 en Costa Rica: la base guarda 02:00 del 22/11.
        var venta = new DateTime(2026, 11, 22, 2, 0, 0);
        var desde = CostaRicaClock.ToDatabase(new DateTime(2026, 11, 21));
        var hastaExclusivo = CostaRicaClock.ToDatabase(new DateTime(2026, 11, 21)).AddDays(1);

        Assert.True(venta >= desde && venta < hastaExclusivo, "El reporte del 21/11 debe incluir la venta de las 8 p. m.");
        Assert.Equal(new DateTime(2026, 11, 21), CostaRicaClock.FromDatabase(venta).Date);
    }

    // ---- Lectores de SQL -----------------------------------------------------------------------------------

    [Fact]
    public void Reader_GetDateTimeCR_ConvertsStampsAndLeavesDatesAlone()
    {
        CostaRicaClock.DatabaseUtcOffset = TimeSpan.Zero;
        var tabla = new DataTable();
        tabla.Columns.Add("FechaRegistro", typeof(DateTime));
        tabla.Columns.Add("Vencimiento", typeof(DateTime));
        tabla.Rows.Add(new DateTime(2026, 11, 22, 5, 9, 0), new DateTime(2026, 12, 21));
        using var reader = tabla.CreateDataReader();
        Assert.True(reader.Read());

        Assert.Equal(new DateTime(2026, 11, 21, 23, 9, 0), reader.GetDateTimeCR(0));
        Assert.Equal(new DateTime(2026, 12, 21), reader.GetDateTimeCR(1));
    }

    [Fact]
    public void Reader_UtcColumns_AreReadAsUtcInstantsOrConvertedWhenThePropertyIsNotNamedUtc()
    {
        var tabla = new DataTable();
        tabla.Columns.Add("FechaCreacionUtc", typeof(DateTime));
        tabla.Columns.Add("FechaSolicitud", typeof(DateTime));
        tabla.Rows.Add(new DateTime(2026, 11, 22, 5, 9, 0), new DateTime(2026, 11, 22, 5, 9, 0));
        using var reader = tabla.CreateDataReader();
        Assert.True(reader.Read());

        var utc = reader.GetUtcDateTime(0);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(new DateTime(2026, 11, 21, 23, 9, 0), utc.ToCostaRica());
        // SYSUTCDATETIME() en una columna sin sufijo Utc (Garantias.FechaSolicitud): siempre UTC, sin importar el reloj de la base.
        CostaRicaClock.DatabaseUtcOffset = TimeSpan.FromHours(-6);
        Assert.Equal(new DateTime(2026, 11, 21, 23, 9, 0), reader.GetDateTimeCRFromUtc(1));
    }

    [Fact]
    public void DocumentExpirationPolicy_BusinessDate_UsesTheSameClock()
    {
        var instante = new DateTimeOffset(2026, 11, 22, 5, 9, 0, TimeSpan.Zero);

        Assert.Equal(new DateTime(2026, 11, 21), DocumentExpirationPolicy.BusinessDate(instante));
        Assert.Equal(new DateTime(2026, 11, 21, 23, 9, 0), DocumentExpirationPolicy.BusinessDateTime(instante));
    }

    // ---- Vigilancia del código -----------------------------------------------------------------------------

    [Fact]
    public void Code_NeverUsesTheServerClockOrZone()
    {
        var prohibidos = new Regex(@"\bDateTime\.(Now|Today)\b|\bDateTimeOffset\.Now\b|\.ToLocalTime\(\)|TimeZoneInfo\.Local\b|\bDateTime\.UtcNow\.(Date|Year|Month|Day)\b", RegexOptions.Compiled);
        var infracciones = ArchivosDeCodigo(".cs", ".cshtml")
            .SelectMany(ruta => File.ReadLines(ruta).Select((linea, i) => (ruta, linea, numero: i + 1)))
            .Where(x => prohibidos.IsMatch(x.linea))
            .Select(x => $"{Relativa(x.ruta)}:{x.numero}")
            .ToList();

        Assert.True(infracciones.Count == 0,
            "Usar CostaRicaClock.Now/Today (no dependen de la zona del servidor): " + string.Join(", ", infracciones));
    }

    [Fact]
    public void Services_ReadDatesOnlyThroughTheClockExtensions()
    {
        var crudo = new Regex(@"\.GetDateTime\(", RegexOptions.Compiled);
        var infracciones = ArchivosDeCodigo(".cs")
            .Where(ruta => !Relativa(ruta).StartsWith("Proyecto_Final.Tests", StringComparison.Ordinal))
            .SelectMany(ruta => File.ReadLines(ruta).Select((linea, i) => (ruta, linea, numero: i + 1)))
            .Where(x => crudo.IsMatch(x.linea))
            .Select(x => $"{Relativa(x.ruta)}:{x.numero}")
            .ToList();

        Assert.True(infracciones.Count == 0,
            "Leer con GetDateTimeCR (marca de la base), GetUtcDateTime (columna ...Utc) o GetDateTimeCRFromUtc: " + string.Join(", ", infracciones));
    }

    [Fact]
    public void Views_DoNotPrintAUtcLabel_BecauseTheyShowCostaRicaTime()
    {
        var etiqueta = new Regex(@"\)\s*UTC\b|HH:mm\s+UTC|\bUTC</small>", RegexOptions.Compiled);
        var infracciones = ArchivosDeCodigo(".cshtml")
            .SelectMany(ruta => File.ReadLines(ruta).Select((linea, i) => (ruta, linea, numero: i + 1)))
            .Where(x => etiqueta.IsMatch(x.linea))
            .Select(x => $"{Relativa(x.ruta)}:{x.numero}")
            .ToList();

        Assert.True(infracciones.Count == 0, "Vistas con la etiqueta UTC: " + string.Join(", ", infracciones));
    }

    [Theory]
    [InlineData("Budgets", "Index.cshtml")]
    [InlineData("Budgets", "Details.cshtml")]
    [InlineData("Expenses", "Details.cshtml")]
    [InlineData("Documents", "Details.cshtml")]
    [InlineData("BudgetComparison", "Print.cshtml")]
    [InlineData("BudgetComparison", "Index.cshtml")]
    [InlineData("DeliveryBoard", "Index.cshtml")]
    [InlineData("InventoryIntelligence", "Index.cshtml")]
    [InlineData("Combos", "Index.cshtml")]
    [InlineData("Combos", "Detail.cshtml")]
    [InlineData("PriceHistory", "Index.cshtml")]
    [InlineData("PurchaseOrders", "Index.cshtml")]
    public void Views_ShowUtcPropertiesWithToCostaRica(string carpeta, string vista)
    {
        var texto = File.ReadAllText(SourcePath("Proyecto_Final", "Views", carpeta, vista));

        Assert.Contains(".ToCostaRica()", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void ChatKeepsUtcInstants_BecauseTheBrowserScriptConvertsThem()
    {
        var chat = File.ReadAllText(SourcePath("Proyecto_Final", "Services", "ChatDbService.cs"));

        // Convertir aquí además de chat.js lo restaría dos veces.
        Assert.DoesNotContain("GetDateTimeCR(", chat, StringComparison.Ordinal);
        Assert.Contains("GetUtcDateTime(", chat, StringComparison.Ordinal);
        Assert.Contains("America/Costa_Rica", File.ReadAllText(SourcePath("Proyecto_Final", "wwwroot", "js", "chat.js")), StringComparison.Ordinal);
    }

    [Fact]
    public void MobileApi_ConvertsTimestampsBeforeSendingThemToTheApp()
    {
        var baseDatos = File.ReadAllText(SourcePath("Proyecto_FinalAPI", "Services", "Mobile", "MobileDbServiceBase.cs"));

        Assert.Contains("CostaRicaClock.FromDatabase", baseDatos, StringComparison.Ordinal);
        Assert.Contains("CostaRicaClock.FromUtc", baseDatos, StringComparison.Ordinal);
        Assert.Contains("CostaRicaClock.FromUtc(_time.GetUtcNow())", File.ReadAllText(SourcePath("Proyecto_FinalAPI", "Controllers", "MobileStaffController.cs")), StringComparison.Ordinal);
        Assert.Contains("CostaRicaClock.ConfigureAsync", File.ReadAllText(SourcePath("Proyecto_FinalAPI", "Program.cs")), StringComparison.Ordinal);
        Assert.Contains("CostaRicaClock.ConfigureAsync", File.ReadAllText(SourcePath("Proyecto_Final", "Program.cs")), StringComparison.Ordinal);
    }

    // ---- Migraciones 0340-0342 -----------------------------------------------------------------------------

    [Theory]
    [InlineData("0340_reloj_costa_rica_funciones")]
    [InlineData("0341_reloj_costa_rica_hoy")]
    [InlineData("0342_reloj_costa_rica_reportes")]
    [InlineData("0343_reloj_costa_rica_objetos_nuevos")]
    public void Migrations_HaveVerifyRollbackAndReadmeRow(string nombre)
    {
        var carpeta = SourcePath("Trilogia-Cursos", "database", "migrations");
        var readme = File.ReadAllText(Path.Combine(carpeta, "README.md"));

        Assert.True(File.Exists(Path.Combine(carpeta, nombre + ".sql")));
        Assert.True(File.Exists(Path.Combine(carpeta, nombre + ".verify.sql")));
        Assert.True(File.Exists(Path.Combine(carpeta, nombre + ".rollback.md")));
        Assert.Contains(nombre + ".sql", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0340_CreatesTheFourClockFunctions_WithoutTouchingData()
    {
        var sql = File.ReadAllText(SourcePath("Trilogia-Cursos", "database", "migrations", "0340_reloj_costa_rica_funciones.sql"));

        foreach (var funcion in new[] { "fn_AhoraCR", "fn_HoyCR", "fn_FechaHoraCR", "fn_DiaCRaReloj" })
            Assert.Contains($"CREATE OR ALTER FUNCTION dbo.{funcion}", sql, StringComparison.Ordinal);
        Assert.Contains("-06:00", sql, StringComparison.Ordinal);
        // Solo agrega funciones: ni UPDATE ni DELETE ni cambios de tablas.
        Assert.DoesNotMatch(new Regex(@"^\s*(UPDATE|DELETE|ALTER TABLE|DROP TABLE)\b", RegexOptions.Multiline | RegexOptions.IgnoreCase), sql);
    }

    [Fact]
    public void Migrations0341And0342_PatchTheCurrentDefinitionInsteadOfRedefiningProceduresWholesale()
    {
        var carpeta = SourcePath("Trilogia-Cursos", "database", "migrations");
        foreach (var nombre in new[] { "0341_reloj_costa_rica_hoy.sql", "0342_reloj_costa_rica_reportes.sql" })
        {
            var sql = File.ReadAllText(Path.Combine(carpeta, nombre));
            Assert.Contains("OBJECT_DEFINITION", sql, StringComparison.Ordinal);
            Assert.Contains("0340_reloj_costa_rica_funciones", sql, StringComparison.Ordinal);
            Assert.DoesNotMatch(new Regex(@"^\s*CREATE (OR ALTER )?PROCEDURE", RegexOptions.Multiline | RegexOptions.IgnoreCase), sql);
        }
    }

    [Fact]
    public void Migration0341_FixesTheTodayOfPromotionsFleetAndInvoiceAlerts()
    {
        var sql = File.ReadAllText(SourcePath("Trilogia-Cursos", "database", "migrations", "0341_reloj_costa_rica_hoy.sql"));

        foreach (var objeto in new[]
                 {
                     "sp_Flota_Alertas", "sp_Promociones_Vigentes", "sp_Promociones_List", "sp_Store_CreateOrderWithPromotions",
                     "sp_FacturasVencidas_GenerarAvisos", "sp_Chofer_ResumenDia", "sp_Mantenimiento_Create", "sp_Comodato_List"
                 })
            Assert.Contains($"N'{objeto}'", sql, StringComparison.Ordinal);
        Assert.Contains("dbo.fn_HoyCR()", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0342_FixesDateFiltersAndMonthlyGoals()
    {
        var sql = File.ReadAllText(SourcePath("Trilogia-Cursos", "database", "migrations", "0342_reloj_costa_rica_reportes.sql"));

        foreach (var objeto in new[]
                 {
                     "sp_Reportes_DashboardKpis", "sp_Reportes_VentasDetallado", "sp_Reportes_DesempenoVendedores",
                     "sp_Admin_GetInventoryMovements", "sp_Admin_SalesSummary", "sp_Metas_List", "sp_Metas_MiProgreso", "sp_Kpi_CumplimientoGlobal"
                 })
            Assert.Contains($"N'{objeto}'", sql, StringComparison.Ordinal);
        Assert.Contains("dbo.fn_DiaCRaReloj(@Desde)", sql, StringComparison.Ordinal);
        Assert.Contains("MONTH(dbo.fn_FechaHoraCR(", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0343_FixesTheTodayOfThePromotionFunctionAndTheNewMailAlerts()
    {
        var sql = File.ReadAllText(SourcePath("Trilogia-Cursos", "database", "migrations", "0343_reloj_costa_rica_objetos_nuevos.sql"));

        foreach (var objeto in new[] { "fn_Promociones_Aplicar", "sp_DocumentAlert_PendingNotifications", "sp_FlotaDocumentos_AvisosPendientes" })
            Assert.Contains($"N'{objeto}'", sql, StringComparison.Ordinal);
        Assert.Contains("0340_reloj_costa_rica_funciones", sql, StringComparison.Ordinal);
        Assert.Contains("OBJECT_DEFINITION", sql, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"^\s*CREATE (OR ALTER )?(PROCEDURE|FUNCTION)", RegexOptions.Multiline | RegexOptions.IgnoreCase), sql);
    }
    // ---- Utilidades ----------------------------------------------------------------------------------------

    private static IEnumerable<string> ArchivosDeCodigo(params string[] extensiones)
    {
        var raiz = SourcePath("Trilogia-Cursos");
        foreach (var carpeta in new[] { "Proyecto_Final", "Proyecto_FinalAPI", "Proyecto_Final.Shared" })
        {
            foreach (var ruta in Directory.EnumerateFiles(Path.Combine(raiz, carpeta), "*", SearchOption.AllDirectories))
            {
                if (!extensiones.Contains(Path.GetExtension(ruta), StringComparer.OrdinalIgnoreCase)) continue;
                var relativa = Path.GetRelativePath(raiz, ruta).Replace('\\', '/');
                if (relativa.Contains("/obj/") || relativa.Contains("/bin/") || relativa.Contains("/wwwroot/lib/")) continue;
                // El reloj mismo es el único lugar que puede hablar de la zona.
                if (relativa.EndsWith("Proyecto_Final.Shared/CostaRicaClock.cs", StringComparison.Ordinal)) continue;
                yield return ruta;
            }
        }
    }

    private static string Relativa(string ruta) => Path.GetRelativePath(SourcePath("Trilogia-Cursos"), ruta).Replace('\\', '/');

    private static string SourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        var raiz = directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
        // La raíz del repositorio de pruebas es Trilogia-Cursos; los demás tests la usan como base.
        if (parts.Length > 0 && parts[0] == "Trilogia-Cursos") return Path.Combine([raiz, .. parts.Skip(1)]);
        return Path.Combine([raiz, .. parts]);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}

[CollectionDefinition("CostaRicaClock", DisableParallelization = true)]
public sealed class CostaRicaClockCollection;
