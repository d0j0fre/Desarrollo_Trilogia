SET NOCOUNT ON;
SET XACT_ABORT ON;

/* Obligatorios y explicitos, no heredados de la herramienta. Los indices
   filtrados los exigen, y sqlcmd trae QUOTED_IDENTIFIER apagado. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* Aplicacion movil por rol: bodega, gestion, ventas, personal y oficina.

   Hasta la 0027 la aplicacion solo servia al chofer. Esta migracion le da a
   cada perfil del personal su seccion:

   1. Permisos. Un permiso nuevo (PEDIDOS_PREPARAR) y la asignacion del minimo
      necesario a cada perfil. Siete perfiles de oficina no tenian ningun
      permiso: tampoco podian usar sus modulos en el sitio web. Lo que se asigna
      aqui les abre la seccion movil Y el modulo web equivalente, que es lo que
      su perfil ya prometia en el menu.
   2. Idempotencia. dbo.MovilOperaciones registra el identificador de cada
      escritura hecha desde el telefono, para que la cola offline pueda
      reintentar sin duplicar un movimiento de inventario o una reasignacion.
   3. Preparacion de pedidos en bodega. Se registra en dbo.PedidoPreparacion y
      NO cambia Pedidos.Estado: los pedidos facturados solo pueden pasar a
      Entregado (sp_Admin_UpdateOrderStatus), y las ventas de vendedor se
      facturan al crearse. Mover el estado romperia el flujo web.
   4. Procedimientos sp_Movil_*. Nuevos; ningun procedimiento existente se
      modifica.

   Aditiva. Rollback en 0028_mobile_roles_surface.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 55800,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0028_mobile_roles_surface' AND Status=N'Applied')
    THROW 55801,N'0028 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0025_mobile_driver_surface' AND Status=N'Applied')
    THROW 55802,N'0028 depende de 0025 (superficie movil). Aplicar 0025 primero.',1;
IF OBJECT_ID(N'dbo.Productos',N'U') IS NULL OR OBJECT_ID(N'dbo.MovimientosInventario',N'U') IS NULL
   OR OBJECT_ID(N'dbo.Pedidos',N'U') IS NULL OR OBJECT_ID(N'dbo.Rutas',N'U') IS NULL
   OR OBJECT_ID(N'dbo.RutaPedidos',N'U') IS NULL OR OBJECT_ID(N'dbo.Vehiculos',N'U') IS NULL
   OR OBJECT_ID(N'dbo.Facturas',N'U') IS NULL OR OBJECT_ID(N'dbo.FacturaDetalle',N'U') IS NULL
    THROW 55803,N'Faltan tablas de inventario, pedidos, rutas o facturas.',1;
IF OBJECT_ID(N'dbo.sp_Admin_GetOrderDetailLines',N'P') IS NULL
    THROW 55804,N'Falta sp_Admin_GetOrderDetailLines (combos, migracion 0012).',1;

BEGIN TRANSACTION;
GO

/* ─────────────────────────────────────────────────────────────────────────
   1. Idempotencia de las escrituras moviles.
   ───────────────────────────────────────────────────────────────────────── */
IF OBJECT_ID(N'dbo.MovilOperaciones',N'U') IS NULL
    CREATE TABLE dbo.MovilOperaciones
    (
        SyncGuid       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MovilOperaciones PRIMARY KEY,
        Operacion      NVARCHAR(40)     NOT NULL,
        EntidadId      INT              NOT NULL,
        UsuarioId      INT              NOT NULL,
        Resultado      NVARCHAR(200)    NULL,
        FechaUtc       DATETIME2(0)     NOT NULL CONSTRAINT DF_MovilOperaciones_Fecha DEFAULT SYSUTCDATETIME()
    );
GO

/* ─────────────────────────────────────────────────────────────────────────
   2. Preparacion de pedidos en bodega.
   Una fila por pedido: preparar dos veces el mismo pedido no tiene sentido y
   la clave primaria lo impide.
   ───────────────────────────────────────────────────────────────────────── */
IF OBJECT_ID(N'dbo.PedidoPreparacion',N'U') IS NULL
    CREATE TABLE dbo.PedidoPreparacion
    (
        PedidoId               INT              NOT NULL CONSTRAINT PK_PedidoPreparacion PRIMARY KEY
                                                CONSTRAINT FK_PedidoPreparacion_Pedidos REFERENCES dbo.Pedidos(PedidoId),
        PreparadoPorUsuarioId  INT              NOT NULL,
        PreparadoPorNombre     NVARCHAR(150)    NOT NULL,
        Observaciones          NVARCHAR(300)    NULL,
        SyncGuid               UNIQUEIDENTIFIER NOT NULL,
        FechaPreparacion       DATETIME2(0)     NOT NULL CONSTRAINT DF_PedidoPreparacion_Fecha DEFAULT SYSDATETIME()
    );
GO

/* ─────────────────────────────────────────────────────────────────────────
   3. Permisos.
   ───────────────────────────────────────────────────────────────────────── */
UPDATE dbo.Permisos
SET Modulo=N'Pedidos', Nombre=N'Preparar pedidos en bodega',
    Descripcion=N'Permite marcar desde la aplicación móvil que un pedido quedó preparado para despacho, sin cambiar su estado comercial.', Activo=1
WHERE Codigo=N'PEDIDOS_PREPARAR';
IF @@ROWCOUNT=0
    INSERT dbo.Permisos(Codigo,Modulo,Nombre,Descripcion,Activo)
    VALUES(N'PEDIDOS_PREPARAR',N'Pedidos',N'Preparar pedidos en bodega',
           N'Permite marcar desde la aplicación móvil que un pedido quedó preparado para despacho, sin cambiar su estado comercial.',1);
GO

/* Matriz perfil → permisos. Los nombres de perfil con tilde se comparan por
   patron para no depender de la codificacion con la que se ejecute el script. */
DECLARE @Matriz TABLE (Perfil NVARCHAR(100) NOT NULL, Codigo NVARCHAR(100) NOT NULL);

INSERT @Matriz (Perfil, Codigo) VALUES
    -- Acceso a la aplicacion para el personal que aun no lo tenia.
    (N'Supervisor', N'MOVIL_ACCESO'), (N'Cajero', N'MOVIL_ACCESO'), (N'Facturador', N'MOVIL_ACCESO'),
    (N'Cr_dito y Cobro', N'MOVIL_ACCESO'), (N'Compras', N'MOVIL_ACCESO'), (N'Soporte', N'MOVIL_ACCESO'),
    (N'Auditor Interno', N'MOVIL_ACCESO'),

    -- Registro de jornada propia para todo el personal.
    (N'Chofer', N'RRHH_JORNADAS_REGISTRAR'), (N'Chofer', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Bodeguero', N'RRHH_JORNADAS_REGISTRAR'), (N'Bodeguero', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Gerente', N'RRHH_JORNADAS_REGISTRAR'), (N'Gerente', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Supervisor', N'RRHH_JORNADAS_REGISTRAR'), (N'Supervisor', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Cajero', N'RRHH_JORNADAS_REGISTRAR'), (N'Cajero', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Facturador', N'RRHH_JORNADAS_REGISTRAR'), (N'Facturador', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Cr_dito y Cobro', N'RRHH_JORNADAS_REGISTRAR'), (N'Cr_dito y Cobro', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Compras', N'RRHH_JORNADAS_REGISTRAR'), (N'Compras', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Soporte', N'RRHH_JORNADAS_REGISTRAR'), (N'Soporte', N'RRHH_JORNADAS_VER_PROPIAS'),
    (N'Auditor Interno', N'RRHH_JORNADAS_REGISTRAR'), (N'Auditor Interno', N'RRHH_JORNADAS_VER_PROPIAS'),

    -- Bodega: inventario, recepcion de compras y preparacion de pedidos.
    (N'Bodeguero', N'INVENTARIO_VER'), (N'Bodeguero', N'INVENTARIO_MOVIMIENTOS'),
    (N'Bodeguero', N'COMPRAS_ORDENES_VER'), (N'Bodeguero', N'COMPRAS_ORDENES_RECIBIR'),
    (N'Bodeguero', N'PEDIDOS_VER'), (N'Bodeguero', N'PEDIDOS_PREPARAR'),

    -- Gerencia: consulta de pedidos e inventario (ya gestiona rutas y aprobaciones).
    (N'Gerente', N'PEDIDOS_VER'), (N'Gerente', N'INVENTARIO_VER'),

    -- Oficina: un modulo enfocado por perfil.
    (N'Supervisor', N'RRHH_JORNADAS_APROBAR'),
    (N'Cajero', N'LIQUIDACION_FINANCIERA'),
    (N'Facturador', N'FACTURACION_VER'), (N'Facturador', N'PEDIDOS_VER'),
    (N'Cr_dito y Cobro', N'CREDITOS_VER'), (N'Cr_dito y Cobro', N'CLIENTES_VER'),
    (N'Compras', N'COMPRAS_ORDENES_VER'), (N'Compras', N'COMPRAS_SUGERENCIAS_VER'),
    (N'Compras', N'INVENTARIO_VER'), (N'Compras', N'PROVEEDORES_VER'),
    (N'Soporte', N'CONSULTAS_VER'), (N'Soporte', N'CONSULTAS_ATENDER'),
    (N'Auditor Interno', N'AUDITORIA_VER');

IF EXISTS (SELECT 1 FROM @Matriz m WHERE NOT EXISTS (SELECT 1 FROM dbo.Permisos p WHERE p.Codigo = m.Codigo))
    THROW 55805, N'Uno o más permisos de la matriz no existen en dbo.Permisos.', 1;

INSERT dbo.PerfilPermisos (PerfilId, PermisoId, UsuarioAsignacionId, UsuarioAsignacionNombre)
SELECT DISTINCT profile.PerfilId, permission.PermisoId, NULL, N'Migración 0028 aplicación móvil por rol'
FROM @Matriz m
INNER JOIN dbo.Perfiles profile ON profile.Nombre LIKE m.Perfil
INNER JOIN dbo.Permisos permission ON permission.Codigo = m.Codigo
WHERE NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos assigned
                  WHERE assigned.PerfilId = profile.PerfilId AND assigned.PermisoId = permission.PermisoId);
GO

/* ─────────────────────────────────────────────────────────────────────────
   4. Inventario.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_Movil_Productos_Listar
    @Buscar NVARCHAR(150) = NULL,
    @Filtro NVARCHAR(20)  = NULL,   -- Todos | Bajo | Agotado | Inactivos
    @Top    INT           = 200
AS
BEGIN
    SET NOCOUNT ON;
    SET @Buscar = NULLIF(LTRIM(RTRIM(@Buscar)), N'');
    SET @Filtro = ISNULL(NULLIF(LTRIM(RTRIM(@Filtro)), N''), N'Todos');
    IF @Top IS NULL OR @Top NOT BETWEEN 1 AND 500 SET @Top = 200;

    SELECT TOP (@Top)
        p.ProductoId, p.Nombre, p.Categoria, p.Precio, p.Stock, p.StockMinimo,
        CONVERT(NVARCHAR(10), p.EstadoStock) AS EstadoStock, p.Activo, ISNULL(p.ImagenUrl, N'') AS ImagenUrl
    FROM dbo.Productos p
    WHERE (@Buscar IS NULL OR p.Nombre LIKE N'%' + @Buscar + N'%' OR p.Categoria LIKE N'%' + @Buscar + N'%'
           OR CONVERT(NVARCHAR(20), p.ProductoId) = @Buscar)
      AND (
            (@Filtro = N'Todos')
         OR (@Filtro = N'Bajo'      AND p.Activo = 1 AND p.EstadoStock = 'Bajo')
         OR (@Filtro = N'Agotado'   AND p.Activo = 1 AND p.EstadoStock = 'Agotado')
         OR (@Filtro = N'Inactivos' AND p.Activo = 0)
      )
    ORDER BY p.Activo DESC,
             CASE p.EstadoStock WHEN 'Agotado' THEN 0 WHEN 'Bajo' THEN 1 ELSE 2 END,
             p.Nombre;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Movil_Inventario_MovimientosProducto
    @ProductoId INT,
    @Top        INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @Top IS NULL OR @Top NOT BETWEEN 1 AND 100 SET @Top = 20;

    SELECT TOP (@Top)
        m.MovimientoId, m.TipoMovimiento, m.Cantidad, m.StockAnterior, m.StockNuevo,
        ISNULL(m.Motivo, N'') AS Motivo, m.UsuarioNombre, m.FechaMovimiento
    FROM dbo.MovimientosInventario m
    WHERE m.ProductoId = @ProductoId
    ORDER BY m.FechaMovimiento DESC, m.MovimientoId DESC;
END;
GO

/* Movimiento manual. Mismas reglas que el modulo web
   (AdminDbService.RegisterInventoryMovementAsync): producto activo, Entrada y
   Salida suman o restan, Ajuste fija la cantidad, el stock nunca queda
   negativo. A diferencia del web, lectura, actualizacion y bitacora ocurren en
   una sola transaccion con bloqueo, y el reintento no duplica. */
CREATE OR ALTER PROCEDURE dbo.sp_Movil_Inventario_RegistrarMovimiento
    @ProductoId     INT,
    @TipoMovimiento NVARCHAR(20),
    @Cantidad       INT,
    @Motivo         NVARCHAR(300) = NULL,
    @SyncGuid       UNIQUEIDENTIFIER,
    @UsuarioId      INT,
    @UsuarioNombre  NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @TipoMovimiento = NULLIF(LTRIM(RTRIM(@TipoMovimiento)), N'');
    SET @Motivo = NULLIF(LTRIM(RTRIM(@Motivo)), N'');

    IF @SyncGuid IS NULL THROW 55810, N'Falta el identificador de sincronización.', 1;
    IF @TipoMovimiento IS NULL OR @TipoMovimiento NOT IN (N'Entrada', N'Salida', N'Ajuste')
        THROW 55811, N'El tipo de movimiento no es válido.', 1;
    IF @Cantidad IS NULL OR @Cantidad < 0 OR (@Cantidad = 0 AND @TipoMovimiento <> N'Ajuste')
        THROW 55812, N'La cantidad debe ser mayor a cero.', 1;
    IF @Motivo IS NULL AND @TipoMovimiento IN (N'Salida', N'Ajuste')
        THROW 55813, N'Indicá el motivo de la salida o del ajuste.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Existente INT, @OperacionExistente NVARCHAR(40);
        SELECT @Existente = EntidadId, @OperacionExistente = Operacion
        FROM dbo.MovilOperaciones WITH (UPDLOCK, HOLDLOCK)
        WHERE SyncGuid = @SyncGuid;

        IF @Existente IS NOT NULL
        BEGIN
            IF @OperacionExistente <> N'InventarioMovimiento'
                THROW 55814, N'El identificador de sincronización ya se usó en otra operación.', 1;

            SELECT m.ProductoId, m.ProductoNombre, m.TipoMovimiento, m.StockAnterior, m.StockNuevo,
                   CAST(1 AS BIT) AS Duplicado
            FROM dbo.MovimientosInventario m WHERE m.MovimientoId = @Existente;
            COMMIT TRANSACTION;
            RETURN;
        END;

        DECLARE @Nombre NVARCHAR(150), @StockAnterior INT, @StockNuevo BIGINT;
        SELECT @Nombre = Nombre, @StockAnterior = Stock
        FROM dbo.Productos WITH (UPDLOCK, HOLDLOCK)
        WHERE ProductoId = @ProductoId AND Activo = 1;

        IF @Nombre IS NULL THROW 55815, N'El producto no existe o está inactivo.', 1;

        SET @StockNuevo = CASE @TipoMovimiento
            WHEN N'Entrada' THEN CONVERT(BIGINT, @StockAnterior) + @Cantidad
            WHEN N'Salida'  THEN CONVERT(BIGINT, @StockAnterior) - @Cantidad
            ELSE @Cantidad END;

        IF @StockNuevo < 0 THROW 55816, N'No hay suficiente stock para esa salida.', 1;
        IF @StockNuevo > 2147483647 THROW 55817, N'La cantidad excede el máximo permitido.', 1;

        UPDATE dbo.Productos SET Stock = CONVERT(INT, @StockNuevo) WHERE ProductoId = @ProductoId;

        INSERT dbo.MovimientosInventario
            (ProductoId, ProductoNombre, TipoMovimiento, Cantidad, StockAnterior, StockNuevo, Motivo, UsuarioId, UsuarioNombre, FechaMovimiento)
        VALUES
            (@ProductoId, @Nombre, @TipoMovimiento, @Cantidad, @StockAnterior, CONVERT(INT, @StockNuevo),
             LEFT(CONCAT(N'[Móvil] ', ISNULL(@Motivo, N'Movimiento registrado desde la aplicación.')), 500),
             @UsuarioId, @UsuarioNombre, SYSDATETIME());

        INSERT dbo.MovilOperaciones (SyncGuid, Operacion, EntidadId, UsuarioId, Resultado)
        VALUES (@SyncGuid, N'InventarioMovimiento', CAST(SCOPE_IDENTITY() AS INT), @UsuarioId,
                CONCAT(@TipoMovimiento, N' ', @Cantidad, N' → stock ', @StockNuevo));

        COMMIT TRANSACTION;

        SELECT @ProductoId AS ProductoId, @Nombre AS ProductoNombre, @TipoMovimiento AS TipoMovimiento,
               @StockAnterior AS StockAnterior, CONVERT(INT, @StockNuevo) AS StockNuevo, CAST(0 AS BIT) AS Duplicado;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* Activar o inactivar con estado destino explicito. A diferencia de
   sp_Admin_ToggleProductStatus, repetir la orden no la invierte: un reintento
   de la cola deja el producto igual. */
CREATE OR ALTER PROCEDURE dbo.sp_Movil_Producto_CambiarEstado
    @ProductoId    INT,
    @Activo        BIT,
    @SyncGuid      UNIQUEIDENTIFIER,
    @UsuarioId     INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @SyncGuid IS NULL THROW 55820, N'Falta el identificador de sincronización.', 1;
    IF @Activo IS NULL THROW 55821, N'Indicá si el producto queda activo o inactivo.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Nombre NVARCHAR(150), @ActivoActual BIT;
        SELECT @Nombre = Nombre, @ActivoActual = Activo
        FROM dbo.Productos WITH (UPDLOCK, HOLDLOCK) WHERE ProductoId = @ProductoId;

        IF @Nombre IS NULL THROW 55822, N'El producto no existe.', 1;

        DECLARE @Cambio BIT = CASE WHEN @ActivoActual <> @Activo THEN 1 ELSE 0 END;

        IF EXISTS (SELECT 1 FROM dbo.MovilOperaciones WITH (UPDLOCK, HOLDLOCK)
                   WHERE SyncGuid = @SyncGuid AND (Operacion <> N'ProductoEstado' OR EntidadId <> @ProductoId))
            THROW 55823, N'El identificador de sincronización ya se usó en otra operación.', 1;

        IF NOT EXISTS (SELECT 1 FROM dbo.MovilOperaciones WHERE SyncGuid = @SyncGuid)
        BEGIN
            IF @Cambio = 1
                UPDATE dbo.Productos SET Activo = @Activo WHERE ProductoId = @ProductoId;

            INSERT dbo.MovilOperaciones (SyncGuid, Operacion, EntidadId, UsuarioId, Resultado)
            VALUES (@SyncGuid, N'ProductoEstado', @ProductoId, @UsuarioId, IIF(@Activo = 1, N'Activo', N'Inactivo'));
        END
        ELSE SET @Cambio = 0;

        COMMIT TRANSACTION;

        SELECT @ProductoId AS ProductoId, @Nombre AS Nombre, @Activo AS Activo, @Cambio AS Cambio;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* ─────────────────────────────────────────────────────────────────────────
   5. Pedidos.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_Movil_Pedidos_Listar
    @Estado NVARCHAR(30)  = NULL,
    @Buscar NVARCHAR(150) = NULL,
    @Top    INT           = 100
AS
BEGIN
    SET NOCOUNT ON;
    SET @Estado = NULLIF(LTRIM(RTRIM(@Estado)), N'');
    SET @Buscar = NULLIF(LTRIM(RTRIM(@Buscar)), N'');
    IF @Top IS NULL OR @Top NOT BETWEEN 1 AND 300 SET @Top = 100;

    SELECT TOP (@Top)
        p.PedidoId, u.NombreCompleto AS Cliente, p.FechaPedido, p.Estado,
        ISNULL(p.TipoEntrega, N'') AS TipoEntrega, p.Total,
        ISNULL(p.VendedorNombre, N'') AS VendedorNombre, ISNULL(p.CanalPedido, N'') AS CanalPedido,
        CAST(IIF(EXISTS (SELECT 1 FROM dbo.Facturas f WHERE f.PedidoId = p.PedidoId), 1, 0) AS BIT) AS TieneFactura,
        CAST(IIF(EXISTS (SELECT 1 FROM dbo.PedidoPreparacion pp WHERE pp.PedidoId = p.PedidoId), 1, 0) AS BIT) AS Preparado
    FROM dbo.Pedidos p
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
    WHERE (@Estado IS NULL OR p.Estado = @Estado)
      AND (@Buscar IS NULL OR u.NombreCompleto LIKE N'%' + @Buscar + N'%'
           OR CONVERT(NVARCHAR(20), p.PedidoId) = @Buscar
           OR p.VendedorNombre LIKE N'%' + @Buscar + N'%')
    ORDER BY p.FechaPedido DESC, p.PedidoId DESC;
END;
GO

/* Cabecera con todo lo que la pantalla necesita para decidir que acciones
   ofrecer, y las lineas del pedido (mismo procedimiento que el web). */
CREATE OR ALTER PROCEDURE dbo.sp_Movil_Pedido_Detalle
    @PedidoId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.PedidoId, u.NombreCompleto AS Cliente, u.Correo AS ClienteCorreo, ISNULL(u.Telefono, N'') AS ClienteTelefono,
        p.FechaPedido, p.Estado, ISNULL(p.TipoEntrega, N'') AS TipoEntrega,
        ISNULL(NULLIF(p.DireccionEntrega, N''), ISNULL(u.Direccion, N'')) AS DireccionEntrega,
        p.Total, ISNULL(p.Observaciones, N'') AS Observaciones,
        ISNULL(p.VendedorNombre, N'') AS VendedorNombre, ISNULL(p.CanalPedido, N'') AS CanalPedido,
        ISNULL(p.MotivoRechazo, N'') AS MotivoRechazo,
        ISNULL(f.NumeroFactura, N'') AS NumeroFactura,
        CAST(IIF(f.FacturaId IS NULL, 0, 1) AS BIT) AS TieneFactura,
        ISNULL(pp.PreparadoPorNombre, N'') AS PreparadoPorNombre, pp.FechaPreparacion,
        ISNULL(ruta.Codigo, N'') AS RutaCodigo, ISNULL(ruta.EstadoEntrega, N'') AS EstadoEntrega
    FROM dbo.Pedidos p
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
    OUTER APPLY (SELECT TOP 1 fac.FacturaId, fac.NumeroFactura FROM dbo.Facturas fac
                 WHERE fac.PedidoId = p.PedidoId ORDER BY fac.FacturaId DESC) f
    LEFT JOIN dbo.PedidoPreparacion pp ON pp.PedidoId = p.PedidoId
    OUTER APPLY (SELECT TOP 1 r.Codigo, rp.EstadoEntrega FROM dbo.RutaPedidos rp
                 INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId
                 WHERE rp.PedidoId = p.PedidoId AND r.Estado <> N'Cancelada'
                 ORDER BY r.FechaCreacion DESC) ruta
    WHERE p.PedidoId = @PedidoId;

    IF @@ROWCOUNT > 0
        EXEC dbo.sp_Admin_GetOrderDetailLines @PedidoId = @PedidoId;
END;
GO

/* Lo que bodega tiene que alistar: pedidos vigentes (no cancelados,
   rechazados, retenidos ni entregados) que todavia no se prepararon y cuya
   entrega no salio. Con @Preparados = 1 devuelve lo preparado hoy. */
CREATE OR ALTER PROCEDURE dbo.sp_Movil_Bodega_PedidosPorPreparar
    @Preparados BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (150)
        p.PedidoId, u.NombreCompleto AS Cliente, p.FechaPedido, p.Estado,
        ISNULL(p.TipoEntrega, N'') AS TipoEntrega,
        ISNULL(NULLIF(p.DireccionEntrega, N''), ISNULL(u.Direccion, N'')) AS DireccionEntrega,
        (SELECT COUNT(*) FROM dbo.PedidoDetalle d WHERE d.PedidoId = p.PedidoId AND d.PedidoComboId IS NULL)
          + (SELECT COUNT(*) FROM dbo.PedidoCombos c WHERE c.PedidoId = p.PedidoId) AS TotalLineas,
        (SELECT ISNULL(SUM(d.Cantidad), 0) FROM dbo.PedidoDetalle d WHERE d.PedidoId = p.PedidoId) AS TotalUnidades,
        ISNULL(pp.PreparadoPorNombre, N'') AS PreparadoPorNombre, pp.FechaPreparacion,
        ISNULL(ruta.Codigo, N'') AS RutaCodigo
    FROM dbo.Pedidos p
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
    LEFT JOIN dbo.PedidoPreparacion pp ON pp.PedidoId = p.PedidoId
    OUTER APPLY (SELECT TOP 1 r.Codigo FROM dbo.RutaPedidos rp
                 INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId
                 WHERE rp.PedidoId = p.PedidoId AND r.Estado IN (N'Planificada', N'Despachada')
                 ORDER BY r.FechaCreacion DESC) ruta
    WHERE p.Estado NOT IN (N'Cancelado', N'Rechazado', N'Retenido', N'Entregado')
      -- Lo que ya salio en una ruta despachada o cerrada dejo la bodega.
      AND NOT EXISTS (SELECT 1 FROM dbo.RutaPedidos rp2
                      INNER JOIN dbo.Rutas r2 ON r2.RutaId = rp2.RutaId
                      WHERE rp2.PedidoId = p.PedidoId
                        AND (rp2.EstadoEntrega IN (N'Entregado', N'Fallido')
                             OR r2.Estado IN (N'Despachada', N'Completada')))
      AND (
            (@Preparados = 0 AND pp.PedidoId IS NULL)
         OR (@Preparados = 1 AND pp.PedidoId IS NOT NULL AND CAST(pp.FechaPreparacion AS DATE) = CAST(SYSDATETIME() AS DATE))
      )
    ORDER BY CASE WHEN @Preparados = 1 THEN pp.FechaPreparacion END DESC, p.FechaPedido ASC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Movil_Bodega_MarcarPreparado
    @PedidoId       INT,
    @Observaciones  NVARCHAR(300) = NULL,
    @SyncGuid       UNIQUEIDENTIFIER,
    @UsuarioId      INT,
    @UsuarioNombre  NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @SyncGuid IS NULL THROW 55830, N'Falta el identificador de sincronización.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Estado NVARCHAR(30);
        SELECT @Estado = Estado FROM dbo.Pedidos WITH (UPDLOCK, HOLDLOCK) WHERE PedidoId = @PedidoId;
        IF @Estado IS NULL THROW 55831, N'El pedido no existe.', 1;

        IF EXISTS (SELECT 1 FROM dbo.PedidoPreparacion WITH (UPDLOCK, HOLDLOCK) WHERE PedidoId = @PedidoId)
        BEGIN
            -- Ya estaba preparado: es exactamente el resultado buscado.
            SELECT pp.PedidoId, pp.PreparadoPorNombre, pp.FechaPreparacion, CAST(1 AS BIT) AS Duplicado
            FROM dbo.PedidoPreparacion pp WHERE pp.PedidoId = @PedidoId;
            COMMIT TRANSACTION;
            RETURN;
        END;

        IF @Estado IN (N'Cancelado', N'Rechazado', N'Retenido', N'Entregado')
            THROW 55832, N'Este pedido ya no se prepara: fue cancelado, rechazado, está retenido o ya se entregó.', 1;

        IF EXISTS (SELECT 1 FROM dbo.MovilOperaciones WITH (UPDLOCK, HOLDLOCK) WHERE SyncGuid = @SyncGuid)
            THROW 55833, N'El identificador de sincronización ya se usó en otra operación.', 1;

        INSERT dbo.PedidoPreparacion (PedidoId, PreparadoPorUsuarioId, PreparadoPorNombre, Observaciones, SyncGuid)
        VALUES (@PedidoId, @UsuarioId, @UsuarioNombre, NULLIF(LTRIM(RTRIM(@Observaciones)), N''), @SyncGuid);

        INSERT dbo.MovilOperaciones (SyncGuid, Operacion, EntidadId, UsuarioId, Resultado)
        VALUES (@SyncGuid, N'PedidoPreparado', @PedidoId, @UsuarioId, N'Preparado');

        COMMIT TRANSACTION;

        SELECT pp.PedidoId, pp.PreparadoPorNombre, pp.FechaPreparacion, CAST(0 AS BIT) AS Duplicado
        FROM dbo.PedidoPreparacion pp WHERE pp.PedidoId = @PedidoId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* ─────────────────────────────────────────────────────────────────────────
   6. Rutas: reasignar chofer y vehiculo.
   El caso de uso es el chofer que se enferma a media mañana. Se permite en
   rutas Planificadas y Despachadas; las entregas ya cerradas conservan su
   historia y las pendientes pasan al chofer nuevo.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_Movil_Rutas_Reasignar
    @RutaId          INT,
    @ChoferUsuarioId INT,
    @VehiculoId      INT = NULL,
    @Motivo          NVARCHAR(200),
    @SyncGuid        UNIQUEIDENTIFIER,
    @UsuarioId       INT,
    @UsuarioNombre   NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Motivo = NULLIF(LTRIM(RTRIM(@Motivo)), N'');
    IF @SyncGuid IS NULL THROW 55840, N'Falta el identificador de sincronización.', 1;
    IF @Motivo IS NULL THROW 55841, N'Indicá el motivo de la reasignación.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM dbo.MovilOperaciones WITH (UPDLOCK, HOLDLOCK)
                   WHERE SyncGuid = @SyncGuid AND Operacion = N'RutaReasignada' AND EntidadId = @RutaId)
        BEGIN
            SELECT r.RutaId, r.Codigo, ch.NombreCompleto AS Chofer, v.Placa AS VehiculoPlaca, CAST(1 AS BIT) AS Duplicado,
                   CAST(N'' AS NVARCHAR(150)) AS ChoferAnterior
            FROM dbo.Rutas r
            INNER JOIN dbo.Usuarios ch ON ch.UsuarioId = r.ChoferUsuarioId
            INNER JOIN dbo.Vehiculos v ON v.VehiculoId = r.VehiculoId
            WHERE r.RutaId = @RutaId;
            COMMIT TRANSACTION;
            RETURN;
        END;

        IF EXISTS (SELECT 1 FROM dbo.MovilOperaciones WHERE SyncGuid = @SyncGuid)
            THROW 55842, N'El identificador de sincronización ya se usó en otra operación.', 1;

        DECLARE @Estado NVARCHAR(20), @ChoferActual INT, @VehiculoActual INT;
        SELECT @Estado = Estado, @ChoferActual = ChoferUsuarioId, @VehiculoActual = VehiculoId
        FROM dbo.Rutas WITH (UPDLOCK, HOLDLOCK) WHERE RutaId = @RutaId;

        IF @Estado IS NULL THROW 55843, N'La ruta no existe.', 1;
        IF @Estado NOT IN (N'Planificada', N'Despachada')
            THROW 55844, N'Solo se pueden reasignar rutas planificadas o despachadas.', 1;

        SET @VehiculoId = ISNULL(@VehiculoId, @VehiculoActual);

        DECLARE @ChoferNuevo NVARCHAR(150);
        SELECT @ChoferNuevo = u.NombreCompleto
        FROM dbo.Usuarios u INNER JOIN dbo.Perfiles pf ON pf.PerfilId = u.PerfilId
        WHERE u.UsuarioId = @ChoferUsuarioId AND u.Activo = 1 AND pf.Nombre = N'Chofer';
        IF @ChoferNuevo IS NULL THROW 55845, N'El chofer seleccionado no existe o está inactivo.', 1;

        DECLARE @Placa NVARCHAR(30);
        SELECT @Placa = Placa FROM dbo.Vehiculos WHERE VehiculoId = @VehiculoId AND Activo = 1;
        IF @Placa IS NULL THROW 55846, N'El vehículo seleccionado no existe o está inactivo.', 1;

        IF @ChoferActual = @ChoferUsuarioId AND @VehiculoActual = @VehiculoId
            THROW 55847, N'La ruta ya está asignada a ese chofer y vehículo.', 1;

        DECLARE @ChoferAnterior NVARCHAR(150) = (SELECT NombreCompleto FROM dbo.Usuarios WHERE UsuarioId = @ChoferActual);

        UPDATE dbo.Rutas
        SET ChoferUsuarioId = @ChoferUsuarioId,
            VehiculoId = @VehiculoId,
            Observaciones = LEFT(CONCAT(N'Reasignada por ', @UsuarioNombre, N': ', @Motivo,
                                        ISNULL(N' | ' + Observaciones, N'')), 300),
            FechaActualizacion = SYSDATETIME()
        WHERE RutaId = @RutaId;

        INSERT dbo.MovilOperaciones (SyncGuid, Operacion, EntidadId, UsuarioId, Resultado)
        VALUES (@SyncGuid, N'RutaReasignada', @RutaId, @UsuarioId,
                LEFT(CONCAT(@ChoferAnterior, N' → ', @ChoferNuevo, N' · ', @Placa), 200));

        COMMIT TRANSACTION;

        SELECT r.RutaId, r.Codigo, @ChoferNuevo AS Chofer, @Placa AS VehiculoPlaca, CAST(0 AS BIT) AS Duplicado,
               @ChoferAnterior AS ChoferAnterior
        FROM dbo.Rutas r WHERE r.RutaId = @RutaId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* ─────────────────────────────────────────────────────────────────────────
   7. Tablero de gestion: lo operativo que sp_Reportes_DashboardKpis no trae.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_Movil_Gestion_Operacion
    @Desde DATE = NULL,
    @Hasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @Desde IS NULL SET @Desde = CAST(GETDATE() AS DATE);
    IF @Hasta IS NULL SET @Hasta = CAST(GETDATE() AS DATE);
    IF @Desde > @Hasta BEGIN DECLARE @Tmp DATE = @Desde; SET @Desde = @Hasta; SET @Hasta = @Tmp; END;

    -- Pedidos vigentes por estado (estado actual, no del periodo).
    SELECT p.Estado, COUNT(*) AS Cantidad
    FROM dbo.Pedidos p
    WHERE p.Estado NOT IN (N'Cancelado', N'Rechazado', N'Entregado')
    GROUP BY p.Estado
    ORDER BY COUNT(*) DESC;

    -- Operacion en calle.
    SELECT
        (SELECT COUNT(*) FROM dbo.Rutas WHERE Estado = N'Planificada') AS RutasPlanificadas,
        (SELECT COUNT(*) FROM dbo.Rutas WHERE Estado = N'Despachada') AS RutasDespachadas,
        (SELECT COUNT(*) FROM dbo.RutaPedidos rp INNER JOIN dbo.Rutas r ON r.RutaId = rp.RutaId
         WHERE r.Estado IN (N'Planificada', N'Despachada') AND rp.EstadoEntrega IN (N'Pendiente', N'EnRuta')) AS EntregasPendientes,
        (SELECT COUNT(*) FROM dbo.RutaPedidos WHERE EstadoEntrega = N'Entregado'
         AND CAST(FechaEntrega AS DATE) BETWEEN @Desde AND @Hasta) AS EntregasCompletadas,
        (SELECT COUNT(*) FROM dbo.RutaPedidos WHERE EstadoEntrega = N'Fallido'
         AND CAST(FechaEntrega AS DATE) BETWEEN @Desde AND @Hasta) AS EntregasFallidas,
        (SELECT COUNT(*) FROM dbo.Pedidos WHERE Estado = N'Retenido') AS PedidosRetenidos;

    -- Productos mas vendidos del periodo (facturado).
    SELECT TOP 5 d.ProductoId, d.ProductoNombre, SUM(d.Cantidad) AS Unidades,
           CONVERT(DECIMAL(18,2), SUM(d.Cantidad * d.PrecioUnitario)) AS Monto
    FROM dbo.FacturaDetalle d
    INNER JOIN dbo.Facturas f ON f.FacturaId = d.FacturaId
    WHERE f.Estado = N'Generada' AND CAST(f.FechaFactura AS DATE) BETWEEN @Desde AND @Hasta
    GROUP BY d.ProductoId, d.ProductoNombre
    ORDER BY SUM(d.Cantidad) DESC;

    -- Existencias en riesgo.
    SELECT TOP 8 p.ProductoId, p.Nombre, p.Stock, CONVERT(NVARCHAR(10), p.EstadoStock) AS EstadoStock
    FROM dbo.Productos p
    WHERE p.Activo = 1 AND p.EstadoStock <> 'Normal'
    ORDER BY p.Stock ASC, p.Nombre;
END;
GO

IF XACT_STATE()<>1 THROW 55806,N'La transacción 0028 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 55807,N'SHA-256 inválido para 0028.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0028_mobile_roles_surface',N'0028_mobile_roles_surface.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'App movil por rol: permisos minimos por perfil, PEDIDOS_PREPARAR, MovilOperaciones (idempotencia), PedidoPreparacion y procedimientos sp_Movil_*. Aditiva.');
COMMIT TRANSACTION;
GO
