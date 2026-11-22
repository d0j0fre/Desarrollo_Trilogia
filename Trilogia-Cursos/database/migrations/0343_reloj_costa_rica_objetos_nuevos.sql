SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* POST-qa3-06 (integracion): 0330-0333 (total con IVA y promociones en venta movil) y 0335 (avisos de correo
   de documentos y de flota) crearon objetos nuevos que cuentan «hoy» con el reloj del servidor SQL, UTC en
   Azure. A partir de las 6 p. m. de Costa Rica «hoy» ya era «manana»:

     - dbo.fn_Promociones_Aplicar (0331): la promocion vigente que usan la cotizacion, la venta movil y el
       checkout vencia a las 6 p. m. de su ultimo dia y empezaba a las 6 p. m. del dia anterior.
     - dbo.sp_DocumentAlert_PendingNotifications (0335): el aviso por correo de un documento por vencer.
     - dbo.sp_FlotaDocumentos_AvisosPendientes (0335): los avisos de vencimiento de la flota (umbrales y
       la ventana de 30 dias hacia atras).

   Misma tecnica de 0341: reemplazo de texto sobre la definicion VIGENTE (OBJECT_DEFINITION), no una copia
   completa; si el texto no se encuentra (y el corregido tampoco esta) falla sin cambiar nada.

     CONVERT(DATE, SYSDATETIME()) / CAST(SYSDATETIME() AS DATE)  ->  dbo.fn_HoyCR()

   Requiere 0331, 0335 y 0340. No cambia tablas ni datos guardados. Rollback en
   0343_reloj_costa_rica_objetos_nuevos.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58970,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0343_reloj_costa_rica_objetos_nuevos' AND Status=N'Applied')
    THROW 58971,N'0343 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0340_reloj_costa_rica_funciones' AND Status=N'Applied')
    THROW 58972,N'Falta 0340 (funciones del reloj de Costa Rica).',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0331_promociones_calculo_compartido' AND Status=N'Applied')
    OR NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0335_correo_alertas_pendientes' AND Status=N'Applied')
    THROW 58973,N'Faltan 0331 y/o 0335.',1;

BEGIN TRANSACTION;
GO

CREATE TABLE #Parches(Orden INT IDENTITY(1,1) PRIMARY KEY, Objeto SYSNAME NOT NULL, Viejo NVARCHAR(MAX) NOT NULL, Nuevo NVARCHAR(MAX) NOT NULL);
INSERT #Parches(Objeto,Viejo,Nuevo) VALUES
 (N'fn_Promociones_Aplicar',N'CONVERT(DATE, SYSDATETIME())',N'dbo.fn_HoyCR()'),
 (N'sp_DocumentAlert_PendingNotifications',N'CAST(SYSDATETIME() AS DATE)',N'dbo.fn_HoyCR()'),
 (N'sp_FlotaDocumentos_AvisosPendientes',N'CAST(SYSDATETIME() AS DATE)',N'dbo.fn_HoyCR()');
GO

DECLARE @Obj SYSNAME, @Def NVARCHAR(MAX), @Viejo NVARCHAR(MAX), @Nuevo NVARCHAR(MAX), @Pos INT, @Msg NVARCHAR(400);
DECLARE objetos CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT Objeto FROM #Parches;
OPEN objetos;
FETCH NEXT FROM objetos INTO @Obj;
WHILE @@FETCH_STATUS=0
BEGIN
    SET @Def=OBJECT_DEFINITION(OBJECT_ID(N'dbo.'+@Obj));
    IF @Def IS NULL BEGIN SET @Msg=CONCAT(N'Falta dbo.',@Obj,N'.'); THROW 58974,@Msg,1; END;
    DECLARE parches CURSOR LOCAL FAST_FORWARD FOR SELECT Viejo,Nuevo FROM #Parches WHERE Objeto=@Obj ORDER BY Orden;
    OPEN parches;
    FETCH NEXT FROM parches INTO @Viejo,@Nuevo;
    WHILE @@FETCH_STATUS=0
    BEGIN
        IF CHARINDEX(@Viejo,@Def)>0 SET @Def=REPLACE(@Def,@Viejo,@Nuevo);
        ELSE IF CHARINDEX(@Nuevo,@Def)=0
        BEGIN SET @Msg=CONCAT(N'dbo.',@Obj,N': no se encontro el texto a corregir: ',LEFT(@Viejo,200)); THROW 58975,@Msg,1; END;
        FETCH NEXT FROM parches INTO @Viejo,@Nuevo;
    END;
    CLOSE parches; DEALLOCATE parches;
    -- OBJECT_DEFINITION devuelve «CREATE   PROCEDURE ...» aunque se haya creado con CREATE OR ALTER.
    SET @Pos=PATINDEX(N'%CREATE%',@Def);
    IF @Pos=0 BEGIN SET @Msg=CONCAT(N'dbo.',@Obj,N': la definicion no empieza con CREATE.'); THROW 58976,@Msg,1; END;
    SET @Def=STUFF(@Def,@Pos,6,N'ALTER');
    EXEC sys.sp_executesql @Def;
    FETCH NEXT FROM objetos INTO @Obj;
END;
CLOSE objetos; DEALLOCATE objetos;
DROP TABLE #Parches;
GO

IF XACT_STATE()<>1 THROW 58977,N'La transacción 0343 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 58978,N'SHA-256 inválido para 0343.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0343_reloj_costa_rica_objetos_nuevos',N'0343_reloj_costa_rica_objetos_nuevos.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'POST-qa3-06: «hoy» de Costa Rica (fn_HoyCR) en la vigencia de promociones (fn_Promociones_Aplicar) y en los avisos de correo de documentos y flota (0335). Reemplazo de texto sobre la definicion vigente.');
COMMIT TRANSACTION;
GO
