SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Compras_CrearProveedor
    @Nombre NVARCHAR(150),
    @Contacto NVARCHAR(150) = NULL,
    @Telefono NVARCHAR(30) = NULL,
    @Email NVARCHAR(150) = NULL,
    @Activo BIT = 1,
    @NuevoProveedorId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Proveedores (Nombre, Contacto, Telefono, Email, Activo)
    VALUES (@Nombre, @Contacto, @Telefono, @Email, @Activo);

    SET @NuevoProveedorId = SCOPE_IDENTITY();
END;
GO

----------------------------------------------------------------------------------------

/* Registro en la bitácora de migraciones */
IF OBJECT_ID('dbo.SchemaMigrationHistory', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0011_compras_proveedores_crear_activo')
BEGIN
    INSERT INTO dbo.SchemaMigrationHistory
        (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
    VALUES
        (N'0011_compras_proveedores_crear_activo', N'0011_compras_proveedores_crear_activo.sql', N'PENDIENTE_CALCULAR_SHA256',
         N'Applied', SUSER_SNAME(), N'DEV', N'CU-101 - Fix: sp_Compras_CrearProveedor ahora acepta @Activo al crear.');
END;
GO