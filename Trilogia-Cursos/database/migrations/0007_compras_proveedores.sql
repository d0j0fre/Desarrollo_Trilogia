/* =========================================================
   CU-101 / Sprint 4 - Compras y Proveedores: Gestión de proveedores
   Objetivo:
   - Registrar, editar y desactivar proveedores.
   - Base para asociar órdenes de compra (CU-102) más adelante.
   Script seguro: usa IF/CREATE OR ALTER y no elimina datos existentes.
   ========================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* 1. Tabla Proveedores */
IF OBJECT_ID('dbo.Proveedores', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Proveedores
    (
        ProveedorId INT IDENTITY(1,1) NOT NULL,
        Nombre NVARCHAR(150) NOT NULL,
        Contacto NVARCHAR(150) NULL,
        Telefono NVARCHAR(30) NULL,
        Email NVARCHAR(150) NULL,
        Activo BIT NOT NULL CONSTRAINT DF_Proveedores_Activo DEFAULT (1),
        FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_Proveedores_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion DATETIME2(0) NULL,
        CONSTRAINT PK_Proveedores PRIMARY KEY (ProveedorId)
    );
END;

/* 2. Registro en la bitácora de migraciones */
IF OBJECT_ID('dbo.SchemaMigrationHistory', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0002_compras_proveedores')
BEGIN
    INSERT INTO dbo.SchemaMigrationHistory
        (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
    VALUES
        (N'0007_compras_proveedores', N'0007_compras_proveedores.sql', N'PENDIENTE_CALCULAR_SHA256',
         N'Applied', SUSER_SNAME(), N'DEV', N'CU-101 - Tabla Proveedores + SPs de gestión de proveedores.');
END;

COMMIT TRANSACTION;
GO

/* =========================================================
   Stored Procedures - Gestión de Proveedores (CU-101)
   ========================================================= */

CREATE OR ALTER PROCEDURE dbo.sp_Compras_ListarProveedores
    @SoloActivos BIT = NULL,
    @Filtro NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.ProveedorId,
        p.Nombre,
        p.Contacto,
        p.Telefono,
        p.Email,
        p.Activo,
        p.FechaCreacion,
        p.FechaActualizacion
    FROM dbo.Proveedores p
    WHERE (@SoloActivos IS NULL OR p.Activo = @SoloActivos)
      AND (@Filtro IS NULL OR p.Nombre LIKE '%' + @Filtro + '%')
    ORDER BY p.Nombre;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Compras_CrearProveedor
    @Nombre NVARCHAR(150),
    @Contacto NVARCHAR(150) = NULL,
    @Telefono NVARCHAR(30) = NULL,
    @Email NVARCHAR(150) = NULL,
    @NuevoProveedorId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Proveedores (Nombre, Contacto, Telefono, Email)
    VALUES (@Nombre, @Contacto, @Telefono, @Email);

    SET @NuevoProveedorId = SCOPE_IDENTITY();
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Compras_ActualizarProveedor
    @ProveedorId INT,
    @Nombre NVARCHAR(150),
    @Contacto NVARCHAR(150) = NULL,
    @Telefono NVARCHAR(30) = NULL,
    @Email NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Proveedores
    SET Nombre = @Nombre,
        Contacto = @Contacto,
        Telefono = @Telefono,
        Email = @Email,
        FechaActualizacion = SYSUTCDATETIME()
    WHERE ProveedorId = @ProveedorId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Compras_DesactivarProveedor
    @ProveedorId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Proveedores
    SET Activo = 0,
        FechaActualizacion = SYSUTCDATETIME()
    WHERE ProveedorId = @ProveedorId;
END;
GO