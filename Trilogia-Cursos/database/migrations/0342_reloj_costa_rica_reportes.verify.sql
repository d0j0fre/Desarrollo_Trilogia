SET NOCOUNT ON;

/* Verificacion de 0342. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0342_reloj_costa_rica_reportes' AND Status = N'Applied')
    THROW 58960, N'0342 no figura aplicada en el ledger.', 1;

DECLARE @Objetos TABLE (Nombre SYSNAME PRIMARY KEY);
INSERT @Objetos VALUES
 (N'sp_Reportes_DashboardKpis'), (N'sp_Reportes_DashboardDetalle'), (N'sp_Reportes_DashboardVentasSerie'),
 (N'sp_Reportes_DesempenoVendedores'), (N'sp_Reportes_VentasDetallado'), (N'sp_Admin_GetInventoryMovements'),
 (N'sp_Movil_Gestion_Operacion'), (N'sp_Admin_DashboardSummary'), (N'sp_Admin_SalesSummary'),
 (N'sp_Admin_GetMonthlySales'), (N'sp_Admin_GetSeasonalSalesTrend'), (N'sp_Kpi_CumplimientoGlobal'),
 (N'sp_Metas_List'), (N'sp_Metas_MiProgreso');

IF EXISTS (SELECT 1 FROM @Objetos o WHERE OBJECT_ID(N'dbo.' + o.Nombre) IS NULL)
    THROW 58961, N'Falta alguno de los objetos corregidos por 0342.', 1;
IF EXISTS (SELECT 1 FROM @Objetos o CROSS APPLY (SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + o.Nombre)) AS Def) d
           WHERE d.Def NOT LIKE N'%dbo.fn_FechaHoraCR%' AND d.Def NOT LIKE N'%dbo.fn_DiaCRaReloj%' AND d.Def NOT LIKE N'%dbo.fn_HoyCR%')
    THROW 58962, N'Un objeto de 0342 no usa las funciones del reloj de Costa Rica.', 1;
IF EXISTS (SELECT 1 FROM @Objetos o CROSS APPLY (SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + o.Nombre)) AS Def) d
           WHERE d.Def LIKE N'%GETDATE()%' OR d.Def LIKE N'%SYSDATETIME()%')
    THROW 58963, N'Queda un GETDATE()/SYSDATETIME() en un objeto de 0342.', 1;
-- Ninguna de las marcas se compara ya contra el dia escrito sin pasar por las funciones.
IF EXISTS (SELECT 1 FROM @Objetos o CROSS APPLY (SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + o.Nombre)) AS Def) d
           WHERE d.Def LIKE N'%FechaFactura >= @Desde%' OR d.Def LIKE N'%FechaFactura>=@Desde%'
              OR d.Def LIKE N'%YEAR(f.FechaFactura)%' OR d.Def LIKE N'%CAST(f.FechaFactura AS DATE)%')
    THROW 58964, N'Queda una comparacion de FechaFactura sin la hora de Costa Rica.', 1;
PRINT N'Verificacion 0342 completada.';
