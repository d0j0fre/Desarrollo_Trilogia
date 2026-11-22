SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* POST-qa1 observacion 2 (pruebas post instalacion en Azure):
   «Total comprado» del cliente (lista de Clientes, detalle del cliente y Portal
   de clientes) sumaba los pedidos Rechazados. Solo excluia los Cancelados, asi
   que un cliente con pedidos rechazados veia un total mayor al realmente
   comprado (ejemplo: QA-POST qa1 Cliente mostraba ₡289 500 con 2 rechazados).

   Esta migracion vuelve a crear dbo.sp_Admin_GetClients y dbo.sp_Admin_GetClientById
   (mismas columnas, mismo orden y mismos filtros que cu061_cu063_clientes_patch.sql)
   y cambia solo la suma: no cuenta pedidos Rechazados ni Cancelados.
   sp_Admin_GetClientDetail llama a sp_Admin_GetClientById, por lo que hereda el
   cambio; el Portal de clientes usa el mismo dato. «Total de pedidos» (conteo)
   no cambia.

   Aditiva, sin cambios de tablas ni de datos. Rollback en
   0321_total_comprado_sin_rechazados.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56330,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0321_total_comprado_sin_rechazados' AND Status=N'Applied')
    THROW 56331,N'0321 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.sp_Admin_GetClientDetail',N'P') IS NULL THROW 56332,N'Falta dbo.sp_Admin_GetClientDetail (cu061).',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetClients
    @Buscar NVARCHAR(200) = NULL,
    @Estado NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ClientePerfilId INT = (SELECT TOP 1 PerfilId FROM dbo.Perfiles WHERE Nombre = 'Cliente');
    SET @Buscar = NULLIF(LTRIM(RTRIM(ISNULL(@Buscar, ''))), '');
    SET @Estado = NULLIF(LTRIM(RTRIM(ISNULL(@Estado, ''))), '');

    SELECT
        u.UsuarioId,
        u.NombreCompleto,
        u.Correo,
        u.Telefono,
        u.Direccion,
        CAST(ISNULL(u.Activo, 1) AS BIT) AS Activo,
        u.FechaRegistro,
        COUNT(p.PedidoId) AS TotalPedidos,
        ISNULL(SUM(CASE WHEN ISNULL(p.Estado, '') NOT IN (N'Cancelado', N'Rechazado') THEN ISNULL(p.Total, 0) ELSE 0 END), 0) AS TotalComprado,
        MAX(p.FechaPedido) AS UltimoPedido,
        u.MotivoInactivacion,
        u.FechaInactivacion
    FROM dbo.Usuarios u
    LEFT JOIN dbo.Pedidos p
        ON p.UsuarioId = u.UsuarioId
    WHERE u.PerfilId = @ClientePerfilId
      AND (
            @Buscar IS NULL
            OR u.NombreCompleto LIKE '%' + @Buscar + '%'
            OR u.Correo LIKE '%' + @Buscar + '%'
            OR ISNULL(u.Telefono, '') LIKE '%' + @Buscar + '%'
            OR ISNULL(u.Direccion, '') LIKE '%' + @Buscar + '%'
      )
      AND (
            @Estado IS NULL
            OR (@Estado = 'Activo' AND ISNULL(u.Activo, 1) = 1)
            OR (@Estado = 'Inactivo' AND ISNULL(u.Activo, 1) = 0)
      )
    GROUP BY
        u.UsuarioId,
        u.NombreCompleto,
        u.Correo,
        u.Telefono,
        u.Direccion,
        u.Activo,
        u.FechaRegistro,
        u.MotivoInactivacion,
        u.FechaInactivacion
    ORDER BY u.FechaRegistro DESC, u.UsuarioId DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Admin_GetClientById
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ClientePerfilId INT = (SELECT TOP 1 PerfilId FROM dbo.Perfiles WHERE Nombre = 'Cliente');

    SELECT TOP 1
        u.UsuarioId,
        u.NombreCompleto,
        u.Correo,
        u.Telefono,
        u.Direccion,
        CAST(ISNULL(u.Activo, 1) AS BIT) AS Activo,
        u.FechaRegistro,
        COUNT(p.PedidoId) AS TotalPedidos,
        ISNULL(SUM(CASE WHEN ISNULL(p.Estado, '') NOT IN (N'Cancelado', N'Rechazado') THEN ISNULL(p.Total, 0) ELSE 0 END), 0) AS TotalComprado,
        MAX(p.FechaPedido) AS UltimoPedido,
        u.MotivoInactivacion,
        u.FechaInactivacion
    FROM dbo.Usuarios u
    LEFT JOIN dbo.Pedidos p
        ON p.UsuarioId = u.UsuarioId
    WHERE u.UsuarioId = @UsuarioId
      AND u.PerfilId = @ClientePerfilId
    GROUP BY
        u.UsuarioId,
        u.NombreCompleto,
        u.Correo,
        u.Telefono,
        u.Direccion,
        u.Activo,
        u.FechaRegistro,
        u.MotivoInactivacion,
        u.FechaInactivacion;
END
GO

IF XACT_STATE()<>1 THROW 56333,N'La transacción 0321 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56334,N'SHA-256 inválido para 0321.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0321_total_comprado_sin_rechazados',N'0321_total_comprado_sin_rechazados.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'POST-qa1 obs. 2: Total comprado del cliente no cuenta pedidos Rechazados ni Cancelados (sp_Admin_GetClients y sp_Admin_GetClientById). Aditiva.');
COMMIT TRANSACTION;
GO
