SET NOCOUNT ON;
SET XACT_ABORT ON;

/*  0200 — sp_Admin_GetOrderHeader vuelve al contrato que usa la aplicación.

    Problema corregido (BUG-09-03, BUG-09-05, BUG-14-03)
    ----------------------------------------------------
    AdminDbService.GetOrderDetailInternalAsync lee el encabezado del pedido por
    posición con el contrato de cu097/cu098:
        0 PedidoId, 1 UsuarioId, 2 NombreCompleto, 3 Correo, 4 FechaPedido,
        5 Estado, 6 TipoEntrega, 7 DireccionEntrega, 8 Observaciones, 9 Total,
        10 MetodoPago, 11 EstadoPago, 12 ReferenciaPago, 13 FechaPago,
        14 MotivoRechazo.
    El baseline database/sp_admin_modulo.sql (y 00_todo_en_uno.sql) define una
    versión anterior que devuelve Total en la posición 8 y Observaciones en la 9
    y omite las columnas de pago. Como ese baseline se ejecuta después de los
    parches cu097/cu098, la base queda con la versión antigua y la lectura de la
    posición 8 como texto lanza InvalidCastException. Por eso fallan el detalle
    de pedido en Administración, la confirmación de Venta móvil y el detalle de
    pedido del portal del cliente.

    Compatibilidad
    --------------
    - CREATE OR ALTER conserva object_id y los GRANT EXECUTE existentes.
    - La definición es la de database/cu098_retencion_autorizacion_pedidos.sql.
    - No modifica tablas ni datos de negocio.
*/

IF OBJECT_ID(N'dbo.SchemaMigrationHistory', N'U') IS NULL
    THROW 55700, N'Falta el ledger 0001.', 1;

IF EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
           WHERE MigrationId = N'0200_order_header_contract'
             AND Status = N'Applied')
    THROW 55701, N'0200 ya figura aplicada.', 1;

IF OBJECT_ID(N'dbo.Pedidos', N'U') IS NULL OR OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
    THROW 55702, N'Faltan las tablas dbo.Pedidos o dbo.Usuarios.', 1;

IF COL_LENGTH(N'dbo.Pedidos', N'MetodoPago') IS NULL
   OR COL_LENGTH(N'dbo.Pedidos', N'EstadoPago') IS NULL
   OR COL_LENGTH(N'dbo.Pedidos', N'ReferenciaPago') IS NULL
   OR COL_LENGTH(N'dbo.Pedidos', N'FechaPago') IS NULL
   OR COL_LENGTH(N'dbo.Pedidos', N'MotivoRechazo') IS NULL
    THROW 55703, N'dbo.Pedidos no tiene las columnas de pago y retención (cu097/cu098).', 1;

BEGIN TRANSACTION;

DECLARE @definicion NVARCHAR(MAX) = N'
CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetOrderHeader
    @PedidoId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.PedidoId,
        p.UsuarioId,
        u.NombreCompleto,
        u.Correo,
        p.FechaPedido,
        p.Estado,
        p.TipoEntrega,
        p.DireccionEntrega,
        p.Observaciones,
        p.Total,
        ISNULL(p.MetodoPago, N'''') AS MetodoPago,
        ISNULL(p.EstadoPago, N'''') AS EstadoPago,
        p.ReferenciaPago,
        p.FechaPago,
        p.MotivoRechazo
    FROM dbo.Pedidos p
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
    WHERE p.PedidoId = @PedidoId;
END';

EXEC sys.sp_executesql @definicion;

/* Validación posterior: el orden de columnas debe ser el que lee la aplicación. */
IF NOT EXISTS (SELECT 1 FROM sys.dm_exec_describe_first_result_set_for_object(OBJECT_ID(N'dbo.sp_Admin_GetOrderHeader'), 0)
               WHERE column_ordinal = 9 AND name = N'Observaciones')
   OR NOT EXISTS (SELECT 1 FROM sys.dm_exec_describe_first_result_set_for_object(OBJECT_ID(N'dbo.sp_Admin_GetOrderHeader'), 0)
                  WHERE column_ordinal = 10 AND name = N'Total')
   OR (SELECT COUNT(*) FROM sys.dm_exec_describe_first_result_set_for_object(OBJECT_ID(N'dbo.sp_Admin_GetOrderHeader'), 0)) <> 15
    THROW 55706, N'sp_Admin_GetOrderHeader no quedó con el contrato esperado.', 1;

IF XACT_STATE() <> 1
    THROW 55704, N'La transacción 0200 no está disponible.', 1;

DECLARE @MigrationSha256 NVARCHAR(128) = N'$(MigrationSha256)';
IF LEN(@MigrationSha256) <> 64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%'
    THROW 55705, N'SHA-256 inválido para 0200.', 1;

INSERT dbo.SchemaMigrationHistory
    (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
VALUES
    (N'0200_order_header_contract',
     N'0200_order_header_contract.sql',
     UPPER(@MigrationSha256),
     N'Applied',
     ORIGINAL_LOGIN(),
     DB_NAME(),
     N'sp_Admin_GetOrderHeader devuelve Observaciones, Total, pago y MotivoRechazo en el orden que lee la aplicación (BUG-09-03, BUG-09-05, BUG-14-03).');

COMMIT TRANSACTION;
