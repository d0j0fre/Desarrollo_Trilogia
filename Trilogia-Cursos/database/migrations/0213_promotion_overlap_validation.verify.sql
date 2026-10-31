SET NOCOUNT ON;

/*  Verificación de 0213. Sólo lectura (la prueba de traslape se hace dentro
    de una transacción que siempre se revierte). */

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
               WHERE MigrationId = N'0213_promotion_overlap_validation'
                 AND Status = N'Applied'
                 AND LEN(FileSha256) = 64)
    THROW 55410, N'0213 no figura aplicada correctamente.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.sp_Promociones_Create') AND definition LIKE N'%53102%')
    THROW 55411, N'sp_Promociones_Create no valida reglas traslapadas.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.sp_Promociones_Update') AND definition LIKE N'%53102%')
    THROW 55412, N'sp_Promociones_Update no valida reglas traslapadas.', 1;

/* Prueba funcional revertida: dos promociones cruzadas sobre el mismo producto. */
DECLARE @ProductoId INT = (SELECT TOP 1 ProductoId FROM dbo.Productos WHERE Activo = 1 ORDER BY ProductoId);
DECLARE @Rechazada BIT = 0;
IF @ProductoId IS NOT NULL
BEGIN
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @Ids TABLE (PromocionId INT);
        INSERT @Ids EXEC dbo.sp_Promociones_Create @Nombre = N'0213 verify A', @Tipo = N'DescuentoPorcentual',
            @ProductoId = @ProductoId, @PorcentajeDescuento = 10, @FechaInicio = '2099-01-01', @FechaFin = '2099-01-20';
        INSERT @Ids EXEC dbo.sp_Promociones_Create @Nombre = N'0213 verify B', @Tipo = N'DescuentoPorcentual',
            @ProductoId = @ProductoId, @PorcentajeDescuento = 15, @FechaInicio = '2099-01-10', @FechaFin = '2099-01-15';
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() = 53102 SET @Rechazada = 1;
    END CATCH;
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    IF @Rechazada = 0
        THROW 55413, N'Una promoción traslapada no fue rechazada.', 1;
END;

SELECT ledger.MigrationId, ledger.Status, ledger.AppliedAtUtc, @Rechazada AS TraslapeRechazado
FROM dbo.SchemaMigrationHistory ledger
WHERE ledger.MigrationId = N'0213_promotion_overlap_validation';
