SET NOCOUNT ON;
SET XACT_ABORT ON;

/*  0201 — teléfono del cliente de una factura para enviar el comprobante por WhatsApp.

    BUG-09-01 (CU-092 escenario 2): al facturar un pedido de un cliente con número
    registrado, el sistema ofrece enviar el comprobante por WhatsApp (enlace wa.me
    con un clic de confirmación). La aplicación necesita el teléfono del cliente,
    que vive en dbo.Usuarios y se alcanza por Facturas.PedidoId -> Pedidos.UsuarioId.

    Compatibilidad
    --------------
    - Procedimiento nuevo; no modifica tablas, datos ni otros procedimientos.
    - La aplicación tolera que no exista: sin 0201 el detalle de factura indica que
      el cliente no tiene número registrado.
*/

IF OBJECT_ID(N'dbo.SchemaMigrationHistory', N'U') IS NULL
    THROW 55720, N'Falta el ledger 0001.', 1;

IF EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory
           WHERE MigrationId = N'0201_invoice_client_phone'
             AND Status = N'Applied')
    THROW 55721, N'0201 ya figura aplicada.', 1;

IF OBJECT_ID(N'dbo.Facturas', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Pedidos', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
   OR COL_LENGTH(N'dbo.Usuarios', N'Telefono') IS NULL
    THROW 55722, N'Faltan dbo.Facturas, dbo.Pedidos o dbo.Usuarios.Telefono.', 1;

BEGIN TRANSACTION;

DECLARE @definicion NVARCHAR(MAX) = N'
CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetInvoiceClientPhone
    @FacturaId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT u.Telefono
    FROM dbo.Facturas f
    INNER JOIN dbo.Pedidos p ON p.PedidoId = f.PedidoId
    INNER JOIN dbo.Usuarios u ON u.UsuarioId = p.UsuarioId
    WHERE f.FacturaId = @FacturaId;
END';

EXEC sys.sp_executesql @definicion;

IF OBJECT_ID(N'dbo.sp_Admin_GetInvoiceClientPhone', N'P') IS NULL
   OR NOT EXISTS (SELECT 1 FROM sys.parameters
                  WHERE object_id = OBJECT_ID(N'dbo.sp_Admin_GetInvoiceClientPhone')
                    AND name = N'@FacturaId')
    THROW 55726, N'sp_Admin_GetInvoiceClientPhone no quedó con la firma esperada.', 1;

IF XACT_STATE() <> 1
    THROW 55724, N'La transacción 0201 no está disponible.', 1;

DECLARE @MigrationSha256 NVARCHAR(128) = N'$(MigrationSha256)';
IF LEN(@MigrationSha256) <> 64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%'
    THROW 55725, N'SHA-256 inválido para 0201.', 1;

INSERT dbo.SchemaMigrationHistory
    (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
VALUES
    (N'0201_invoice_client_phone',
     N'0201_invoice_client_phone.sql',
     UPPER(@MigrationSha256),
     N'Applied',
     ORIGINAL_LOGIN(),
     DB_NAME(),
     N'sp_Admin_GetInvoiceClientPhone: teléfono del cliente de una factura para enviar el comprobante por WhatsApp (BUG-09-01).');

COMMIT TRANSACTION;
