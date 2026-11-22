SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/* POST-qa3-02, POST-qa3-03 y POST-qa3-04 · CU-192-01, CU-211-01 y CU-232-01
   (pruebas post instalacion en Azure): tres funciones prometen avisar a alguien
   y no avisan a nadie. Las pruebas originales las dejaron «no verificables por
   falta de buzon», pero al revisarlas la funcion no existe:

   1. Cerrar un reclamo documentando la resolucion no avisa al cliente.
   2. Definir o reemplazar la meta mensual no avisa al vendedor.
   3. Agregar a una persona a un departamento de chat no le avisa.

   Esta migracion deja un aviso en dbo.NotificacionesUsuario (campana y pagina
   Notificaciones del sitio, y bandeja de la app) con tres disparadores. Van en
   disparadores para cubrir todos los caminos que cambian esos datos sin
   modificar los procedimientos existentes (mismo patron de 0221, 0222 y 0320).
   No dependen del correo: sirven aunque el SMTP no este configurado.

   Aditiva, sin cambios de tablas ni de datos. Rollback en
   0327_avisos_reclamo_meta_departamento.rollback.md. */
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL THROW 58327,N'Falta el ledger 0001.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0327_avisos_reclamo_meta_departamento' AND Status=N'Applied')
    THROW 58328,N'0327 ya figura aplicada.',1;
IF OBJECT_ID(N'dbo.NotificacionesUsuario',N'U') IS NULL THROW 58329,N'Falta dbo.NotificacionesUsuario (migracion 0221).',1;
IF OBJECT_ID(N'dbo.Reclamos',N'U') IS NULL OR OBJECT_ID(N'dbo.MetasVendedor',N'U') IS NULL OR OBJECT_ID(N'dbo.ChatDepartamentoMiembros',N'U') IS NULL OR OBJECT_ID(N'dbo.ChatDepartamentos',N'U') IS NULL
    THROW 58330,N'Faltan dbo.Reclamos, dbo.MetasVendedor o las tablas de departamentos de chat (CU-191, CU-211, 0003).',1;

BEGIN TRANSACTION;
GO

/* 1. Reclamo cerrado: aviso al cliente. Solo cuando pasa a Cerrado. */
CREATE OR ALTER TRIGGER dbo.TR_Reclamos_NotificarCierre
ON dbo.Reclamos
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT UPDATE(Estado) RETURN;

    INSERT dbo.NotificacionesUsuario (UsuarioId, Tipo, Titulo, Mensaje, EntidadTipo, EntidadId)
    SELECT i.UsuarioId,
           N'ReclamoCerrado',
           N'Su reclamo fue resuelto',
           LEFT(CONCAT(N'Su reclamo #', i.ReclamoId, N' («', i.Asunto, N'») quedó cerrado. Resolución: ',
                       ISNULL(NULLIF(LTRIM(RTRIM(i.ResolucionDescripcion)), N''), N'sin detalle registrado'), N'.'), 400),
           N'Reclamo',
           i.ReclamoId
    FROM inserted i
    INNER JOIN deleted d ON d.ReclamoId = i.ReclamoId
    WHERE i.Estado = N'Cerrado'
      AND ISNULL(d.Estado, N'') <> N'Cerrado';
END;
GO

/* 2. Meta de ventas: aviso al vendedor al definirla o cambiar el monto. */
CREATE OR ALTER TRIGGER dbo.TR_MetasVendedor_NotificarVendedor
ON dbo.MetasVendedor
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM inserted) RETURN;
    IF EXISTS (SELECT 1 FROM deleted) AND NOT UPDATE(MontoMeta) RETURN;

    INSERT dbo.NotificacionesUsuario (UsuarioId, Tipo, Titulo, Mensaje, EntidadTipo, EntidadId)
    SELECT i.VendedorUsuarioId,
           N'MetaAsignada',
           CASE WHEN d.MetaId IS NULL THEN N'Nueva meta de ventas' ELSE N'Su meta de ventas cambió' END,
           LEFT(CONCAT(N'Su meta del mes ', i.Mes, N'/', i.Anio, N' es de ₡',
                       FORMAT(i.MontoMeta, N'N2', N'es-CR'),
                       CASE WHEN d.MetaId IS NULL THEN N'.' ELSE CONCAT(N' (antes ₡', FORMAT(d.MontoMeta, N'N2', N'es-CR'), N').') END,
                       N' Puede ver su avance en Mi meta.'), 400),
           N'Meta',
           i.MetaId
    FROM inserted i
    LEFT JOIN deleted d ON d.MetaId = i.MetaId
    WHERE d.MetaId IS NULL OR d.MontoMeta <> i.MontoMeta;
END;
GO

/* 3. Departamento de chat: aviso a quien se agrega como miembro. */
CREATE OR ALTER TRIGGER dbo.TR_ChatDepartamentoMiembros_NotificarMiembro
ON dbo.ChatDepartamentoMiembros
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.NotificacionesUsuario (UsuarioId, Tipo, Titulo, Mensaje, EntidadTipo, EntidadId)
    SELECT i.UsuarioId,
           N'ChatDepartamento',
           N'Lo agregaron a un departamento de chat',
           LEFT(CONCAT(N'Ahora forma parte del departamento «', dep.Nombre, N'» (',
                       CASE WHEN i.PuedePublicar = 1 THEN N'puede publicar' ELSE N'solo lectura' END,
                       N'). Lo encuentra en Chat > Departamentos.'), 400),
           N'ChatDepartamento',
           i.DepartamentoId
    FROM inserted i
    INNER JOIN dbo.ChatDepartamentos dep ON dep.DepartamentoId = i.DepartamentoId;
END;
GO

IF XACT_STATE()<>1 THROW 58331,N'La transacción 0327 no está disponible.',1;
DECLARE @MigrationSha256 NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@MigrationSha256)<>64 OR @MigrationSha256 LIKE N'%[^0-9A-Fa-f]%' THROW 58332,N'SHA-256 inválido para 0327.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes)
VALUES(N'0327_avisos_reclamo_meta_departamento',N'0327_avisos_reclamo_meta_departamento.sql',UPPER(@MigrationSha256),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),
       N'POST-qa3-02/03/04: avisos al cliente (reclamo cerrado), al vendedor (meta) y al miembro de un departamento de chat. Aditiva.');
COMMIT TRANSACTION;
GO
