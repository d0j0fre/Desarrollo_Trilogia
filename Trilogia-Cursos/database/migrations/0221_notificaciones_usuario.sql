SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* BUG-08-01 · CU-081-04 y BUG-10-08 · CU-101-03: al asignar o reasignar el
   chofer de una ruta, el chofer no recibe ninguna notificacion. No existia
   ningun lugar donde guardar un aviso para un usuario: ni campana en la web
   ni bandeja en la aplicacion.

   1. dbo.NotificacionesUsuario: avisos por usuario, con leida/no leida.
   2. TR_Rutas_NotificarChofer (AFTER INSERT, UPDATE en dbo.Rutas): cuando una
      ruta activa recibe chofer (al crearla) o cambia de chofer (reasignacion),
      deja un aviso para el chofer nuevo. Va en un disparador para cubrir
      todos los caminos que asignan chofer (sitio web, aplicacion y lo que se
      agregue despues) sin modificar sp_Rutas_Create ni sp_Movil_Rutas_Reasignar.
      No usa OUTPUT ni @@IDENTITY de dbo.Rutas: ningun procedimiento existente
      depende de eso (verificado al escribir esta migracion).
   3. Procedimientos sp_Notificaciones_* para listar, contar y marcar leidas.
      Siempre filtran por @UsuarioId: nadie lee avisos ajenos.

   Aditiva. Rollback en 0221_notificaciones_usuario.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 56220,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0221_notificaciones_usuario' AND Status=N'Applied')
    THROW 56221,N'0221 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.Usuarios',N'U') IS NULL OR OBJECT_ID(N'dbo.Rutas',N'U') IS NULL
    THROW 56222,N'Faltan dbo.Usuarios o dbo.Rutas (CU-081).',1;

BEGIN TRANSACTION;
GO

/* ─────────────────────────────────────────────────────────────────────────
   1. Avisos por usuario.
   ───────────────────────────────────────────────────────────────────────── */
IF OBJECT_ID(N'dbo.NotificacionesUsuario',N'U') IS NULL
    CREATE TABLE dbo.NotificacionesUsuario
    (
        NotificacionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificacionesUsuario PRIMARY KEY,
        UsuarioId      INT            NOT NULL CONSTRAINT FK_NotificacionesUsuario_Usuarios REFERENCES dbo.Usuarios(UsuarioId),
        Tipo           NVARCHAR(40)   NOT NULL,
        Titulo         NVARCHAR(150)  NOT NULL,
        Mensaje        NVARCHAR(400)  NOT NULL,
        EntidadTipo    NVARCHAR(30)   NULL,
        EntidadId      INT            NULL,
        Leida          BIT            NOT NULL CONSTRAINT DF_NotificacionesUsuario_Leida DEFAULT 0,
        FechaCreacion  DATETIME2(0)   NOT NULL CONSTRAINT DF_NotificacionesUsuario_Fecha DEFAULT SYSDATETIME(),
        FechaLectura   DATETIME2(0)   NULL
    );
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_NotificacionesUsuario_Usuario' AND object_id = OBJECT_ID(N'dbo.NotificacionesUsuario'))
    CREATE INDEX IX_NotificacionesUsuario_Usuario ON dbo.NotificacionesUsuario (UsuarioId, Leida, NotificacionId DESC);
GO

/* ─────────────────────────────────────────────────────────────────────────
   2. Aviso al chofer cuando se le asigna una ruta.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER TRIGGER dbo.TR_Rutas_NotificarChofer
ON dbo.Rutas
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM inserted) RETURN;
    IF EXISTS (SELECT 1 FROM deleted) AND NOT UPDATE(ChoferUsuarioId) RETURN;

    INSERT dbo.NotificacionesUsuario (UsuarioId, Tipo, Titulo, Mensaje, EntidadTipo, EntidadId)
    SELECT i.ChoferUsuarioId,
           CASE WHEN d.RutaId IS NULL THEN N'RutaAsignada' ELSE N'RutaReasignada' END,
           CASE WHEN d.RutaId IS NULL THEN N'Nueva ruta asignada' ELSE N'Te reasignaron una ruta' END,
           LEFT(CONCAT(N'Ruta ', i.Codigo, N' · ', i.Zona, N' (', i.Estado, N'). ',
                       CASE WHEN d.RutaId IS NULL
                            THEN N'Ya aparece en Mis entregas y en Mis rutas de la aplicación.'
                            ELSE N'Ahora está a tu cargo: ya aparece en Mis entregas y en Mis rutas de la aplicación.' END), 400),
           N'Ruta',
           i.RutaId
    FROM inserted i
    LEFT JOIN deleted d ON d.RutaId = i.RutaId
    WHERE i.ChoferUsuarioId IS NOT NULL
      AND i.Estado IN (N'Planificada', N'Despachada')
      AND (d.RutaId IS NULL OR ISNULL(d.ChoferUsuarioId, 0) <> i.ChoferUsuarioId);
END;
GO

/* ─────────────────────────────────────────────────────────────────────────
   3. Bandeja.
   ───────────────────────────────────────────────────────────────────────── */
CREATE OR ALTER PROCEDURE dbo.sp_Notificaciones_Listar
    @UsuarioId INT,
    @Top       INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    SET @Top = CASE WHEN @Top IS NULL OR @Top < 1 THEN 50 WHEN @Top > 200 THEN 200 ELSE @Top END;

    SELECT TOP (@Top) n.NotificacionId, n.Tipo, n.Titulo, n.Mensaje, n.EntidadTipo, n.EntidadId,
           n.Leida, n.FechaCreacion
    FROM dbo.NotificacionesUsuario n
    WHERE n.UsuarioId = @UsuarioId
    ORDER BY n.NotificacionId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Notificaciones_ContarNoLeidas
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) AS NoLeidas
    FROM dbo.NotificacionesUsuario
    WHERE UsuarioId = @UsuarioId AND Leida = 0;
END;
GO

/* @NotificacionId NULL marca todas las del usuario. */
CREATE OR ALTER PROCEDURE dbo.sp_Notificaciones_MarcarLeidas
    @UsuarioId      INT,
    @NotificacionId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.NotificacionesUsuario
    SET Leida = 1, FechaLectura = SYSDATETIME()
    WHERE UsuarioId = @UsuarioId
      AND Leida = 0
      AND (@NotificacionId IS NULL OR NotificacionId = @NotificacionId);

    SELECT @@ROWCOUNT AS Marcadas;
END;
GO

IF XACT_STATE()<>1 THROW 56223,N'La transacción 0221 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 56224,N'SHA-256 inválido para 0221.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0221_notificaciones_usuario',N'0221_notificaciones_usuario.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'BUG-08-01 y BUG-10-08: NotificacionesUsuario, TR_Rutas_NotificarChofer y sp_Notificaciones_*. Aditiva.');
COMMIT TRANSACTION;
GO
