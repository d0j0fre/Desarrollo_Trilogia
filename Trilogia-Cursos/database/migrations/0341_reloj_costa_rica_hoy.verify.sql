SET NOCOUNT ON;

/* Verificacion de 0341. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0341_reloj_costa_rica_hoy' AND Status = N'Applied')
    THROW 58950, N'0341 no figura aplicada en el ledger.', 1;

DECLARE @Objetos TABLE (Nombre SYSNAME PRIMARY KEY);
INSERT @Objetos VALUES
 (N'sp_Asistente_Metricas'), (N'sp_Chofer_ResumenDia'), (N'sp_FacturasVencidas_GenerarAvisos'), (N'sp_Flota_Alertas'),
 (N'sp_Mantenimiento_Create'), (N'sp_Mantenimiento_MarcarCompletada'), (N'sp_VehiculoDocumento_List'),
 (N'sp_Promociones_List'), (N'sp_Promociones_Vigentes'), (N'sp_Store_CreateOrderWithPromotions'),
 (N'sp_Comodato_List'), (N'fn_Comodato_Rentabilidad'), (N'sp_Movil_Bodega_PedidosPorPreparar'),
 (N'sp_Admin_UpdateEmployeeLeaveRequestStatus'), (N'sp_Admin_GetPurchaseSuggestions'), (N'sp_Admin_GetSlowMovingProducts');

IF EXISTS (SELECT 1 FROM @Objetos o WHERE OBJECT_ID(N'dbo.' + o.Nombre) IS NULL)
    THROW 58951, N'Falta alguno de los objetos corregidos por 0341.', 1;
-- Todos usan ya el dia de Costa Rica.
IF EXISTS (SELECT 1 FROM @Objetos o WHERE CHARINDEX(N'dbo.fn_HoyCR()', OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + o.Nombre))) = 0)
    THROW 58952, N'Un objeto de 0341 no usa dbo.fn_HoyCR().', 1;
-- Y ninguno cuenta «hoy» con el reloj del servidor SQL.
IF EXISTS (SELECT 1 FROM @Objetos o CROSS APPLY (SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + o.Nombre)) AS Def) d
           WHERE d.Def LIKE N'%CAST(SYSDATETIME() AS DATE)%' OR d.Def LIKE N'%CONVERT(DATE, SYSDATETIME())%'
              OR d.Def LIKE N'%CONVERT(DATE, GETDATE())%' OR d.Def LIKE N'%CAST(GETDATE() AS DATE)%')
    THROW 58953, N'Queda algun «hoy» calculado con el reloj del servidor SQL.', 1;
PRINT N'Verificacion 0341 completada.';
