SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* POST-qa2-11 (ronda 3, CU-093-04, prueba de horas): los «dias de atraso» de una factura a credito vencida se
   calculaban con DATEDIFF(DAY, vencimiento, SYSDATETIME()). En Azure el reloj del servidor va en UTC, asi que
   a partir de las 6:00 p. m. de Costa Rica ya era «manana» y el atraso salia con un dia de mas: la factura
   FE-CR-2026-008 (vencio el 07/10/2026) decia 47 dias el 22/11 a las 6:09 p. m. en Costa Rica, cuando son 46
   (y antes de las 6:00 p. m. decia 46). Se veia igual en Cuentas por cobrar > Detalle, en la ficha del cliente,
   en el aviso de comodatos y en el texto de la notificacion de factura vencida.

   Se corrige con un reemplazo de texto sobre la definicion VIGENTE (OBJECT_DEFINITION), igual que 0341:
     DATEDIFF(DAY, <vencimiento>, SYSDATETIME())
       -> DATEDIFF(DAY, CAST(dbo.fn_FechaHoraCR(<vencimiento>) AS DATE), dbo.fn_HoyCR())
   (dia de calendario de Costa Rica de ambos extremos). No cambia cuando una factura se considera vencida
   (sigue siendo vencimiento <= instante actual), solo cuantos dias de atraso se cuentan.

     dbo.fn_Cliente_EstadoDeuda        (0230): DiasAtraso de la deuda vencida mas antigua
     dbo.fn_Cliente_FacturasVencidas   (0326): DiasAtraso de cada factura vencida
     dbo.sp_Comodato_Asignar / sp_Comodato_Devolver (0245/0242): fecha de entrega y devolucion por omision
     dbo.sp_Admin_GetSlowMovingProducts (0012): dias sin movimiento y su nivel de riesgo

   Se revisaron con el catalogo (sys.sql_modules) todos los objetos que usan SYSDATETIME/GETDATE/SYSUTCDATETIME:
   el resto solo guarda marcas de tiempo (FechaCreacion, tokens, codigos con hora) o compara instantes, no dias.

   Requiere 0340 (funciones). No cambia tablas ni datos. Rollback en 0356_dias_atraso_reloj_cr.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58940,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0356_dias_atraso_reloj_cr' AND Status=N'Applied')
    THROW 58941,N'0356 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0340_reloj_costa_rica_funciones' AND Status=N'Applied')
    THROW 58942,N'Falta 0340 (funciones del reloj de Costa Rica).',1;

BEGIN TRANSACTION;
GO

CREATE TABLE #Parches(Orden INT IDENTITY(1,1) PRIMARY KEY, Objeto SYSNAME NOT NULL, Viejo NVARCHAR(MAX) NOT NULL, Nuevo NVARCHAR(MAX) NOT NULL);
INSERT #Parches(Objeto,Viejo,Nuevo) VALUES
 (N'fn_Cliente_EstadoDeuda',N'DATEDIFF(DAY, v.VencimientoMasAntiguo, SYSDATETIME())',N'DATEDIFF(DAY, CAST(dbo.fn_FechaHoraCR(v.VencimientoMasAntiguo) AS DATE), dbo.fn_HoyCR())'),
 (N'fn_Cliente_FacturasVencidas',N'DATEDIFF(DAY, p.Vencimiento, SYSDATETIME())',N'DATEDIFF(DAY, CAST(dbo.fn_FechaHoraCR(p.Vencimiento) AS DATE), dbo.fn_HoyCR())'),
 -- Comodatos: la fecha de entrega y de devolucion que se guarda por omision es el dia de Costa Rica.
 (N'sp_Comodato_Asignar',N'CAST(SYSDATETIME() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'sp_Comodato_Devolver',N'CAST(SYSDATETIME() AS DATE)',N'dbo.fn_HoyCR()'),
 -- Productos de lento movimiento: dias sin movimiento por dia de calendario de Costa Rica.
 (N'sp_Admin_GetSlowMovingProducts',N'DATEDIFF(DAY, COALESCE(rotation.LastSale, product.FechaCreacion), SYSDATETIME())',N'DATEDIFF(DAY, CAST(dbo.fn_FechaHoraCR(COALESCE(rotation.LastSale, product.FechaCreacion)) AS DATE), dbo.fn_HoyCR())');
GO

DECLARE @Obj SYSNAME, @Def NVARCHAR(MAX), @Viejo NVARCHAR(MAX), @Nuevo NVARCHAR(MAX), @Pos INT, @Msg NVARCHAR(400);
DECLARE objetos CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT Objeto FROM #Parches;
OPEN objetos;
FETCH NEXT FROM objetos INTO @Obj;
WHILE @@FETCH_STATUS=0
BEGIN
    SET @Def=OBJECT_DEFINITION(OBJECT_ID(N'dbo.'+@Obj));
    IF @Def IS NULL BEGIN SET @Msg=CONCAT(N'Falta dbo.',@Obj,N'.'); THROW 58943,@Msg,1; END;
    DECLARE parches CURSOR LOCAL FAST_FORWARD FOR SELECT Viejo,Nuevo FROM #Parches WHERE Objeto=@Obj ORDER BY Orden;
    OPEN parches;
    FETCH NEXT FROM parches INTO @Viejo,@Nuevo;
    WHILE @@FETCH_STATUS=0
    BEGIN
        IF CHARINDEX(@Viejo,@Def)>0 SET @Def=REPLACE(@Def,@Viejo,@Nuevo);
        ELSE IF CHARINDEX(@Nuevo,@Def)=0
        BEGIN SET @Msg=CONCAT(N'dbo.',@Obj,N': no se encontro el texto a corregir: ',LEFT(@Viejo,200)); THROW 58944,@Msg,1; END;
        FETCH NEXT FROM parches INTO @Viejo,@Nuevo;
    END;
    CLOSE parches; DEALLOCATE parches;
    -- OBJECT_DEFINITION devuelve «CREATE   FUNCTION ...» aunque se haya creado con CREATE OR ALTER.
    SET @Pos=PATINDEX(N'%CREATE%',@Def);
    IF @Pos=0 BEGIN SET @Msg=CONCAT(N'dbo.',@Obj,N': la definicion no empieza con CREATE.'); THROW 58945,@Msg,1; END;
    SET @Def=STUFF(@Def,@Pos,6,N'ALTER');
    EXEC sys.sp_executesql @Def;
    FETCH NEXT FROM objetos INTO @Obj;
END;
CLOSE objetos; DEALLOCATE objetos;
DROP TABLE #Parches;
GO

IF XACT_STATE()<>1 THROW 58946,N'La transacción 0356 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 58947,N'SHA-256 inválido para 0356.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0356_dias_atraso_reloj_cr',N'0356_dias_atraso_reloj_cr.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'POST-qa2-11: los dias de atraso de las facturas a credito vencidas se cuentan con el dia de Costa Rica (fn_HoyCR) y no con SYSDATETIME() en UTC. Reemplazo de texto sobre fn_Cliente_EstadoDeuda y fn_Cliente_FacturasVencidas.');
COMMIT TRANSACTION;
GO
