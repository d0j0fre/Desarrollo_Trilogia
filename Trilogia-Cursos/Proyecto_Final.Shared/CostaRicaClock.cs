using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace Proyecto_Final.Services;

/// <summary>
/// Reloj único del sistema: todo lo que ve una persona (pantallas, API hacia la app, correos,
/// archivos exportados) usa la hora de Costa Rica (America/Costa_Rica, UTC-6, sin horario de
/// verano) sin importar dónde corra el servidor (Windows en hora local o Linux/Azure en UTC).
///
/// Convención:
///  - Instante guardado = UTC. Azure SQL siempre trabaja en UTC: SYSDATETIME() y GETDATE() devuelven
///    UTC y SYSUTCDATETIME() también.
///  - En C# una propiedad cuyo nombre termina en "Utc" es un instante UTC y se muestra con
///    <see cref="ToCostaRica(DateTime)"/>. Cualquier otra propiedad de fecha y hora es la hora de
///    pared de Costa Rica: la capa de datos ya la convirtió al leerla
///    (<see cref="DbDataReaderClockExtensions.GetDateTimeCR(DbDataReader, int)"/>).
///  - "Hoy" y "ahora" salen de <see cref="Today"/> y <see cref="Now"/>, nunca de DateTime.Now
///    ni DateTime.Today (dependen de la zona del servidor).
///  - Una fecha sin hora (00:00:00: vencimientos, día de factura, columnas DATE) es un día de
///    calendario y no se convierte: moverla seis horas la pasaría al día anterior.
/// </summary>
public static class CostaRicaClock
{
    public const string ZoneId = "America/Costa_Rica";
    public const string WindowsZoneId = "Central America Standard Time";

    private static readonly TimeZoneInfo Zone = FindZone();
    private static TimeProvider _provider = TimeProvider.System;
    private static TimeSpan _databaseUtcOffset = TimeSpan.Zero;

    /// <summary>Reloj del sistema; las pruebas lo reemplazan para simular otra hora o un servidor en UTC.</summary>
    public static TimeProvider Provider
    {
        get => _provider;
        set => _provider = value ?? TimeProvider.System;
    }

    /// <summary>
    /// Diferencia con UTC del reloj local del servidor SQL (lo que usan GETDATE() y SYSDATETIME()).
    /// Azure SQL: cero. Un SQL Server o LocalDB en Windows con hora de Costa Rica: -6 horas.
    /// Se detecta al arrancar con <see cref="DetectDatabaseOffsetAsync"/>.
    /// </summary>
    public static TimeSpan DatabaseUtcOffset
    {
        get => _databaseUtcOffset;
        set => _databaseUtcOffset = value;
    }

    /// <summary>Instante actual en UTC.</summary>
    public static DateTime UtcNow => _provider.GetUtcNow().UtcDateTime;

    /// <summary>Fecha y hora actuales de Costa Rica (Kind sin especificar).</summary>
    public static DateTime Now => FromUtc(UtcNow);

    /// <summary>Día de hoy en Costa Rica (a las 00:00).</summary>
    public static DateTime Today => Now.Date;

    public static TimeSpan Offset => Zone.BaseUtcOffset;

    /// <summary>Hora de Costa Rica de un instante UTC (se asume UTC aunque el Kind no esté marcado).</summary>
    public static DateTime FromUtc(DateTime utc)
    {
        if (utc == DateTime.MinValue || utc == DateTime.MaxValue) return utc;
        var instant = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(instant, Zone), DateTimeKind.Unspecified);
    }

    public static DateTime? FromUtc(DateTime? utc) => utc.HasValue ? FromUtc(utc.Value) : null;

    public static DateTime FromUtc(DateTimeOffset instant) => FromUtc(instant.UtcDateTime);

    /// <summary>
    /// Hora de Costa Rica de una marca puesta por la base con GETDATE()/SYSDATETIME() (reloj local del
    /// servidor SQL). Una fecha sin hora se devuelve igual.
    /// </summary>
    public static DateTime FromDatabase(DateTime stamp)
    {
        if (stamp == DateTime.MinValue || stamp == DateTime.MaxValue) return stamp;
        if (stamp.TimeOfDay == TimeSpan.Zero) return stamp;
        return FromUtc(stamp - _databaseUtcOffset);
    }

    public static DateTime? FromDatabase(DateTime? stamp) => stamp.HasValue ? FromDatabase(stamp.Value) : null;

    /// <summary>Instante UTC de una hora de pared de Costa Rica.</summary>
    public static DateTime ToUtc(DateTime costaRica)
    {
        var wall = DateTime.SpecifyKind(costaRica, DateTimeKind.Unspecified);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(wall, Zone), DateTimeKind.Utc);
    }

    /// <summary>Hora de pared de Costa Rica expresada como valor de la base (reloj local del servidor SQL).</summary>
    public static DateTime ToDatabase(DateTime costaRica) => ToUtc(costaRica) + _databaseUtcOffset;

    /// <summary>
    /// Convierte a hora de Costa Rica un valor ya leído que representa un instante UTC (propiedades "...Utc").
    /// Los valores con Kind Local se tratan como instantes reales; sin Kind o Utc, como UTC.
    /// </summary>
    public static DateTime ToCostaRica(this DateTime value) =>
        value.Kind == DateTimeKind.Local ? FromUtc(value.ToUniversalTime()) : FromUtc(value);

    public static DateTime? ToCostaRica(this DateTime? value) => value.HasValue ? value.Value.ToCostaRica() : null;

    public static DateTime ToCostaRica(this DateTimeOffset value) => FromUtc(value.UtcDateTime);

    /// <summary>
    /// Lee en el servidor SQL la diferencia con UTC de su reloj local y la deja en
    /// <see cref="DatabaseUtcOffset"/>. Si no puede consultar se queda el valor anterior (UTC).
    /// </summary>
    public static async Task DetectDatabaseOffsetAsync(string? connectionString, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(timeout.Token);
            await using var command = new SqlCommand("SELECT DATEPART(TZOFFSET, SYSDATETIMEOFFSET())", connection);
            var minutes = Convert.ToInt32(await command.ExecuteScalarAsync(timeout.Token));
            _databaseUtcOffset = TimeSpan.FromMinutes(minutes);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Sin conexión no hay nada que mostrar de todos modos; se asume Azure SQL (UTC).
        }
    }

    /// <summary>
    /// Arranque del sitio y de la API: usa Time:DatabaseUtcOffsetMinutes si está configurado
    /// (para forzarlo) y, si no, lo consulta al servidor SQL.
    /// </summary>
    public static async Task ConfigureAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var configured = configuration["Time:DatabaseUtcOffsetMinutes"];
        if (int.TryParse(configured, out var minutes))
        {
            _databaseUtcOffset = TimeSpan.FromMinutes(minutes);
            return;
        }
        await DetectDatabaseOffsetAsync(configuration.GetConnectionString("DefaultConnection"), cancellationToken);
    }

    private static TimeZoneInfo FindZone()
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(ZoneId, out var zone)) return zone;
        if (TimeZoneInfo.TryFindSystemTimeZoneById(WindowsZoneId, out zone)) return zone;
        return TimeZoneInfo.CreateCustomTimeZone(ZoneId, TimeSpan.FromHours(-6), "Costa Rica", "Costa Rica");
    }
}

/// <summary>Lectura de fechas desde SQL ya en la convención de <see cref="CostaRicaClock"/>.</summary>
public static class DbDataReaderClockExtensions
{
    /// <summary>Marca puesta por la base con GETDATE()/SYSDATETIME(): se devuelve en hora de Costa Rica.</summary>
    public static DateTime GetDateTimeCR(this DbDataReader reader, int ordinal) =>
        CostaRicaClock.FromDatabase(reader.GetDateTime(ordinal));

    /// <summary>
    /// Marca puesta con SYSUTCDATETIME() en una columna que no se llama "...Utc" (Garantias.FechaSolicitud,
    /// AppMovilVersiones.FechaPublicacion): se devuelve ya en hora de Costa Rica.
    /// </summary>
    public static DateTime GetDateTimeCRFromUtc(this DbDataReader reader, int ordinal) =>
        CostaRicaClock.FromUtc(reader.GetDateTime(ordinal));

    /// <summary>Marca puesta con SYSUTCDATETIME() (columnas "...Utc"): se devuelve como instante UTC (Kind=Utc).</summary>
    public static DateTime GetUtcDateTime(this DbDataReader reader, int ordinal) =>
        DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);
}
