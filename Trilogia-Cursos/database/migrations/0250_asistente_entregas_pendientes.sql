SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-26-04 · CU-261-06: el resumen del Asistente decía "2 entregas
   pendientes" cuando las rutas despachadas tenían 6 paradas sin cerrar.

   sp_Asistente_Metricas contaba solo las paradas con EstadoEntrega
   'Pendiente', de cualquier ruta. Al despachar, sp_Rutas_Dispatch pasa las
   paradas a 'EnRuta', así que las de las rutas despachadas no se contaban y
   sí las de rutas todavía planificadas.

   Definición de "entrega pendiente" que usa el resumen desde esta migración:

       parada (dbo.RutaPedidos) de una ruta con Estado = 'Despachada'
       cuyo EstadoEntrega es 'Pendiente' o 'EnRuta'
       (todavía no está 'Entregado' ni 'Fallido').

   Es la columna "Pendientes" del listado de Rutas y Entregas
   (sp_Rutas_List), sumada solo sobre las rutas despachadas. No incluye rutas
   Planificadas (aún no salen), Completadas ni Canceladas.

   Solo se reemplaza sp_Asistente_Metricas (procedimiento de lectura del
   asistente, cu310). Se copia su definición vigente sin cambios, salvo el
   cálculo de @EntregasPendientes. No se modifica ninguna tabla ni
   procedimiento de rutas. Idempotente. Rollback en
   0250_asistente_entregas_pendientes.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56500,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0250_asistente_entregas_pendientes' AND Status=N'Applied')
    THROW 56501,N'0250 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.sp_Asistente_Metricas',N'P') IS NULL
    THROW 56502,N'Falta dbo.sp_Asistente_Metricas (cu310).',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Asistente_Metricas
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Hoy DATE = CAST(SYSDATETIME() AS DATE);
    DECLARE @IniMes DATE = DATEFROMPARTS(YEAR(@Hoy), MONTH(@Hoy), 1);
    DECLARE @IniAnio DATE = DATEFROMPARTS(YEAR(@Hoy), 1, 1);

    -- ── Financiero ──────────────────────────────────────────
    DECLARE @VentasHoy DECIMAL(18,2), @VentasMes DECIMAL(18,2), @VentasAnio DECIMAL(18,2),
            @FacturasMes INT, @TicketMes DECIMAL(18,2), @CobrosPendientes DECIMAL(18,2),
            @ClientesConCredito INT = 0, @CreditosBloqueados INT = 0, @ValorInventario DECIMAL(18,2) = 0;

    SELECT @VentasHoy = ISNULL(SUM(Total), 0)
    FROM dbo.Facturas WHERE Estado = N'Generada' AND CAST(FechaFactura AS DATE) = @Hoy;

    SELECT @VentasMes = ISNULL(SUM(Total), 0), @FacturasMes = COUNT(*)
    FROM dbo.Facturas WHERE Estado = N'Generada' AND CAST(FechaFactura AS DATE) BETWEEN @IniMes AND @Hoy;

    SELECT @VentasAnio = ISNULL(SUM(Total), 0)
    FROM dbo.Facturas WHERE Estado = N'Generada' AND CAST(FechaFactura AS DATE) BETWEEN @IniAnio AND @Hoy;

    SET @TicketMes = CASE WHEN @FacturasMes > 0 THEN ROUND(@VentasMes / @FacturasMes, 2) ELSE 0 END;

    SET @CobrosPendientes = (
        SELECT ISNULL(SUM(CASE WHEN s.Saldo > 0 THEN s.Saldo ELSE 0 END), 0)
        FROM (
            SELECT cc.UsuarioId,
                   ISNULL((SELECT SUM(CASE cm.TipoMovimiento
                                        WHEN N'Cargo' THEN cm.Monto WHEN N'Abono' THEN -cm.Monto
                                        WHEN N'AjustePositivo' THEN cm.Monto ELSE -cm.Monto END)
                           FROM dbo.ClienteCreditoMovimientos cm WHERE cm.UsuarioId = cc.UsuarioId), 0) AS Saldo
            FROM dbo.ClienteCreditos cc
        ) s);

    SELECT @ClientesConCredito = SUM(CASE WHEN CreditoActivo = 1 THEN 1 ELSE 0 END),
           @CreditosBloqueados = SUM(CASE WHEN CreditoBloqueado = 1 THEN 1 ELSE 0 END)
    FROM dbo.ClienteCreditos;

    SELECT @ValorInventario = ISNULL(SUM(CAST(Stock AS DECIMAL(18,2)) * Precio), 0)
    FROM dbo.Productos WHERE Activo = 1;

    -- ── Inventario ──────────────────────────────────────────
    DECLARE @TotalProductos INT, @StockBajo INT, @Agotados INT, @DevolucionesCuarentena INT = 0;
    SELECT @TotalProductos = COUNT(*),
           @StockBajo = SUM(CASE WHEN EstadoStock = N'Bajo' THEN 1 ELSE 0 END),
           @Agotados  = SUM(CASE WHEN EstadoStock = N'Agotado' THEN 1 ELSE 0 END)
    FROM dbo.Productos WHERE Activo = 1;

    IF OBJECT_ID('dbo.Devoluciones', 'U') IS NOT NULL
        SELECT @DevolucionesCuarentena = COUNT(*) FROM dbo.Devoluciones WHERE Estado = N'EnCuarentena';

    -- ── Pedidos ─────────────────────────────────────────────
    DECLARE @PedidosHoy INT, @PedidosPendientes INT, @PedidosRetenidos INT,
            @PedidosEntregadosHoy INT, @PedidosEnRuta INT = 0;
    SELECT @PedidosHoy = SUM(CASE WHEN CAST(FechaPedido AS DATE) = @Hoy THEN 1 ELSE 0 END),
           @PedidosPendientes = SUM(CASE WHEN Estado = N'Pendiente' THEN 1 ELSE 0 END),
           @PedidosRetenidos = SUM(CASE WHEN Estado = N'Retenido' THEN 1 ELSE 0 END),
           @PedidosEntregadosHoy = SUM(CASE WHEN Estado = N'Entregado' AND CAST(FechaPedido AS DATE) = @Hoy THEN 1 ELSE 0 END)
    FROM dbo.Pedidos;

    IF OBJECT_ID('dbo.RutaPedidos', 'U') IS NOT NULL
        SELECT @PedidosEnRuta = COUNT(*) FROM dbo.RutaPedidos WHERE EstadoEntrega = N'EnRuta';

    -- ── Clientes / consultas ────────────────────────────────
    DECLARE @TotalClientes INT, @ClientesActivos INT, @ConsultasPendientes INT = 0;
    SELECT @TotalClientes = COUNT(*),
           @ClientesActivos = SUM(CASE WHEN u.Activo = 1 THEN 1 ELSE 0 END)
    FROM dbo.Usuarios u INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId
    WHERE p.Nombre = N'Cliente';

    IF OBJECT_ID('dbo.Consultas', 'U') IS NOT NULL
        SELECT @ConsultasPendientes = COUNT(*) FROM dbo.Consultas WHERE Estado = N'Pendiente';

    -- ── Personal ────────────────────────────────────────────
    DECLARE @TotalEmpleados INT = 0, @SolicitudesPendientes INT = 0, @TareasPendientes INT = 0;
    IF OBJECT_ID('dbo.Empleados', 'U') IS NOT NULL
        SELECT @TotalEmpleados = COUNT(*) FROM dbo.Empleados WHERE Activo = 1;
    IF OBJECT_ID('dbo.EmpleadoSolicitudesTiempoLibre', 'U') IS NOT NULL
        SELECT @SolicitudesPendientes = COUNT(*) FROM dbo.EmpleadoSolicitudesTiempoLibre WHERE Estado = N'Pendiente';
    IF OBJECT_ID('dbo.EmpleadoTareas', 'U') IS NOT NULL
        SELECT @TareasPendientes = COUNT(*) FROM dbo.EmpleadoTareas WHERE Estado IN (N'Pendiente', N'En proceso');

    -- ── Logística / flota ───────────────────────────────────
    DECLARE @RutasPlanificadas INT = 0, @RutasDespachadas INT = 0, @EntregasPendientes INT = 0,
            @EntregasFallidas INT = 0, @TotalVehiculos INT = 0, @VehiculosActivos INT = 0,
            @MantenimientosProgramados INT = 0, @AlertasFlota INT = 0, @TotalActivos INT = 0, @ActivosPrestados INT = 0;

    IF OBJECT_ID('dbo.Rutas', 'U') IS NOT NULL
        SELECT @RutasPlanificadas = SUM(CASE WHEN Estado = N'Planificada' THEN 1 ELSE 0 END),
               @RutasDespachadas  = SUM(CASE WHEN Estado = N'Despachada'  THEN 1 ELSE 0 END)
        FROM dbo.Rutas;

    IF OBJECT_ID('dbo.RutaPedidos', 'U') IS NOT NULL
        SELECT @EntregasFallidas   = SUM(CASE WHEN EstadoEntrega = N'Fallido'   THEN 1 ELSE 0 END)
        FROM dbo.RutaPedidos;

    -- BUG-26-04: paradas sin cerrar (Pendiente o EnRuta) de las rutas despachadas,
    -- igual que la columna "Pendientes" del listado de rutas.
    IF OBJECT_ID('dbo.RutaPedidos', 'U') IS NOT NULL AND OBJECT_ID('dbo.Rutas', 'U') IS NOT NULL
        SELECT @EntregasPendientes = COUNT(*)
        FROM dbo.RutaPedidos rp
        INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId
        WHERE r.Estado = N'Despachada'
          AND rp.EstadoEntrega IN (N'Pendiente', N'EnRuta');

    IF OBJECT_ID('dbo.Vehiculos', 'U') IS NOT NULL
        SELECT @TotalVehiculos = COUNT(*), @VehiculosActivos = SUM(CASE WHEN Activo = 1 THEN 1 ELSE 0 END)
        FROM dbo.Vehiculos;

    IF OBJECT_ID('dbo.OrdenesMantenimiento', 'U') IS NOT NULL
        SELECT @MantenimientosProgramados = COUNT(*) FROM dbo.OrdenesMantenimiento WHERE Estado = N'Programada';

    -- Alertas de flota (documentos por vencer/vencidos + preventivos vencidos por fecha o km)
    IF OBJECT_ID('dbo.VehiculoDocumentos', 'U') IS NOT NULL
        SELECT @AlertasFlota = @AlertasFlota + COUNT(*)
        FROM dbo.VehiculoDocumentos WHERE DATEDIFF(DAY, @Hoy, FechaVencimiento) <= 15;

    IF OBJECT_ID('dbo.OrdenesMantenimiento', 'U') IS NOT NULL AND OBJECT_ID('dbo.Vehiculos', 'U') IS NOT NULL
        SELECT @AlertasFlota = @AlertasFlota + COUNT(*)
        FROM dbo.OrdenesMantenimiento m INNER JOIN dbo.Vehiculos v ON v.VehiculoId = m.VehiculoId
        WHERE m.Tipo = N'Preventivo' AND m.Estado = N'Programada'
          AND ((m.FechaProgramada IS NOT NULL AND DATEDIFF(DAY, @Hoy, m.FechaProgramada) <= 15)
               OR (m.KilometrajeProximo IS NOT NULL AND v.KilometrajeActual >= m.KilometrajeProximo));

    IF OBJECT_ID('dbo.Activos', 'U') IS NOT NULL
        SELECT @TotalActivos = COUNT(*), @ActivosPrestados = SUM(CASE WHEN Estado = N'Prestado' THEN 1 ELSE 0 END)
        FROM dbo.Activos;

    -- ── Resultado (una sola fila) ───────────────────────────
    SELECT
        @VentasHoy AS VentasHoy, @VentasMes AS VentasMes, @VentasAnio AS VentasAnio,
        @FacturasMes AS FacturasMes, @TicketMes AS TicketPromedioMes, @CobrosPendientes AS CobrosPendientes,
        @ClientesConCredito AS ClientesConCredito, @CreditosBloqueados AS CreditosBloqueados, @ValorInventario AS ValorInventario,
        @TotalProductos AS TotalProductos, ISNULL(@StockBajo,0) AS StockBajo, ISNULL(@Agotados,0) AS ProductosAgotados,
        @DevolucionesCuarentena AS DevolucionesCuarentena,
        ISNULL(@PedidosHoy,0) AS PedidosHoy, ISNULL(@PedidosPendientes,0) AS PedidosPendientes,
        ISNULL(@PedidosRetenidos,0) AS PedidosRetenidos, @PedidosEnRuta AS PedidosEnRuta,
        ISNULL(@PedidosEntregadosHoy,0) AS PedidosEntregadosHoy,
        @TotalClientes AS TotalClientes, ISNULL(@ClientesActivos,0) AS ClientesActivos, @ConsultasPendientes AS ConsultasPendientes,
        @TotalEmpleados AS TotalEmpleados, @SolicitudesPendientes AS SolicitudesPendientes, @TareasPendientes AS TareasPendientes,
        ISNULL(@RutasPlanificadas,0) AS RutasPlanificadas, ISNULL(@RutasDespachadas,0) AS RutasDespachadas,
        ISNULL(@EntregasPendientes,0) AS EntregasPendientes, ISNULL(@EntregasFallidas,0) AS EntregasFallidas,
        @TotalVehiculos AS TotalVehiculos, ISNULL(@VehiculosActivos,0) AS VehiculosActivos,
        @MantenimientosProgramados AS MantenimientosProgramados, @AlertasFlota AS AlertasFlota,
        @TotalActivos AS TotalActivos, ISNULL(@ActivosPrestados,0) AS ActivosPrestados;
END;
GO

IF XACT_STATE()<>1 THROW 56503,N'La transacción 0250 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56504,N'SHA-256 inválido para 0250.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0250_asistente_entregas_pendientes',N'0250_asistente_entregas_pendientes.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-26-04: sp_Asistente_Metricas cuenta como entregas pendientes las paradas Pendiente/EnRuta de rutas Despachada. Solo lectura.');
COMMIT TRANSACTION;
GO
