SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM sys.parameters WHERE object_id=OBJECT_ID(N'dbo.sp_Metas_Upsert') AND name=N'@Reemplazar') THROW 57983,N'sp_Metas_Upsert no tiene @Reemplazar.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0298_metas_reemplazo_confirmado' AND Status=N'Applied') THROW 57984,N'La migracion 0298 no esta registrada.',1;
-- Prueba funcional dentro de una transaccion que se deshace: crear, rechazar sin confirmar y reemplazar confirmando.
DECLARE @v INT=(SELECT TOP (1) u.UsuarioId FROM dbo.Usuarios u INNER JOIN dbo.Perfiles p ON p.PerfilId=u.PerfilId WHERE p.Nombre=N'Vendedor' ORDER BY u.UsuarioId);
IF @v IS NOT NULL
BEGIN
    BEGIN TRANSACTION;
    DECLARE @rechazo BIT=0,@monto DECIMAL(18,2);
    EXEC dbo.sp_Metas_Upsert @VendedorUsuarioId=@v,@Anio=2099,@Mes=12,@MontoMeta=1000;
    BEGIN TRY EXEC dbo.sp_Metas_Upsert @VendedorUsuarioId=@v,@Anio=2099,@Mes=12,@MontoMeta=2000; END TRY
    BEGIN CATCH IF ERROR_NUMBER()=53134 SET @rechazo=1; END CATCH;
    SELECT @monto=MontoMeta FROM dbo.MetasVendedor WHERE VendedorUsuarioId=@v AND Anio=2099 AND Mes=12;
    IF @rechazo=0 OR @monto<>1000 THROW 57985,N'La meta existente se sobrescribio sin confirmacion.',1;
    EXEC dbo.sp_Metas_Upsert @VendedorUsuarioId=@v,@Anio=2099,@Mes=12,@MontoMeta=2000,@Reemplazar=1;
    SELECT @monto=MontoMeta FROM dbo.MetasVendedor WHERE VendedorUsuarioId=@v AND Anio=2099 AND Mes=12;
    IF @monto<>2000 THROW 57986,N'El reemplazo confirmado no se aplico.',1;
    ROLLBACK TRANSACTION;
END;
PRINT N'Verificacion 0298 completada.';
