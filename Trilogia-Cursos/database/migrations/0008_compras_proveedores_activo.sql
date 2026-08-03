/* =========================================================
   CU-101 / Sprint 4 - Fix: permitir reactivar proveedor desde Editar
   Objetivo:
   - sp_Compras_ActualizarProveedor ahora también actualiza Activo.
   ========================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Compras_ActualizarProveedor
    @ProveedorId INT,
    @Nombre NVARCHAR(150),
    @Contacto NVARCHAR(150) = NULL,
    @Telefono NVARCHAR(30) = NULL,
    @Email NVARCHAR(150) = NULL,
    @Activo BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Proveedores
    SET Nombre = @Nombre,
        Contacto = @Contacto,
        Telefono = @Telefono,
        Email = @Email,
        Activo = @Activo,
        FechaActualizacion = SYSUTCDATETIME()
    WHERE ProveedorId = @ProveedorId;
END;
GO

---------------------------------------------------------------------------------------------
/* Registro en la bitácora de migraciones */
IF OBJECT_ID('dbo.SchemaMigrationHistory', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0008_compras_proveedores_activo')
BEGIN
    INSERT INTO dbo.SchemaMigrationHistory
        (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
    VALUES
        (N'0008_compras_proveedores_activo', N'0008_compras_proveedores_activo.sql', N'PENDIENTE_CALCULAR_SHA256',
         N'Applied', SUSER_SNAME(), N'DEV', N'CU-101 - Fix: sp_Compras_ActualizarProveedor ahora actualiza Activo.');
END;
GO