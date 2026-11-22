SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* POST-qa2-10 (ronda 3, CU-131-05): el tablero gerencial y el Asistente contaban «stock bajo» con la columna
   EstadoStock de la base, que usa un tope fijo de 5 unidades, mientras que Inventario y el Reporte de inventario
   usan el stock minimo de cada producto. Con el mismo inventario el tablero decia 6 bajos y Inventario 5; por
   ejemplo un producto con 3 unidades y minimo 1 salia como bajo en el tablero y uno con 9 unidades y minimo 10
   no salia.

   Regla unica (la misma de Inventario): agotado = Stock <= 0; bajo = Stock > 0 y Stock <= StockMinimo.
   Se corrige con un reemplazo de texto sobre la definicion VIGENTE (OBJECT_DEFINITION), igual que 0341, para no
   pisar cambios de otras migraciones. Si un texto a corregir no se encuentra (y el corregido tampoco esta) la
   migracion falla sin cambiar nada.

     sp_Reportes_DashboardKpis, sp_Asistente_Metricas : EstadoStock = N'Bajo' / N'Agotado' -> regla por StockMinimo
     sp_Reportes_DashboardDetalle (indicador stock)    : EstadoStock IN (N'Bajo', N'Agotado') y la etiqueta

   No cambia tablas ni datos. Rollback en 0355_tablero_stock_minimo.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58930,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0355_tablero_stock_minimo' AND Status=N'Applied')
    THROW 58931,N'0355 ya figura aplicada.',1;

BEGIN TRANSACTION;
GO

CREATE TABLE #Parches(Orden INT IDENTITY(1,1) PRIMARY KEY, Objeto SYSNAME NOT NULL, Viejo NVARCHAR(MAX) NOT NULL, Nuevo NVARCHAR(MAX) NOT NULL);
INSERT #Parches(Objeto,Viejo,Nuevo) VALUES
 (N'sp_Reportes_DashboardKpis',N'EstadoStock = N''Bajo''',N'(Stock > 0 AND Stock <= StockMinimo)'),
 (N'sp_Reportes_DashboardKpis',N'EstadoStock = N''Agotado''',N'Stock <= 0'),
 (N'sp_Asistente_Metricas',N'EstadoStock = N''Bajo''',N'(Stock > 0 AND Stock <= StockMinimo)'),
 (N'sp_Asistente_Metricas',N'EstadoStock = N''Agotado''',N'Stock <= 0'),
 (N'sp_Reportes_DashboardDetalle',N'EstadoStock IN (N''Bajo'', N''Agotado'')',N'(Stock <= 0 OR Stock <= StockMinimo)'),
 (N'sp_Reportes_DashboardDetalle',N'N'' - '', EstadoStock)',N'N'' - '', CASE WHEN Stock <= 0 THEN N''Agotado'' ELSE N''Bajo'' END)');
GO

DECLARE @Obj SYSNAME, @Def NVARCHAR(MAX), @Viejo NVARCHAR(MAX), @Nuevo NVARCHAR(MAX), @Pos INT, @Msg NVARCHAR(400);
DECLARE objetos CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT Objeto FROM #Parches;
OPEN objetos;
FETCH NEXT FROM objetos INTO @Obj;
WHILE @@FETCH_STATUS=0
BEGIN
    SET @Def=OBJECT_DEFINITION(OBJECT_ID(N'dbo.'+@Obj));
    IF @Def IS NULL BEGIN SET @Msg=CONCAT(N'Falta dbo.',@Obj,N'.'); THROW 58932,@Msg,1; END;
    DECLARE parches CURSOR LOCAL FAST_FORWARD FOR SELECT Viejo,Nuevo FROM #Parches WHERE Objeto=@Obj ORDER BY Orden;
    OPEN parches;
    FETCH NEXT FROM parches INTO @Viejo,@Nuevo;
    WHILE @@FETCH_STATUS=0
    BEGIN
        IF CHARINDEX(@Viejo,@Def)>0 SET @Def=REPLACE(@Def,@Viejo,@Nuevo);
        ELSE IF CHARINDEX(@Nuevo,@Def)=0
        BEGIN SET @Msg=CONCAT(N'dbo.',@Obj,N': no se encontro el texto a corregir: ',LEFT(@Viejo,200)); THROW 58933,@Msg,1; END;
        FETCH NEXT FROM parches INTO @Viejo,@Nuevo;
    END;
    CLOSE parches; DEALLOCATE parches;
    -- OBJECT_DEFINITION devuelve «CREATE   PROCEDURE ...» aunque se haya creado con CREATE OR ALTER.
    SET @Pos=PATINDEX(N'%CREATE%',@Def);
    IF @Pos=0 BEGIN SET @Msg=CONCAT(N'dbo.',@Obj,N': la definicion no empieza con CREATE.'); THROW 58934,@Msg,1; END;
    SET @Def=STUFF(@Def,@Pos,6,N'ALTER');
    EXEC sys.sp_executesql @Def;
    FETCH NEXT FROM objetos INTO @Obj;
END;
CLOSE objetos; DEALLOCATE objetos;
DROP TABLE #Parches;
GO

IF XACT_STATE()<>1 THROW 58935,N'La transacción 0355 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 58936,N'SHA-256 inválido para 0355.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0355_tablero_stock_minimo',N'0355_tablero_stock_minimo.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'POST-qa2-10: el tablero gerencial y el Asistente cuentan stock bajo y agotado con el stock minimo de cada producto (la misma regla de Inventario), no con el tope fijo de 5 de EstadoStock. Reemplazo de texto sobre la definicion vigente.');
COMMIT TRANSACTION;
GO
