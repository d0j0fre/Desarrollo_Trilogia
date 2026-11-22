SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* POST-qa3-06: las horas del sitio salian 6 horas adelante de Costa Rica. Azure SQL siempre trabaja en UTC:
   GETDATE() y SYSDATETIME() devuelven UTC y no se puede cambiar la zona del servidor.

   Convencion (ver CostaRicaClock en Proyecto_Final.Shared): lo que la base guarda con GETDATE()/SYSDATETIME()
   es la hora del reloj del servidor SQL (UTC en Azure, hora local en un SQL Server o LocalDB de Windows) y
   lo que guarda con SYSUTCDATETIME() es UTC. El sitio y la API convierten a hora de Costa Rica al leer.

   Esta migracion solo agrega cuatro funciones escalares para que los procedimientos puedan calcular «hoy»,
   «ahora» y los limites de un dia de Costa Rica sin depender de la zona del servidor SQL. Funcionan igual en
   Azure (UTC) y en LocalDB (hora de Costa Rica):

     - dbo.fn_AhoraCR()                fecha y hora actuales de Costa Rica (UTC-6, sin horario de verano).
     - dbo.fn_HoyCR()                  el dia de hoy en Costa Rica.
     - dbo.fn_FechaHoraCR(@Marca)      hora de Costa Rica de una marca puesta con GETDATE()/SYSDATETIME().
                                       Una fecha sin hora (00:00:00) se devuelve igual.
     - dbo.fn_DiaCRaReloj(@Dia)        lo inverso: el reloj del servidor SQL en la medianoche de ese dia de
                                       Costa Rica; sirve para comparar rangos de fechas contra columnas
                                       sin envolver la columna en una funcion.

   No cambia tablas ni datos guardados. Rollback en 0340_reloj_costa_rica_funciones.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58910,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0340_reloj_costa_rica_funciones' AND Status=N'Applied')
    THROW 58911,N'0340 ya figura aplicada.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER FUNCTION dbo.fn_AhoraCR()
RETURNS DATETIME2(7)
AS
BEGIN
    RETURN CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), N'-06:00') AS DATETIME2(7));
END;
GO

CREATE OR ALTER FUNCTION dbo.fn_HoyCR()
RETURNS DATE
AS
BEGIN
    RETURN CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), N'-06:00') AS DATE);
END;
GO

CREATE OR ALTER FUNCTION dbo.fn_FechaHoraCR(@Marca DATETIME2(7))
RETURNS DATETIME2(7)
AS
BEGIN
    RETURN CASE
        WHEN @Marca IS NULL THEN NULL
        -- Una fecha sin hora es un dia de calendario: moverla seis horas la pasaria al dia anterior.
        WHEN @Marca = CAST(CAST(@Marca AS DATE) AS DATETIME2(7)) THEN @Marca
        ELSE DATEADD(MINUTE, -360 - DATEPART(TZOFFSET, SYSDATETIMEOFFSET()), @Marca)
    END;
END;
GO

CREATE OR ALTER FUNCTION dbo.fn_DiaCRaReloj(@Dia DATETIME2(7))
RETURNS DATETIME2(7)
AS
BEGIN
    RETURN DATEADD(MINUTE, 360 + DATEPART(TZOFFSET, SYSDATETIMEOFFSET()), @Dia);
END;
GO

IF XACT_STATE()<>1 THROW 58912,N'La transacción 0340 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 58913,N'SHA-256 inválido para 0340.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0340_reloj_costa_rica_funciones',N'0340_reloj_costa_rica_funciones.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'POST-qa3-06: fn_AhoraCR, fn_HoyCR, fn_FechaHoraCR y fn_DiaCRaReloj (hora de Costa Rica sin depender de la zona del servidor SQL). Aditiva.');
COMMIT TRANSACTION;
GO
