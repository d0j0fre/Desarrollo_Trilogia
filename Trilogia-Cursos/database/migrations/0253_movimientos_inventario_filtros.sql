SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* G2-42: la lista de Inventario -> Movimientos no tenia busqueda ni filtros.

   sp_Admin_GetInventoryMovements devolvia los ultimos 200 movimientos sin
   parametros. Se le agregan filtros opcionales: texto del producto, tipo de
   movimiento y rango de fechas. Sin parametros devuelve exactamente lo mismo
   que antes (los ultimos 200, mismo orden y mismas columnas), asi que el codigo
   que ya lo llama no cambia. Las fechas se comparan por dia: @Hasta incluye
   todo ese dia.

   Solo reemplaza ese procedimiento de lectura. Rollback en
   0253_movimientos_inventario_filtros.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56530,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0253_movimientos_inventario_filtros' AND Status=N'Applied')
    THROW 56531,N'0253 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.MovimientosInventario',N'U') IS NULL OR OBJECT_ID(N'dbo.sp_Admin_GetInventoryMovements',N'P') IS NULL
    THROW 56532,N'Faltan dbo.MovimientosInventario o dbo.sp_Admin_GetInventoryMovements.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetInventoryMovements
    @Buscar NVARCHAR(150) = NULL,
    @TipoMovimiento NVARCHAR(20) = NULL,
    @Desde DATE = NULL,
    @Hasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Texto NVARCHAR(150) = NULLIF(LTRIM(RTRIM(@Buscar)), N'');
    DECLARE @Tipo NVARCHAR(20) = NULLIF(LTRIM(RTRIM(@TipoMovimiento)), N'');

    SELECT TOP 200
        MovimientoId,
        ProductoId,
        ProductoNombre,
        TipoMovimiento,
        Cantidad,
        StockAnterior,
        StockNuevo,
        Motivo,
        UsuarioNombre,
        FechaMovimiento
    FROM dbo.MovimientosInventario
    WHERE (@Texto IS NULL OR ProductoNombre LIKE N'%' + @Texto + N'%')
      AND (@Tipo IS NULL OR TipoMovimiento = @Tipo)
      AND (@Desde IS NULL OR FechaMovimiento >= @Desde)
      AND (@Hasta IS NULL OR FechaMovimiento < DATEADD(DAY, 1, CAST(@Hasta AS DATETIME2)))
    ORDER BY FechaMovimiento DESC, MovimientoId DESC;
END;
GO

IF XACT_STATE()<>1 THROW 56533,N'La transacción 0253 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56534,N'SHA-256 inválido para 0253.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0253_movimientos_inventario_filtros',N'0253_movimientos_inventario_filtros.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'G2-42: filtros opcionales (producto, tipo, fechas) en sp_Admin_GetInventoryMovements; sin parámetros, mismo resultado. Solo lectura.');
COMMIT TRANSACTION;
GO
