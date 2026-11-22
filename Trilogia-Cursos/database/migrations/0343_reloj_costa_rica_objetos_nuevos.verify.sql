SET NOCOUNT ON;

/* Verificacion de 0343. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0343_reloj_costa_rica_objetos_nuevos' AND Status = N'Applied')
    THROW 58980, N'0343 no figura aplicada en el ledger.', 1;

DECLARE @Objetos TABLE (Nombre SYSNAME PRIMARY KEY);
INSERT @Objetos VALUES (N'fn_Promociones_Aplicar'), (N'sp_DocumentAlert_PendingNotifications'), (N'sp_FlotaDocumentos_AvisosPendientes');

IF EXISTS (SELECT 1 FROM @Objetos o WHERE OBJECT_ID(N'dbo.' + o.Nombre) IS NULL)
    THROW 58981, N'Falta alguno de los objetos corregidos por 0343.', 1;
IF EXISTS (SELECT 1 FROM @Objetos o WHERE CHARINDEX(N'dbo.fn_HoyCR()', OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + o.Nombre))) = 0)
    THROW 58982, N'Un objeto de 0343 no usa dbo.fn_HoyCR().', 1;
IF EXISTS (SELECT 1 FROM @Objetos o CROSS APPLY (SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + o.Nombre)) AS Def) d
           WHERE d.Def LIKE N'%CAST(SYSDATETIME() AS DATE)%' OR d.Def LIKE N'%CONVERT(DATE, SYSDATETIME())%')
    THROW 58983, N'Queda un «hoy» calculado con el reloj del servidor SQL.', 1;
-- La promocion y el checkout comparten el mismo dia de Costa Rica.
IF CHARINDEX(N'dbo.fn_HoyCR()', OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Store_CreateOrderWithPromotions'))) = 0
    THROW 58984, N'sp_Store_CreateOrderWithPromotions no usa dbo.fn_HoyCR() (0341 se perdio al redefinirlo).', 1;
PRINT N'Verificacion 0343 completada.';
