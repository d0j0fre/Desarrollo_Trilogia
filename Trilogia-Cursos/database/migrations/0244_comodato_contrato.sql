SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-16-01 · CU-162-01: al asignar un comodato no se emite el contrato de
   comodato. La aplicación genera el contrato en PDF con los datos del
   comodato; este procedimiento los entrega en una sola lectura (el listado
   sp_Comodato_List no trae la condición de entrega, las observaciones ni
   quién lo registró).

   Solo lectura y aditiva: un procedimiento nuevo, ninguno existente cambia.
   Rollback en 0244_comodato_contrato.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56450,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0244_comodato_contrato' AND Status=N'Applied')
    THROW 56451,N'0244 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.Comodatos',N'U') IS NULL OR OBJECT_ID(N'dbo.Activos',N'U') IS NULL
    THROW 56452,N'Faltan dbo.Comodatos o dbo.Activos.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Comodato_GetContrato
    @ComodatoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.ComodatoId, a.CodigoActivo, a.Nombre AS ActivoNombre, a.Tipo AS ActivoTipo,
           c.ClienteNombre, ISNULL(c.ClienteIdentificacion, N'') AS ClienteIdentificacion,
           ISNULL(c.Ubicacion, N'') AS Ubicacion, c.FechaAsignacion,
           ISNULL(c.CondicionEntrega, N'') AS CondicionEntrega, ISNULL(c.Observaciones, N'') AS Observaciones,
           ISNULL(c.RegistradoPorNombre, N'') AS RegistradoPorNombre, c.FechaRegistro, c.Estado
    FROM dbo.Comodatos c
    INNER JOIN dbo.Activos a ON a.ActivoId = c.ActivoId
    WHERE c.ComodatoId = @ComodatoId;
END;
GO

IF XACT_STATE()<>1 THROW 56453,N'La transacción 0244 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56454,N'SHA-256 inválido para 0244.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0244_comodato_contrato',N'0244_comodato_contrato.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-16-01: sp_Comodato_GetContrato con los datos del contrato de comodato.');
COMMIT TRANSACTION;
GO
