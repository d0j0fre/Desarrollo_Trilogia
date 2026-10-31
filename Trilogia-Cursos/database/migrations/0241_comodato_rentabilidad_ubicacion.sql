SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-16-06 · CU-164-01: el reporte de equipos prestados (Comodatos >
   Rentabilidad) no muestra la ubicación del equipo registrada al asignar.

   sp_Comodato_Rentabilidad agrega Ubicacion AL FINAL del resultado
   (posición 11). Las columnas 0–10 no cambian (FechaAsignacion, la fecha de
   entrega, ya venía en la posición 6), así que la aplicación anterior sigue
   leyendo bien.

   Aditiva. Rollback en 0241_comodato_rentabilidad_ubicacion.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56410,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0241_comodato_rentabilidad_ubicacion' AND Status=N'Applied')
    THROW 56411,N'0241 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.Comodatos',N'U') IS NULL OR OBJECT_ID(N'dbo.Pedidos',N'U') IS NULL
    THROW 56412,N'Faltan dbo.Comodatos o dbo.Pedidos.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Comodato_Rentabilidad
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        c.ComodatoId, a.CodigoActivo, a.Nombre AS ActivoNombre, a.Tipo AS ActivoTipo,
        c.ClienteNombre, c.ClienteUsuarioId, c.FechaAsignacion,
        DATEDIFF(DAY, c.FechaAsignacion, CAST(SYSDATETIME() AS DATE)) AS DiasEnComodato,
        ISNULL(compras.NumPedidos, 0)   AS NumPedidos,
        ISNULL(compras.TotalComprado, 0) AS TotalComprado,
        compras.UltimaCompra,
        ISNULL(c.Ubicacion, N'') AS Ubicacion
    FROM dbo.Comodatos c
    INNER JOIN dbo.Activos a ON a.ActivoId = c.ActivoId
    OUTER APPLY (
        SELECT COUNT(*) AS NumPedidos, SUM(p.Total) AS TotalComprado, MAX(p.FechaPedido) AS UltimaCompra
        FROM dbo.Pedidos p
        WHERE c.ClienteUsuarioId IS NOT NULL
          AND p.UsuarioId = c.ClienteUsuarioId
          AND p.Estado IN (N'Entregado', N'Aprobado', N'EnProceso')
          AND p.FechaPedido >= c.FechaAsignacion
    ) compras
    WHERE c.Estado = N'Activo'
    ORDER BY ISNULL(compras.TotalComprado, 0) ASC;  -- menos rentables primero
END;
GO

IF XACT_STATE()<>1 THROW 56413,N'La transacción 0241 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56414,N'SHA-256 inválido para 0241.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0241_comodato_rentabilidad_ubicacion',N'0241_comodato_rentabilidad_ubicacion.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-16-06: sp_Comodato_Rentabilidad devuelve la ubicación del equipo.');
COMMIT TRANSACTION;
GO
