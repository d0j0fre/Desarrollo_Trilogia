SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF OBJECT_ID(N'dbo.SchemaMigrationHistory',N'U') IS NULL OR OBJECT_ID(N'dbo.Permisos',N'U') IS NULL OR OBJECT_ID(N'dbo.PerfilPermisos',N'U') IS NULL OR OBJECT_ID(N'dbo.Perfiles',N'U') IS NULL THROW 58400,N'Faltan el ledger, Permisos, PerfilPermisos o Perfiles.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0313_permisos_mutacion_granulares' AND Status=N'Applied') THROW 58401,N'La migracion ya fue aplicada.',1;
BEGIN TRANSACTION;
GO
/* Permisos granulares de mutacion.

   Las acciones que cambian datos de Clientes, Consultas, Creditos, Inventario y Empleados
   (tareas y solicitudes) solo exigian el permiso del MODULO: quien podia ver (por ejemplo
   «Credito y Cobro» con CREDITOS_VER, o un Bodeguero con INVENTARIO_VER) podia tambien
   crear, editar, inactivar, registrar movimientos de credito o eliminar. Ahora cada accion
   exige su permiso (el catalogo ya existia en cu061, cu062, cu080 y 0017; aqui se asegura
   que exista tambien en otras bases) y el Administrador los conserva todos.

   Nuevo: INVENTARIO_ELIMINAR (eliminar productos de forma permanente; antes bastaba
   INVENTARIO_EDITAR). Para no romper el trabajo diario se conserva lo que cada perfil de
   oficina hace hoy: «Credito y Cobro» registra movimientos de credito (cobros) y
   «Supervisor» gestiona tareas y solicitudes de su personal; configurar credito, crear,
   editar, inactivar clientes y eliminar productos queda solo para quien tenga el permiso. */
DECLARE @Requeridos TABLE (Codigo NVARCHAR(100) NOT NULL, Modulo NVARCHAR(100) NOT NULL, Nombre NVARCHAR(150) NOT NULL, Descripcion NVARCHAR(500) NOT NULL);
INSERT @Requeridos (Codigo, Modulo, Nombre, Descripcion) VALUES
    (N'CLIENTES_VER', N'Clientes', N'Ver clientes', N'Permite consultar el listado y detalle de clientes.'),
    (N'CLIENTES_CREAR', N'Clientes', N'Crear clientes', N'Permite registrar clientes manualmente.'),
    (N'CLIENTES_EDITAR', N'Clientes', N'Editar clientes', N'Permite modificar los datos de clientes.'),
    (N'CLIENTES_INACTIVAR', N'Clientes', N'Inactivar clientes', N'Permite suspender o reactivar la relación comercial con clientes.'),
    (N'CONSULTAS_VER', N'Consultas', N'Ver consultas', N'Permite consultar mensajes recibidos.'),
    (N'CONSULTAS_ATENDER', N'Consultas', N'Atender consultas', N'Permite responder y cambiar el estado de consultas.'),
    (N'CREDITOS_VER', N'Créditos', N'Ver créditos de clientes', N'Permite consultar crédito, deuda y movimientos financieros de clientes.'),
    (N'CREDITOS_CONFIGURAR', N'Créditos', N'Configurar crédito', N'Permite definir límite, estado y bloqueo de crédito.'),
    (N'CREDITOS_MOVIMIENTOS', N'Créditos', N'Registrar movimientos de crédito', N'Permite registrar cargos, abonos y ajustes.'),
    (N'INVENTARIO_VER', N'Inventario', N'Ver inventario', N'Permite consultar productos y movimientos.'),
    (N'INVENTARIO_CREAR', N'Inventario', N'Crear productos', N'Permite registrar productos.'),
    (N'INVENTARIO_EDITAR', N'Inventario', N'Editar productos', N'Permite modificar productos y su visibilidad.'),
    (N'INVENTARIO_MOVIMIENTOS', N'Inventario', N'Registrar movimientos', N'Permite registrar entradas, salidas y ajustes.'),
    (N'INVENTARIO_ELIMINAR', N'Inventario', N'Eliminar productos permanentemente', N'Permite eliminar productos sin referencias, con efecto irreversible.'),
    (N'EMPLEADOS_TAREAS', N'Empleados', N'Gestionar tareas de empleados', N'Permite asignar tareas y cambiar su estado.'),
    (N'EMPLEADOS_SOLICITUDES', N'Empleados', N'Resolver solicitudes de empleados', N'Permite aprobar o rechazar solicitudes de permisos y vacaciones.');

-- Solo se crea lo que falta: no se renombran ni se reactivan permisos que el equipo ajusto.
INSERT dbo.Permisos (Codigo, Modulo, Nombre, Descripcion, Activo)
SELECT requerido.Codigo, requerido.Modulo, requerido.Nombre, requerido.Descripcion, 1
FROM @Requeridos requerido
WHERE NOT EXISTS (SELECT 1 FROM dbo.Permisos permiso WHERE permiso.Codigo = requerido.Codigo);

-- El Administrador conserva todas las capacidades.
INSERT dbo.PerfilPermisos (PerfilId, PermisoId, UsuarioAsignacionId, UsuarioAsignacionNombre)
SELECT perfil.PerfilId, permiso.PermisoId, NULL, N'Migración 0313'
FROM dbo.Perfiles perfil
CROSS JOIN dbo.Permisos permiso
WHERE perfil.Nombre = N'Administrador'
  AND permiso.Codigo IN (SELECT Codigo FROM @Requeridos)
  AND NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos asignacion WHERE asignacion.PerfilId = perfil.PerfilId AND asignacion.PermisoId = permiso.PermisoId);

-- Quien podia consultar un modulo conserva la consulta (las mutaciones quedan separadas y explicitas).
INSERT dbo.PerfilPermisos (PerfilId, PermisoId, UsuarioAsignacionId, UsuarioAsignacionNombre)
SELECT DISTINCT asignacion.PerfilId, lectura.PermisoId, NULL, N'Migración 0313'
FROM dbo.PerfilPermisos asignacion
INNER JOIN dbo.Permisos existente ON existente.PermisoId = asignacion.PermisoId
INNER JOIN dbo.Permisos lectura ON lectura.Codigo = CASE
        WHEN existente.Codigo LIKE N'CLIENTES[_]%' THEN N'CLIENTES_VER'
        WHEN existente.Codigo LIKE N'CONSULTAS[_]%' THEN N'CONSULTAS_VER'
        WHEN existente.Codigo LIKE N'CREDITOS[_]%' THEN N'CREDITOS_VER'
        WHEN existente.Codigo LIKE N'INVENTARIO[_]%' AND existente.Codigo NOT LIKE N'INVENTARIO[_]INTELIGENCIA%' THEN N'INVENTARIO_VER'
    END
WHERE NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos otra WHERE otra.PerfilId = asignacion.PerfilId AND otra.PermisoId = lectura.PermisoId);

-- Perfiles de oficina: se conserva lo que hacen hoy (los perfiles pueden no existir; se buscan por nombre).
DECLARE @Conservar TABLE (Perfil NVARCHAR(100) NOT NULL, Codigo NVARCHAR(100) NOT NULL);
INSERT @Conservar (Perfil, Codigo) VALUES
    (N'Cr_dito y Cobro', N'CREDITOS_MOVIMIENTOS'),
    (N'Supervisor', N'EMPLEADOS_TAREAS'),
    (N'Supervisor', N'EMPLEADOS_SOLICITUDES');

INSERT dbo.PerfilPermisos (PerfilId, PermisoId, UsuarioAsignacionId, UsuarioAsignacionNombre)
SELECT perfil.PerfilId, permiso.PermisoId, NULL, N'Migración 0313'
FROM @Conservar conservar
INNER JOIN dbo.Perfiles perfil ON perfil.Nombre LIKE conservar.Perfil
INNER JOIN dbo.Permisos permiso ON permiso.Codigo = conservar.Codigo
WHERE NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos asignacion WHERE asignacion.PerfilId = perfil.PerfilId AND asignacion.PermisoId = permiso.PermisoId);
GO
DECLARE @sha NVARCHAR(128)=N'$(MigrationSha256)';
IF LEN(@sha)<>64 OR @sha LIKE N'%[^0-9A-Fa-f]%' THROW 58402,N'SHA-256 invalido.',1;
INSERT dbo.SchemaMigrationHistory(MigrationId,FileName,FileSha256,Status,AppliedBy,EnvironmentName,Notes) VALUES(N'0313_permisos_mutacion_granulares',N'0313_permisos_mutacion_granulares.sql',UPPER(@sha),N'Applied',ORIGINAL_LOGIN(),DB_NAME(),N'Seguridad: las acciones de Clientes, Consultas, Creditos, Inventario y Empleados (tareas y solicitudes) exigen su permiso de mutacion y no solo el del modulo; nuevo INVENTARIO_ELIMINAR.');
COMMIT TRANSACTION;
GO
