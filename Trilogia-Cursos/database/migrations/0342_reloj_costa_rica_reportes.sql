SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* POST-qa3-06: los reportes agrupan y filtran por dia o por mes con las marcas que guarda la base
   (FechaFactura, FechaPedido, FechaMovimiento, FechaEntrega: reloj del servidor SQL, UTC en Azure) pero el
   usuario escribe dias de Costa Rica. Una venta hecha a las 8 p. m. del 30 de noviembre en Costa Rica ya es
   1 de diciembre a las 2 a. m. UTC: caia en otro dia y, a fin de mes, en otro mes (ventas del mes, metas,
   cumplimiento de metas, tablero gerencial, reporte de ventas, tendencia estacional, movimientos de inventario).

   Cada objeto se corrige con un reemplazo de texto sobre su definicion VIGENTE (OBJECT_DEFINITION), igual que
   0341:

     - «hoy» por defecto de los tableros:         CAST(GETDATE() AS DATE)  ->  dbo.fn_HoyCR()
     - rango de dias escrito por el usuario:      col >= @Desde AND col < DATEADD(DAY,1,@Hasta)
                                                  ->  col >= dbo.fn_DiaCRaReloj(@Desde) AND col < dbo.fn_DiaCRaReloj(DATEADD(DAY,1,@Hasta))
                                                  (la columna no se envuelve: sigue pudiendo usar su indice)
     - dia o mes de una marca:                    CAST(col AS DATE), YEAR(col), MONTH(col), FORMAT(col, ...)
                                                  ->  la misma expresion sobre dbo.fn_FechaHoraCR(col)

   Los valores que devuelven los procedimientos no cambian de forma; el sitio convierte las marcas a hora de
   Costa Rica al leerlas. Requiere 0340. No cambia tablas ni datos guardados. Rollback en
   0342_reloj_costa_rica_reportes.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58930,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0342_reloj_costa_rica_reportes' AND Status=N'Applied')
    THROW 58931,N'0342 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0340_reloj_costa_rica_funciones' AND Status=N'Applied')
    THROW 58932,N'Falta 0340 (funciones del reloj de Costa Rica).',1;

BEGIN TRANSACTION;
GO

CREATE TABLE #Parches(Orden INT IDENTITY(1,1) PRIMARY KEY, Objeto SYSNAME NOT NULL, Viejo NVARCHAR(MAX) NOT NULL, Nuevo NVARCHAR(MAX) NOT NULL);
INSERT #Parches(Objeto,Viejo,Nuevo) VALUES
 -- Tablero gerencial.
 (N'sp_Reportes_DashboardKpis',N'CAST(GETDATE() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'sp_Reportes_DashboardKpis',N'f.FechaFactura >= @Desde AND f.FechaFactura < DATEADD(DAY, 1, @Hasta)',N'f.FechaFactura >= dbo.fn_DiaCRaReloj(@Desde) AND f.FechaFactura < dbo.fn_DiaCRaReloj(DATEADD(DAY, 1, @Hasta))'),
 (N'sp_Reportes_DashboardKpis',N'CAST(p.FechaPedido AS DATE) BETWEEN @Desde AND @Hasta',N'CAST(dbo.fn_FechaHoraCR(p.FechaPedido) AS DATE) BETWEEN @Desde AND @Hasta'),
 (N'sp_Reportes_DashboardDetalle',N'CAST(GETDATE() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'sp_Reportes_DashboardDetalle',N'f.FechaFactura >= @Desde AND f.FechaFactura < DATEADD(DAY, 1, @Hasta)',N'f.FechaFactura >= dbo.fn_DiaCRaReloj(@Desde) AND f.FechaFactura < dbo.fn_DiaCRaReloj(DATEADD(DAY, 1, @Hasta))'),
 (N'sp_Reportes_DashboardDetalle',N'cm.FechaMovimiento >= @Desde AND cm.FechaMovimiento < DATEADD(DAY, 1, @Hasta)',N'cm.FechaMovimiento >= dbo.fn_DiaCRaReloj(@Desde) AND cm.FechaMovimiento < dbo.fn_DiaCRaReloj(DATEADD(DAY, 1, @Hasta))'),
 (N'sp_Reportes_DashboardVentasSerie',N'CAST(GETDATE() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'sp_Reportes_DashboardVentasSerie',N'CAST(f.FechaFactura AS DATE)',N'CAST(dbo.fn_FechaHoraCR(f.FechaFactura) AS DATE)'),
 (N'sp_Reportes_DashboardVentasSerie',N'f.FechaFactura >= @Desde AND f.FechaFactura < DATEADD(DAY, 1, @Hasta)',N'f.FechaFactura >= dbo.fn_DiaCRaReloj(@Desde) AND f.FechaFactura < dbo.fn_DiaCRaReloj(DATEADD(DAY, 1, @Hasta))'),
 -- Reporte de ventas y desempeno de vendedores.
 (N'sp_Reportes_DesempenoVendedores',N'invoice.FechaFactura>=@Desde AND invoice.FechaFactura<DATEADD(DAY,1,@Hasta)',N'invoice.FechaFactura>=dbo.fn_DiaCRaReloj(@Desde) AND invoice.FechaFactura<dbo.fn_DiaCRaReloj(DATEADD(DAY,1,@Hasta))'),
 (N'sp_Reportes_VentasDetallado',N'invoice.FechaFactura>=@Desde AND invoice.FechaFactura<DATEADD(DAY,1,@Hasta)',N'invoice.FechaFactura>=dbo.fn_DiaCRaReloj(@Desde) AND invoice.FechaFactura<dbo.fn_DiaCRaReloj(DATEADD(DAY,1,@Hasta))'),
 (N'sp_Reportes_VentasDetallado',N'CONVERT(DATE,invoice.FechaFactura)',N'CONVERT(DATE,dbo.fn_FechaHoraCR(invoice.FechaFactura))'),
 -- Movimientos de inventario.
 (N'sp_Admin_GetInventoryMovements',N'FechaMovimiento >= @Desde',N'FechaMovimiento >= dbo.fn_DiaCRaReloj(@Desde)'),
 (N'sp_Admin_GetInventoryMovements',N'FechaMovimiento < DATEADD(DAY, 1, CAST(@Hasta AS DATETIME2))',N'FechaMovimiento < dbo.fn_DiaCRaReloj(DATEADD(DAY, 1, CAST(@Hasta AS DATETIME2)))'),
 -- Gestion movil (app).
 (N'sp_Movil_Gestion_Operacion',N'CAST(GETDATE() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'sp_Movil_Gestion_Operacion',N'CAST(FechaEntrega AS DATE)',N'CAST(dbo.fn_FechaHoraCR(FechaEntrega) AS DATE)'),
 (N'sp_Movil_Gestion_Operacion',N'CAST(f.FechaFactura AS DATE)',N'CAST(dbo.fn_FechaHoraCR(f.FechaFactura) AS DATE)'),
 -- Ventas del mes en el panel de administracion.
 (N'sp_Admin_DashboardSummary',N'YEAR(FechaFactura) = YEAR(GETDATE())',N'YEAR(dbo.fn_FechaHoraCR(FechaFactura)) = YEAR(dbo.fn_AhoraCR())'),
 (N'sp_Admin_DashboardSummary',N'MONTH(FechaFactura) = MONTH(GETDATE())',N'MONTH(dbo.fn_FechaHoraCR(FechaFactura)) = MONTH(dbo.fn_AhoraCR())'),
 (N'sp_Admin_SalesSummary',N'YEAR(FechaFactura) = YEAR(GETDATE())',N'YEAR(dbo.fn_FechaHoraCR(FechaFactura)) = YEAR(dbo.fn_AhoraCR())'),
 (N'sp_Admin_SalesSummary',N'MONTH(FechaFactura) = MONTH(GETDATE())',N'MONTH(dbo.fn_FechaHoraCR(FechaFactura)) = MONTH(dbo.fn_AhoraCR())'),
 (N'sp_Admin_GetMonthlySales',N'FORMAT(FechaFactura, ''yyyy-MM'')',N'FORMAT(dbo.fn_FechaHoraCR(FechaFactura), ''yyyy-MM'')'),
 -- Tendencia estacional (por mes de Costa Rica).
 (N'sp_Admin_GetSeasonalSalesTrend',N'MONTH(invoice.FechaFactura)',N'MONTH(dbo.fn_FechaHoraCR(invoice.FechaFactura))'),
 (N'sp_Admin_GetSeasonalSalesTrend',N'invoice.FechaFactura >= @Desde AND invoice.FechaFactura < @Hasta',N'invoice.FechaFactura >= dbo.fn_DiaCRaReloj(@Desde) AND invoice.FechaFactura < dbo.fn_DiaCRaReloj(@Hasta)'),
 -- Metas del mes y cumplimiento (el mes de una factura es el de Costa Rica).
 (N'sp_Kpi_CumplimientoGlobal',N'YEAR(f.FechaFactura) = @Anio AND MONTH(f.FechaFactura) = @Mes',N'YEAR(dbo.fn_FechaHoraCR(f.FechaFactura)) = @Anio AND MONTH(dbo.fn_FechaHoraCR(f.FechaFactura)) = @Mes'),
 (N'sp_Metas_List',N'YEAR(f.FechaFactura) = @Anio AND MONTH(f.FechaFactura) = @Mes',N'YEAR(dbo.fn_FechaHoraCR(f.FechaFactura)) = @Anio AND MONTH(dbo.fn_FechaHoraCR(f.FechaFactura)) = @Mes'),
 (N'sp_Metas_MiProgreso',N'YEAR(f.FechaFactura)=@Anio AND MONTH(f.FechaFactura)=@Mes',N'YEAR(dbo.fn_FechaHoraCR(f.FechaFactura))=@Anio AND MONTH(dbo.fn_FechaHoraCR(f.FechaFactura))=@Mes');
GO

DECLARE @Obj SYSNAME, @Def NVARCHAR(MAX), @Viejo NVARCHAR(MAX), @Nuevo NVARCHAR(MAX), @Pos INT, @Msg NVARCHAR(400);
DECLARE objetos CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT Objeto FROM #Parches;
OPEN objetos;
FETCH NEXT FROM objetos INTO @Obj;
WHILE @@FETCH_STATUS=0
BEGIN
    SET @Def=OBJECT_DEFINITION(OBJECT_ID(N'dbo.'+@Obj));
    IF @Def IS NULL BEGIN SET @Msg=CONCAT(N'Falta dbo.',@Obj,N'.'); THROW 58933,@Msg,1; END;
    DECLARE parches CURSOR LOCAL FAST_FORWARD FOR SELECT Viejo,Nuevo FROM #Parches WHERE Objeto=@Obj ORDER BY Orden;
    OPEN parches;
    FETCH NEXT FROM parches INTO @Viejo,@Nuevo;
    WHILE @@FETCH_STATUS=0
    BEGIN
        IF CHARINDEX(@Viejo,@Def)>0 SET @Def=REPLACE(@Def,@Viejo,@Nuevo);
        ELSE IF CHARINDEX(@Nuevo,@Def)=0
        BEGIN SET @Msg=CONCAT(N'dbo.',@Obj,N': no se encontro el texto a corregir: ',LEFT(@Viejo,200)); THROW 58934,@Msg,1; END;
        FETCH NEXT FROM parches INTO @Viejo,@Nuevo;
    END;
    CLOSE parches; DEALLOCATE parches;
    -- OBJECT_DEFINITION devuelve «CREATE   PROCEDURE ...» aunque se haya creado con CREATE OR ALTER.
    SET @Pos=PATINDEX(N'%CREATE%',@Def);
    IF @Pos=0 BEGIN SET @Msg=CONCAT(N'dbo.',@Obj,N': la definicion no empieza con CREATE.'); THROW 58935,@Msg,1; END;
    SET @Def=STUFF(@Def,@Pos,6,N'ALTER');
    EXEC sys.sp_executesql @Def;
    FETCH NEXT FROM objetos INTO @Obj;
END;
CLOSE objetos; DEALLOCATE objetos;
DROP TABLE #Parches;
GO

IF XACT_STATE()<>1 THROW 58936,N'La transacción 0342 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 58937,N'SHA-256 inválido para 0342.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0342_reloj_costa_rica_reportes',N'0342_reloj_costa_rica_reportes.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'POST-qa3-06: reportes, tableros, metas y movimientos de inventario filtran y agrupan por dia/mes de Costa Rica (fn_DiaCRaReloj, fn_FechaHoraCR). Reemplazo de texto sobre la definicion vigente.');
COMMIT TRANSACTION;
GO
