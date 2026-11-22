SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* POST-qa3-06: «hoy» de los procedimientos que usaban el reloj del servidor SQL. En Azure ese reloj va en UTC,
   asi que a partir de las 6 p. m. de Costa Rica «hoy» ya era «manana»: las alertas de vencimiento de la flota,
   la vigencia de las promociones, el resumen del dia del chofer, el limite de fechas futuras al registrar un
   mantenimiento, los avisos de facturas vencidas (uno por dia) y los dias de comodato contaban un dia de mas.

   Cada objeto se corrige con un reemplazo de texto sobre su definicion VIGENTE (OBJECT_DEFINITION), no con una
   copia completa: asi no pisa cambios que otra migracion haya hecho al mismo procedimiento. Si un texto a
   corregir no se encuentra (y el corregido tampoco esta) la migracion falla sin cambiar nada.

     CAST(SYSDATETIME() AS DATE) / CONVERT(DATE, SYSDATETIME()) / CONVERT(DATE, GETDATE())  ->  dbo.fn_HoyCR()
     CAST(<marca> AS DATE) = @Hoy                                                           ->  CAST(dbo.fn_FechaHoraCR(<marca>) AS DATE) = @Hoy

   Requiere 0340 (funciones). No cambia tablas ni datos guardados. Rollback en
   0341_reloj_costa_rica_hoy.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58920,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0341_reloj_costa_rica_hoy' AND Status=N'Applied')
    THROW 58921,N'0341 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0340_reloj_costa_rica_funciones' AND Status=N'Applied')
    THROW 58922,N'Falta 0340 (funciones del reloj de Costa Rica).',1;

BEGIN TRANSACTION;
GO

CREATE TABLE #Parches(Orden INT IDENTITY(1,1) PRIMARY KEY, Objeto SYSNAME NOT NULL, Viejo NVARCHAR(MAX) NOT NULL, Nuevo NVARCHAR(MAX) NOT NULL);
INSERT #Parches(Objeto,Viejo,Nuevo) VALUES
 -- Asistente: metricas del dia, del mes y del anio por dia de Costa Rica.
 (N'sp_Asistente_Metricas',N'CAST(SYSDATETIME() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'sp_Asistente_Metricas',N'CAST(FechaFactura AS DATE)',N'CAST(dbo.fn_FechaHoraCR(FechaFactura) AS DATE)'),
 (N'sp_Asistente_Metricas',N'CAST(FechaPedido AS DATE)',N'CAST(dbo.fn_FechaHoraCR(FechaPedido) AS DATE)'),
 -- App del chofer: resumen del dia.
 (N'sp_Chofer_ResumenDia',N'CAST(SYSDATETIME() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'sp_Chofer_ResumenDia',N'CAST(rp.FechaEntrega AS DATE)',N'CAST(dbo.fn_FechaHoraCR(rp.FechaEntrega) AS DATE)'),
 -- Facturas vencidas: un aviso por cliente y dia.
 (N'sp_FacturasVencidas_GenerarAvisos',N'CAST(SYSDATETIME() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'sp_FacturasVencidas_GenerarAvisos',N'CAST(n.FechaCreacion AS DATE)',N'CAST(dbo.fn_FechaHoraCR(n.FechaCreacion) AS DATE)'),
 -- Flota y mantenimiento: vencimientos y fecha futura.
 (N'sp_Flota_Alertas',N'CAST(SYSDATETIME() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'sp_Mantenimiento_Create',N'CAST(SYSDATETIME() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'sp_Mantenimiento_MarcarCompletada',N'CAST(SYSDATETIME() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'sp_VehiculoDocumento_List',N'CAST(SYSDATETIME() AS DATE)',N'dbo.fn_HoyCR()'),
 -- Promociones: vigencia por dia de Costa Rica (listado, vigentes y la que aplica al crear el pedido).
 (N'sp_Promociones_List',N'CAST(SYSDATETIME() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'sp_Promociones_Vigentes',N'CAST(SYSDATETIME() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'sp_Store_CreateOrderWithPromotions',N'CONVERT(DATE, SYSDATETIME())',N'dbo.fn_HoyCR()'),
 -- Comodatos: dias en comodato y dias sin compra.
 (N'sp_Comodato_List',N'CAST(SYSDATETIME() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'fn_Comodato_Rentabilidad',N'CAST(SYSDATETIME() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'fn_Comodato_Rentabilidad',N'CAST(ISNULL(compras.UltimaCompra, c.FechaAsignacion) AS DATE)',N'CAST(ISNULL(dbo.fn_FechaHoraCR(compras.UltimaCompra), c.FechaAsignacion) AS DATE)'),
 (N'fn_Comodato_Rentabilidad',N'p.FechaPedido >= c.FechaAsignacion',N'p.FechaPedido >= dbo.fn_DiaCRaReloj(c.FechaAsignacion)'),
 -- Bodega movil: preparados de hoy.
 (N'sp_Movil_Bodega_PedidosPorPreparar',N'CAST(pp.FechaPreparacion AS DATE) = CAST(SYSDATETIME() AS DATE)',N'CAST(dbo.fn_FechaHoraCR(pp.FechaPreparacion) AS DATE) = dbo.fn_HoyCR()'),
 -- Permisos de empleados: una solicitud que ya empezo no se aprueba como futura.
 (N'sp_Admin_UpdateEmployeeLeaveRequestStatus',N'CONVERT(DATE, GETDATE())',N'dbo.fn_HoyCR()'),
 -- Inteligencia de inventario: ventana de meses y dias hacia atras desde el dia de Costa Rica.
 (N'sp_Admin_GetPurchaseSuggestions',N'CONVERT(DATE, SYSDATETIME())',N'dbo.fn_HoyCR()'),
 (N'sp_Admin_GetSlowMovingProducts',N'CONVERT(DATE, SYSDATETIME())',N'dbo.fn_HoyCR()');
GO

DECLARE @Obj SYSNAME, @Def NVARCHAR(MAX), @Viejo NVARCHAR(MAX), @Nuevo NVARCHAR(MAX), @Pos INT, @Msg NVARCHAR(400);
DECLARE objetos CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT Objeto FROM #Parches;
OPEN objetos;
FETCH NEXT FROM objetos INTO @Obj;
WHILE @@FETCH_STATUS=0
BEGIN
    SET @Def=OBJECT_DEFINITION(OBJECT_ID(N'dbo.'+@Obj));
    IF @Def IS NULL BEGIN SET @Msg=CONCAT(N'Falta dbo.',@Obj,N'.'); THROW 58914,@Msg,1; END;
    DECLARE parches CURSOR LOCAL FAST_FORWARD FOR SELECT Viejo,Nuevo FROM #Parches WHERE Objeto=@Obj ORDER BY Orden;
    OPEN parches;
    FETCH NEXT FROM parches INTO @Viejo,@Nuevo;
    WHILE @@FETCH_STATUS=0
    BEGIN
        IF CHARINDEX(@Viejo,@Def)>0 SET @Def=REPLACE(@Def,@Viejo,@Nuevo);
        ELSE IF CHARINDEX(@Nuevo,@Def)=0
        BEGIN SET @Msg=CONCAT(N'dbo.',@Obj,N': no se encontro el texto a corregir: ',LEFT(@Viejo,200)); THROW 58915,@Msg,1; END;
        FETCH NEXT FROM parches INTO @Viejo,@Nuevo;
    END;
    CLOSE parches; DEALLOCATE parches;
    -- OBJECT_DEFINITION devuelve «CREATE   PROCEDURE ...» aunque se haya creado con CREATE OR ALTER.
    SET @Pos=PATINDEX(N'%CREATE%',@Def);
    IF @Pos=0 BEGIN SET @Msg=CONCAT(N'dbo.',@Obj,N': la definicion no empieza con CREATE.'); THROW 58916,@Msg,1; END;
    SET @Def=STUFF(@Def,@Pos,6,N'ALTER');
    EXEC sys.sp_executesql @Def;
    FETCH NEXT FROM objetos INTO @Obj;
END;
CLOSE objetos; DEALLOCATE objetos;
DROP TABLE #Parches;
GO

IF XACT_STATE()<>1 THROW 58917,N'La transacción 0341 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 58918,N'SHA-256 inválido para 0341.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0341_reloj_costa_rica_hoy',N'0341_reloj_costa_rica_hoy.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'POST-qa3-06: «hoy» de Costa Rica (fn_HoyCR) en alertas de flota, promociones, resumen del chofer, facturas vencidas, comodatos y mantenimientos. Reemplazo de texto sobre la definicion vigente.');
COMMIT TRANSACTION;
GO
