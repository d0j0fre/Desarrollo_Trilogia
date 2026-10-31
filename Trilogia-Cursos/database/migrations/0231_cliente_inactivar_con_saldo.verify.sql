SET NOCOUNT ON;
SET XACT_ABORT OFF;

/* Verificación de 0231. Solo lectura: las pruebas corren en transacciones
   que siempre se revierten. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
               WHERE MigrationId = N'0231_cliente_inactivar_con_saldo' AND Status = N'Applied' AND LEN(FileSha256) = 64)
    THROW 56330, N'0231 no figura aplicada correctamente.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_UpdateClient') AND definition LIKE N'%fn_Cliente_EstadoDeuda%')
   OR NOT EXISTS (SELECT 1 FROM sys.sql_modules WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_ToggleClientStatus') AND definition LIKE N'%fn_Cliente_EstadoDeuda%')
    THROW 56331, N'Los procedimientos de clientes no consultan la deuda.', 1;

/* Prueba revertida: un cliente activo con un cargo de 60 000 no se inactiva
   por ninguno de los dos caminos. */
DECLARE @UsuarioId INT = (SELECT TOP 1 u.UsuarioId FROM dbo.Usuarios u
                          INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId
                          WHERE p.Nombre = N'Cliente' AND u.Activo = 1 ORDER BY u.UsuarioId);
DECLARE @Error1 INT = 0, @Error2 INT = 0, @Activo BIT;
IF @UsuarioId IS NOT NULL
BEGIN
    DECLARE @Nombre NVARCHAR(150), @Correo NVARCHAR(150);
    SELECT @Nombre = NombreCompleto, @Correo = Correo FROM dbo.Usuarios WHERE UsuarioId = @UsuarioId;

    BEGIN TRANSACTION;
        INSERT dbo.ClienteCreditoMovimientos (UsuarioId, TipoMovimiento, Monto, Descripcion)
        VALUES (@UsuarioId, N'Cargo', 60000, N'verify 0231');
        BEGIN TRY
            EXEC dbo.sp_Admin_ToggleClientStatus @UsuarioId = @UsuarioId, @MotivoInactivacion = N'verify 0231';
        END TRY
        BEGIN CATCH
            SET @Error1 = ERROR_NUMBER();
        END CATCH;
        BEGIN TRY
            EXEC dbo.sp_Admin_UpdateClient @UsuarioId = @UsuarioId, @NombreCompleto = @Nombre, @Correo = @Correo,
                 @Activo = 0, @MotivoInactivacion = N'verify 0231';
        END TRY
        BEGIN CATCH
            SET @Error2 = ERROR_NUMBER();
        END CATCH;
        SELECT @Activo = Activo FROM dbo.Usuarios WHERE UsuarioId = @UsuarioId;
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    IF @Error1 <> 51012 OR @Error2 <> 51012 OR @Activo <> 1
        THROW 56332, N'Un cliente con saldo pendiente se pudo inactivar.', 1;
END;

PRINT N'0231 verificada.';
