SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-16-03 · CU-163-02: al devolver un equipo con avería (destino
   Mantenimiento) el activo queda en Mantenimiento pero no se genera ninguna
   alerta al área de taller o cobros.

   sp_Comodato_Devolver, dentro de la misma transacción, deja un aviso
   "ComodatoAveria" en la bandeja de avisos (dbo.NotificacionesUsuario, 0221)
   cuando el destino es Mantenimiento. Destinatarios (usuarios activos):
     - Taller: perfiles con FLOTA_MANTENIMIENTO o ACTIVOS_GESTIONAR
       (hoy Administrador y Gerente).
     - Cobros: perfil "Crédito y Cobro" o perfiles con CREDITOS_MOVIMIENTOS
       (hoy Crédito y Cobro y Administrador).
   Un usuario que cumple ambas condiciones recibe un solo aviso. El aviso
   apunta al activo (EntidadTipo = 'Activo').

   El resto del procedimiento es idéntico a cu162_163_164_comodatos.sql:
   mismos parámetros, validaciones, errores y resultado (ninguno).

   Rollback en 0243_comodato_alerta_averia.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56440,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0243_comodato_alerta_averia' AND Status=N'Applied')
    THROW 56441,N'0243 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.Comodatos',N'U') IS NULL OR OBJECT_ID(N'dbo.Activos',N'U') IS NULL
    THROW 56442,N'Faltan dbo.Comodatos o dbo.Activos.',1;
IF OBJECT_ID(N'dbo.NotificacionesUsuario',N'U') IS NULL
    THROW 56443,N'Falta dbo.NotificacionesUsuario (0221).',1;

BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Comodato_Devolver
    @ComodatoId          INT,
    @Destino             NVARCHAR(20),        -- Inventario | Mantenimiento
    @CondicionDevolucion NVARCHAR(300) = NULL,
    @FechaDevolucion     DATE          = NULL,
    @UsuarioId           INT           = NULL,
    @Nombre              NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @Destino NOT IN (N'Inventario', N'Mantenimiento') THROW 53083, 'Destino inválido (Inventario o Mantenimiento).', 1;

    DECLARE @ActivoId INT, @EstadoCom NVARCHAR(20);
    SELECT @ActivoId = ActivoId, @EstadoCom = Estado FROM dbo.Comodatos WHERE ComodatoId = @ComodatoId;
    IF @ActivoId IS NULL          THROW 53084, 'No se encontró el comodato.', 1;
    IF @EstadoCom <> N'Activo'     THROW 53085, 'El comodato ya fue cerrado.', 1;

    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE dbo.Comodatos
        SET Estado = CASE WHEN @Destino = N'Mantenimiento' THEN N'Retirado' ELSE N'Devuelto' END,
            DestinoDevolucion = @Destino,
            CondicionDevolucion = NULLIF(LTRIM(RTRIM(@CondicionDevolucion)), N''),
            FechaDevolucion = ISNULL(@FechaDevolucion, CAST(SYSDATETIME() AS DATE)),
            FechaActualizacion = SYSDATETIME()
        WHERE ComodatoId = @ComodatoId;

        UPDATE dbo.Activos
        SET Estado = CASE WHEN @Destino = N'Mantenimiento' THEN N'Mantenimiento' ELSE N'Disponible' END,
            ClientePrestamo = NULL, FechaActualizacion = SYSDATETIME()
        WHERE ActivoId = @ActivoId;

        -- BUG-16-03: equipo devuelto con avería → aviso a taller y cobros.
        IF @Destino = N'Mantenimiento'
        BEGIN
            INSERT dbo.NotificacionesUsuario (UsuarioId, Tipo, Titulo, Mensaje, EntidadTipo, EntidadId)
            SELECT u.UsuarioId,
                   N'ComodatoAveria',
                   N'Equipo en comodato devuelto con avería',
                   LEFT(CONCAT(N'El equipo ', a.CodigoActivo, N' (', a.Nombre, N') prestado a ', c.ClienteNombre,
                               N' se devolvió el ', CONVERT(NVARCHAR(10), c.FechaDevolucion, 103),
                               N' con la condición: ', ISNULL(c.CondicionDevolucion, N'sin detalle'),
                               N'. Quedó en Mantenimiento (en reparación). Taller: revisar y reparar. Cobros: valorar el cargo al cliente.'), 400),
                   N'Activo',
                   a.ActivoId
            FROM dbo.Comodatos c
            INNER JOIN dbo.Activos a ON a.ActivoId = c.ActivoId
            CROSS JOIN dbo.Usuarios u
            WHERE c.ComodatoId = @ComodatoId
              AND ISNULL(u.Activo, 1) = 1
              AND (EXISTS (SELECT 1
                           FROM dbo.PerfilPermisos pp
                           INNER JOIN dbo.Permisos pe ON pe.PermisoId = pp.PermisoId
                           WHERE pp.PerfilId = u.PerfilId AND pe.Activo = 1
                             AND pe.Codigo IN (N'FLOTA_MANTENIMIENTO', N'ACTIVOS_GESTIONAR', N'CREDITOS_MOVIMIENTOS'))
                   OR EXISTS (SELECT 1 FROM dbo.Perfiles p WHERE p.PerfilId = u.PerfilId AND p.Nombre = N'Crédito y Cobro'));
        END

        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK; THROW;
    END CATCH
END;
GO

IF XACT_STATE()<>1 THROW 56444,N'La transacción 0243 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56445,N'SHA-256 inválido para 0243.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0243_comodato_alerta_averia',N'0243_comodato_alerta_averia.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-16-03: sp_Comodato_Devolver avisa a taller y cobros cuando el equipo vuelve con avería (destino Mantenimiento).');
COMMIT TRANSACTION;
GO
