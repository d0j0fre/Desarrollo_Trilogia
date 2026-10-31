SET NOCOUNT ON;

/* Verificacion de 0250. Solo lectura. */
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0250_asistente_entregas_pendientes' AND Status = N'Applied')
    THROW 56550, N'0250 no figura aplicada en el ledger.', 1;
IF OBJECT_ID(N'dbo.sp_Asistente_Metricas', N'P') IS NULL
    THROW 56551, N'Falta dbo.sp_Asistente_Metricas.', 1;
IF OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_Asistente_Metricas')) NOT LIKE N'%r.Estado = N''Despachada''%'
    THROW 56552, N'sp_Asistente_Metricas no tiene la definicion de entregas pendientes de 0250.', 1;

/* El resumen debe coincidir con la suma de la columna Pendientes de las rutas
   despachadas del listado de Rutas y Entregas. */
DECLARE @Metricas TABLE (
    VentasHoy DECIMAL(18,2), VentasMes DECIMAL(18,2), VentasAnio DECIMAL(18,2), FacturasMes INT, TicketPromedioMes DECIMAL(18,2),
    CobrosPendientes DECIMAL(18,2), ClientesConCredito INT, CreditosBloqueados INT, ValorInventario DECIMAL(18,2),
    TotalProductos INT, StockBajo INT, ProductosAgotados INT, DevolucionesCuarentena INT,
    PedidosHoy INT, PedidosPendientes INT, PedidosRetenidos INT, PedidosEnRuta INT, PedidosEntregadosHoy INT,
    TotalClientes INT, ClientesActivos INT, ConsultasPendientes INT, TotalEmpleados INT, SolicitudesPendientes INT, TareasPendientes INT,
    RutasPlanificadas INT, RutasDespachadas INT, EntregasPendientes INT, EntregasFallidas INT,
    TotalVehiculos INT, VehiculosActivos INT, MantenimientosProgramados INT, AlertasFlota INT, TotalActivos INT, ActivosPrestados INT);
INSERT @Metricas EXEC dbo.sp_Asistente_Metricas;

DECLARE @Resumen INT = (SELECT EntregasPendientes FROM @Metricas);
DECLARE @Listado INT = (
    SELECT COUNT(*) FROM dbo.RutaPedidos rp INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId
    WHERE r.Estado = N'Despachada' AND rp.EstadoEntrega IN (N'Pendiente', N'EnRuta'));
IF @Resumen <> @Listado
    THROW 56553, N'Las entregas pendientes del asistente no coinciden con las paradas pendientes de las rutas despachadas.', 1;

SELECT @Resumen AS EntregasPendientesAsistente, @Listado AS ParadasPendientesRutasDespachadas;
PRINT N'0250 verificada.';
