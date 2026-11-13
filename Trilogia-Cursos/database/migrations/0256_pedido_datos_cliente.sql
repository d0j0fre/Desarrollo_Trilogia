SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* G3-15: en el pago de la Tienda, el cliente indica tipo de cliente (fisico o
   juridico), si quiere factura electronica, el correo para el comprobante y dos
   telefonos. Esos datos solo iban en el correo de confirmacion; el pedido no los
   guardaba y quien factura no los tenia.

   Se agrega dbo.PedidoDatosCliente (uno por pedido) y dos procedimientos:
   - sp_Pedido_GuardarDatosCliente: lo llama la Tienda despues de crear el pedido;
     solo acepta pedidos del mismo cliente y es idempotente (vuelve a guardar los
     mismos datos si se repite).
   - sp_Pedido_GetDatosCliente: lectura para el detalle del pedido.
   El pais ya va en Pedidos.DireccionEntrega (la Tienda arma «Pais, Provincia,
   Canton, Distrito. Senas»). No cambia dbo.Pedidos ni
   sp_Store_CreateOrderWithPromotions, y no toca pedidos ya registrados (quedan
   sin datos de cliente).

   Aditiva. Rollback en 0256_pedido_datos_cliente.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56560,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0256_pedido_datos_cliente' AND Status=N'Applied')
    THROW 56561,N'0256 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.Pedidos',N'U') IS NULL THROW 56562,N'Falta dbo.Pedidos.',1;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.PedidoDatosCliente',N'U') IS NULL
    CREATE TABLE dbo.PedidoDatosCliente
    (
        PedidoId            INT           NOT NULL CONSTRAINT PK_PedidoDatosCliente PRIMARY KEY
                                          CONSTRAINT FK_PedidoDatosCliente_Pedidos REFERENCES dbo.Pedidos(PedidoId),
        TipoCliente         NVARCHAR(30)  NOT NULL CONSTRAINT CK_PedidoDatosCliente_Tipo CHECK (TipoCliente IN (N'Cliente Físico', N'Cliente Jurídico')),
        FacturaElectronica  BIT           NOT NULL CONSTRAINT DF_PedidoDatosCliente_Factura DEFAULT (0),
        CorreoComprobante   NVARCHAR(150) NULL,
        Telefono            NVARCHAR(30)  NULL,
        TelefonoAlterno     NVARCHAR(30)  NULL,
        FechaRegistro       DATETIME2     NOT NULL CONSTRAINT DF_PedidoDatosCliente_Fecha DEFAULT (SYSDATETIME())
    );
GO

CREATE OR ALTER PROCEDURE dbo.sp_Pedido_GuardarDatosCliente
    @PedidoId           INT,
    @UsuarioId          INT,
    @TipoCliente        NVARCHAR(30),
    @FacturaElectronica BIT,
    @CorreoComprobante  NVARCHAR(150) = NULL,
    @Telefono           NVARCHAR(30)  = NULL,
    @TelefonoAlterno    NVARCHAR(30)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Pedidos WHERE PedidoId = @PedidoId AND UsuarioId = @UsuarioId)
        THROW 53140, N'El pedido no existe o no pertenece al cliente.', 1;
    IF @TipoCliente IS NULL OR @TipoCliente NOT IN (N'Cliente Físico', N'Cliente Jurídico')
        THROW 53141, N'El tipo de cliente no es válido.', 1;

    MERGE dbo.PedidoDatosCliente WITH (HOLDLOCK) AS destino
    USING (SELECT @PedidoId AS PedidoId) AS origen ON destino.PedidoId = origen.PedidoId
    WHEN MATCHED THEN UPDATE SET
        TipoCliente = @TipoCliente,
        FacturaElectronica = ISNULL(@FacturaElectronica, 0),
        CorreoComprobante = NULLIF(LTRIM(RTRIM(@CorreoComprobante)), N''),
        Telefono = NULLIF(LTRIM(RTRIM(@Telefono)), N''),
        TelefonoAlterno = NULLIF(LTRIM(RTRIM(@TelefonoAlterno)), N'')
    WHEN NOT MATCHED THEN INSERT (PedidoId, TipoCliente, FacturaElectronica, CorreoComprobante, Telefono, TelefonoAlterno)
        VALUES (@PedidoId, @TipoCliente, ISNULL(@FacturaElectronica, 0),
                NULLIF(LTRIM(RTRIM(@CorreoComprobante)), N''), NULLIF(LTRIM(RTRIM(@Telefono)), N''),
                NULLIF(LTRIM(RTRIM(@TelefonoAlterno)), N''));
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Pedido_GetDatosCliente
    @PedidoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PedidoId, TipoCliente, FacturaElectronica, CorreoComprobante, Telefono, TelefonoAlterno, FechaRegistro
    FROM dbo.PedidoDatosCliente
    WHERE PedidoId = @PedidoId;
END;
GO

IF XACT_STATE()<>1 THROW 56563,N'La transacción 0256 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56564,N'SHA-256 inválido para 0256.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0256_pedido_datos_cliente',N'0256_pedido_datos_cliente.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'G3-15: dbo.PedidoDatosCliente y sp_Pedido_GuardarDatosCliente / sp_Pedido_GetDatosCliente (tipo de cliente, factura electrónica, correo y teléfonos del pago). Aditiva.');
COMMIT TRANSACTION;
GO
