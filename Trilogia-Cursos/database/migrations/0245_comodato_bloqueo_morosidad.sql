SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-16-02 · CU-162-03: el comodato no se bloquea por morosidad; el cliente
   se escribe como texto libre y no se valida su deuda.

   1. dbo.fn_Comodato_DeudaVencida(@DiasPlazo): deuda vencida por cliente,
      calculada SOLO con datos que ya existen (cuentas por cobrar del módulo
      de créditos, dbo.ClienteCreditoMovimientos; sus cargos referencian las
      facturas a crédito). No modifica ni reemplaza procedimientos de crédito
      ni de clientes.
        Débitos  = Cargo + AjustePositivo; créditos = Abono + AjusteNegativo.
        Los créditos se aplican a los débitos más antiguos primero (FIFO).
        SaldoActual  = débitos sin cubrir (igual a DeudaActual de
                       sp_Admin_GetClientCredits).
        SaldoVencido = parte sin cubrir de los débitos con más de @DiasPlazo
                       días (DATEDIFF(DAY, FechaMovimiento, hoy) > 30).
        Moroso       = SaldoVencido > 0.
      PENDIENTE DE ALINEAR: la sesión de crédito (BUG-06-02) define la regla
      oficial de deuda vencida; al unir, basta cambiar el cuerpo de esta
      función (mismas columnas) y el resto sigue igual.
   2. sp_Comodato_ClientesParaAsignar: clientes activos (perfil Cliente) para
      la lista del formulario, con su saldo vencido y la última
      identificación que usaron en un pedido.
   3. sp_Comodato_EvaluarMorosidad: la evaluación de un cliente.
   4. sp_Comodato_Asignar exige un cliente registrado y activo (53087),
      toma su nombre de dbo.Usuarios y rechaza la asignación si es moroso
      (53086). Los demás parámetros, validaciones y el resultado no cambian;
      @ClienteNombre se conserva por compatibilidad y se ignora.

   Rollback en 0245_comodato_bloqueo_morosidad.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56460,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0245_comodato_bloqueo_morosidad' AND Status=N'Applied')
    THROW 56461,N'0245 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.Comodatos',N'U') IS NULL OR OBJECT_ID(N'dbo.ClienteCreditoMovimientos',N'U') IS NULL
    THROW 56462,N'Faltan dbo.Comodatos o dbo.ClienteCreditoMovimientos (cu062).',1;
IF NOT EXISTS (SELECT 1 FROM dbo.Perfiles WHERE Nombre = N'Cliente')
    THROW 56463,N'Falta el perfil Cliente.',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER FUNCTION dbo.fn_Comodato_DeudaVencida (@DiasPlazo INT)
RETURNS TABLE
AS
RETURN
    WITH Creditos AS (
        SELECT UsuarioId, SUM(Monto) AS Abonado
        FROM dbo.ClienteCreditoMovimientos
        WHERE TipoMovimiento IN (N'Abono', N'AjusteNegativo')
        GROUP BY UsuarioId
    ),
    Debitos AS (
        SELECT UsuarioId, Monto, FechaMovimiento,
               SUM(Monto) OVER (PARTITION BY UsuarioId ORDER BY FechaMovimiento, CreditoMovimientoId
                                ROWS UNBOUNDED PRECEDING) AS Acumulado
        FROM dbo.ClienteCreditoMovimientos
        WHERE TipoMovimiento IN (N'Cargo', N'AjustePositivo')
    ),
    Pendientes AS (
        SELECT d.UsuarioId, d.FechaMovimiento,
               CASE WHEN d.Acumulado <= ISNULL(c.Abonado, 0) THEN 0
                    WHEN d.Acumulado - d.Monto >= ISNULL(c.Abonado, 0) THEN d.Monto
                    ELSE d.Acumulado - ISNULL(c.Abonado, 0) END AS Pendiente
        FROM Debitos d
        LEFT JOIN Creditos c ON c.UsuarioId = d.UsuarioId
    )
    SELECT UsuarioId,
           CAST(SUM(Pendiente) AS DECIMAL(18,2)) AS SaldoActual,
           CAST(SUM(CASE WHEN DATEDIFF(DAY, FechaMovimiento, SYSDATETIME()) > @DiasPlazo THEN Pendiente ELSE 0 END) AS DECIMAL(18,2)) AS SaldoVencido,
           MIN(CASE WHEN Pendiente > 0 AND DATEDIFF(DAY, FechaMovimiento, SYSDATETIME()) > @DiasPlazo
                    THEN CAST(FechaMovimiento AS DATE) END) AS CargoVencidoMasAntiguo
    FROM Pendientes
    GROUP BY UsuarioId;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Comodato_ClientesParaAsignar
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UsuarioId, u.NombreCompleto, ISNULL(u.Correo, N'') AS Correo, ISNULL(u.Direccion, N'') AS Direccion,
           ISNULL(ident.IdentificacionCliente, N'') AS Identificacion,
           ISNULL(d.SaldoVencido, 0) AS SaldoVencido,
           ISNULL(DATEDIFF(DAY, d.CargoVencidoMasAntiguo, CAST(SYSDATETIME() AS DATE)), 0) AS DiasAtraso
    FROM dbo.Usuarios u
    INNER JOIN dbo.Perfiles p ON p.PerfilId = u.PerfilId AND p.Nombre = N'Cliente'
    LEFT JOIN dbo.fn_Comodato_DeudaVencida(30) d ON d.UsuarioId = u.UsuarioId
    OUTER APPLY (SELECT TOP 1 pe.IdentificacionCliente
                 FROM dbo.Pedidos pe
                 WHERE pe.UsuarioId = u.UsuarioId AND NULLIF(LTRIM(RTRIM(pe.IdentificacionCliente)), N'') IS NOT NULL
                 ORDER BY pe.FechaPedido DESC) ident
    WHERE ISNULL(u.Activo, 1) = 1
    ORDER BY u.NombreCompleto;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Comodato_EvaluarMorosidad
    @ClienteUsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(d.SaldoActual, 0) AS SaldoActual,
           ISNULL(d.SaldoVencido, 0) AS SaldoVencido,
           d.CargoVencidoMasAntiguo,
           ISNULL(DATEDIFF(DAY, d.CargoVencidoMasAntiguo, CAST(SYSDATETIME() AS DATE)), 0) AS DiasAtraso,
           CAST(CASE WHEN ISNULL(d.SaldoVencido, 0) > 0 THEN 1 ELSE 0 END AS BIT) AS EsMoroso
    FROM (SELECT @ClienteUsuarioId AS UsuarioId) x
    LEFT JOIN dbo.fn_Comodato_DeudaVencida(30) d ON d.UsuarioId = x.UsuarioId;
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

    -- BUG-16-02: bloqueo por morosidad (saldo vencido en cuentas por cobrar).
    IF EXISTS (SELECT 1 FROM dbo.fn_Comodato_DeudaVencida(30) WHERE UsuarioId = @ClienteUsuarioId AND SaldoVencido > 0)
        THROW 53086, 'El cliente tiene saldo vencido: la asignación queda bloqueada hasta que salde su cuenta.', 1;

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

IF XACT_STATE()<>1 THROW 56464,N'La transacción 0245 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56465,N'SHA-256 inválido para 0245.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0245_comodato_bloqueo_morosidad',N'0245_comodato_bloqueo_morosidad.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-16-02: cliente registrado obligatorio y bloqueo del comodato por saldo vencido (fn_Comodato_DeudaVencida).');
COMMIT TRANSACTION;
GO
