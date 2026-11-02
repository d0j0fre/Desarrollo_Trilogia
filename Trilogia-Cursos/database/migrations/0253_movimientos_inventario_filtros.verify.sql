SET NOCOUNT ON;

/* Verificacion de 0253. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0253_movimientos_inventario_filtros' AND Status = N'Applied')
    THROW 56580, N'0253 no figura aplicada en el ledger.', 1;
IF (SELECT COUNT(*) FROM sys.parameters WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_GetInventoryMovements')
       AND name IN (N'@Buscar', N'@TipoMovimiento', N'@Desde', N'@Hasta')) <> 4
    THROW 56581, N'sp_Admin_GetInventoryMovements no tiene los filtros de la 0253.', 1;

DECLARE @Lista TABLE (MovimientoId INT, ProductoId INT, ProductoNombre NVARCHAR(150), TipoMovimiento NVARCHAR(20), Cantidad INT,
    StockAnterior INT, StockNuevo INT, Motivo NVARCHAR(250), UsuarioNombre NVARCHAR(150), FechaMovimiento DATETIME2);

/* Sin parametros: los mismos ultimos 200 de siempre. */
INSERT @Lista EXEC dbo.sp_Admin_GetInventoryMovements;
IF (SELECT COUNT(*) FROM @Lista) <> (SELECT COUNT(*) FROM (SELECT TOP 200 MovimientoId FROM dbo.MovimientosInventario) x)
    THROW 56582, N'Sin filtros no devuelve los últimos 200 movimientos.', 1;

/* Con tipo: solo ese tipo. */
DELETE @Lista;
INSERT @Lista EXEC dbo.sp_Admin_GetInventoryMovements @TipoMovimiento = N'Entrada';
IF EXISTS (SELECT 1 FROM @Lista WHERE TipoMovimiento <> N'Entrada')
    THROW 56583, N'El filtro por tipo devuelve otros tipos.', 1;

SELECT TipoMovimiento, COUNT(*) AS Movimientos FROM dbo.MovimientosInventario GROUP BY TipoMovimiento;
PRINT N'Verificación 0253 completada.';
