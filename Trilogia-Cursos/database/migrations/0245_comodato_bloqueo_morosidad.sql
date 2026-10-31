SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-16-02 · CU-162-03: el comodato no se bloquea por morosidad; el cliente
   se escribe como texto libre y no se valida su deuda.

   La regla de deuda vencida es la del módulo de crédito (BUG-06-02,
   migración 0230): dbo.fn_Cliente_EstadoDeuda(@UsuarioId). Cada cargo vence
   a los PlazoCreditoDias días (30 por defecto), los abonos pagan primero los
   cargos más antiguos y hay deuda vencida si queda saldo sin pagar de cargos
   ya vencidos (TieneDeudaVencida = 1). Esta migración no la redefine: la
   exige aplicada.

   1. sp_Comodato_ClientesParaAsignar: clientes activos (perfil Cliente,
      dbo.Usuarios.UsuarioId, el mismo identificador que recibe
      fn_Cliente_EstadoDeuda) para la lista del formulario, con su deuda
      vencida, días de atraso y la última identificación que usaron en un
      pedido.
   2. sp_Comodato_Asignar exige un cliente registrado y activo (53087), toma
      su nombre de dbo.Usuarios y rechaza la asignación si el cliente tiene
      deuda vencida (53086, con el monto y los días de atraso en el mensaje).
      Los demás parámetros, validaciones y el resultado no cambian;
      @ClienteNombre se conserva por compatibilidad y se ignora.
   La aplicación lee la deuda de un cliente con dbo.sp_Cliente_EstadoDeuda (0230).

   Requiere 0230_cliente_estado_deuda (rama correcciones-esteban-carrito).
   Rollback en 0245_comodato_bloqueo_morosidad.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56460,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0245_comodato_bloqueo_morosidad' AND Status=N'Applied')
    THROW 56461,N'0245 ya figura aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0230_cliente_estado_deuda' AND Status=N'Applied')
    THROW 56462,N'Falta 0230_cliente_estado_deuda (regla de deuda vencida, BUG-06-02): aplicarla antes.',1;
IF OBJECT_ID(N'dbo.fn_Cliente_EstadoDeuda',N'IF') IS NULL OR OBJECT_ID(N'dbo.sp_Cliente_EstadoDeuda',N'P') IS NULL
    THROW 56463,N'Faltan dbo.fn_Cliente_EstadoDeuda o dbo.sp_Cliente_EstadoDeuda (0230).',1;
IF OBJECT_ID(N'dbo.Comodatos',N'U') IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.Perfiles WHERE Nombre = N'Cliente')
    THROW 56464,N'Faltan dbo.Comodatos o el perfil Cliente.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Comodato_ClientesParaAsignar
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UsuarioId, u.NombreCompleto, ISNULL(u.Correo, N'') AS Correo, ISNULL(u.Direccion, N'') AS Direccion,
           ISNULL(ident.IdentificacionCliente, N'') AS Identificacion,
           ISNULL(d.DeudaVencida, 0) AS DeudaVencida,
           ISNULL(d.DiasAtraso, 0) AS DiasAtraso
    FROM dbo.Usuarios u
    INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId AND p.Nombre = N'Cliente'
    OUTER APPLY dbo.fn_Cliente_EstadoDeuda(u.UsuarioId) d
    OUTER APPLY (SELECT TOP 1 pe.IdentificacionCliente
                 FROM dbo.Pedidos pe
                 WHERE pe.UsuarioId = u.UsuarioId AND NULLIF(LTRIM(RTRIM(pe.IdentificacionCliente)), N'') IS NOT NULL
                 ORDER BY pe.FechaPedido DESC) ident
    WHERE ISNULL(u.Activo, 1) = 1
    ORDER BY u.NombreCompleto;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Comodato_Asignar
    @ActivoId              INT,
    @ClienteUsuarioId      INT           = NULL,
    @ClienteNombre         NVARCHAR(150) = NULL,   -- compatibilidad: se toma de dbo.Usuarios
    @ClienteIdentificacion NVARCHAR(50)  = NULL,
    @Ubicacion             NVARCHAR(200) = NULL,
    @FechaAsignacion       DATE          = NULL,
    @CondicionEntrega      NVARCHAR(300) = NULL,
    @Observaciones         NVARCHAR(300) = NULL,
    @UsuarioId             INT           = NULL,
    @Nombre                NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    -- BUG-16-02: el cliente debe ser un cliente registrado y activo.
    SET @ClienteNombre = (SELECT NULLIF(LTRIM(RTRIM(u.NombreCompleto)), N'')
                          FROM dbo.Usuarios u
                          INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId AND p.Nombre = N'Cliente'
                          WHERE u.UsuarioId = @ClienteUsuarioId AND ISNULL(u.Activo, 1) = 1);
    IF @ClienteNombre IS NULL THROW 53087, 'Debe seleccionar un cliente registrado y activo.', 1;

    DECLARE @Estado NVARCHAR(20) = (SELECT Estado FROM dbo.Activos WHERE ActivoId = @ActivoId AND Activo = 1);
    IF @Estado IS NULL           THROW 53081, 'El activo no existe o está inactivo.', 1;
    IF @Estado <> N'Disponible'  THROW 53082, 'El activo no está disponible para comodato.', 1;

    -- BUG-16-02: bloqueo por morosidad con la regla de crédito (0230).
    IF EXISTS (SELECT 1 FROM dbo.fn_Cliente_EstadoDeuda(@ClienteUsuarioId) WHERE TieneDeudaVencida = 1)
    BEGIN
        DECLARE @Mensaje NVARCHAR(400) = (
            SELECT CONCAT(N'El cliente tiene una deuda vencida de ₡', FORMAT(DeudaVencida, N'N2', N'es-CR'),
                          N' con ', DiasAtraso, N' días de atraso: la asignación queda bloqueada hasta que salde su cuenta.')
            FROM dbo.fn_Cliente_EstadoDeuda(@ClienteUsuarioId));
        THROW 53086, @Mensaje, 1;
    END

    BEGIN TRANSACTION;
    BEGIN TRY
        INSERT INTO dbo.Comodatos
            (ActivoId, ClienteUsuarioId, ClienteNombre, ClienteIdentificacion, Ubicacion, FechaAsignacion,
             CondicionEntrega, Observaciones, RegistradoPorUsuarioId, RegistradoPorNombre)
        VALUES
            (@ActivoId, @ClienteUsuarioId, @ClienteNombre, NULLIF(LTRIM(RTRIM(@ClienteIdentificacion)), N''),
             NULLIF(LTRIM(RTRIM(@Ubicacion)), N''), ISNULL(@FechaAsignacion, CAST(SYSDATETIME() AS DATE)),
             NULLIF(LTRIM(RTRIM(@CondicionEntrega)), N''), NULLIF(LTRIM(RTRIM(@Observaciones)), N''), @UsuarioId, @Nombre);

        DECLARE @Id INT = SCOPE_IDENTITY();

        UPDATE dbo.Activos
        SET Estado = N'Prestado', ClientePrestamo = @ClienteNombre, FechaActualizacion = SYSDATETIME()
        WHERE ActivoId = @ActivoId;

        COMMIT;
        SELECT @Id AS ComodatoId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK; THROW;
    END CATCH
END;
GO

IF XACT_STATE()<>1 THROW 56465,N'La transacción 0245 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56466,N'SHA-256 inválido para 0245.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0245_comodato_bloqueo_morosidad',N'0245_comodato_bloqueo_morosidad.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-16-02: cliente registrado obligatorio y bloqueo del comodato por deuda vencida (fn_Cliente_EstadoDeuda, 0230).');
COMMIT TRANSACTION;
GO
