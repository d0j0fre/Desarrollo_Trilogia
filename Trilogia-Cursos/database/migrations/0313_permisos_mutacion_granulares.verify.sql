SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId=N'0313_permisos_mutacion_granulares' AND Status=N'Applied') THROW 58410,N'La migracion 0313 no esta registrada.',1;

DECLARE @Requeridos TABLE (Codigo NVARCHAR(100) NOT NULL);
INSERT @Requeridos VALUES (N'CLIENTES_VER'),(N'CLIENTES_CREAR'),(N'CLIENTES_EDITAR'),(N'CLIENTES_INACTIVAR'),(N'CONSULTAS_VER'),(N'CONSULTAS_ATENDER'),
    (N'CREDITOS_VER'),(N'CREDITOS_CONFIGURAR'),(N'CREDITOS_MOVIMIENTOS'),(N'INVENTARIO_VER'),(N'INVENTARIO_CREAR'),(N'INVENTARIO_EDITAR'),
    (N'INVENTARIO_MOVIMIENTOS'),(N'INVENTARIO_ELIMINAR'),(N'EMPLEADOS_TAREAS'),(N'EMPLEADOS_SOLICITUDES');

IF EXISTS(SELECT 1 FROM @Requeridos r WHERE NOT EXISTS(SELECT 1 FROM dbo.Permisos p WHERE p.Codigo=r.Codigo AND p.Activo=1)) THROW 58411,N'Falta algun permiso granular del catalogo.',1;
IF EXISTS(
    SELECT 1 FROM dbo.Perfiles perfil CROSS JOIN @Requeridos r
    WHERE perfil.Nombre=N'Administrador'
      AND NOT EXISTS(SELECT 1 FROM dbo.PerfilPermisos pp JOIN dbo.Permisos p ON p.PermisoId=pp.PermisoId WHERE pp.PerfilId=perfil.PerfilId AND p.Codigo=r.Codigo)) THROW 58412,N'El Administrador no conserva todos los permisos granulares.',1;

-- Quien tiene una mutacion de un modulo conserva su consulta.
IF EXISTS(
    SELECT 1 FROM dbo.PerfilPermisos pp JOIN dbo.Permisos p ON p.PermisoId=pp.PermisoId
    WHERE p.Codigo IN (N'CLIENTES_CREAR',N'CLIENTES_EDITAR',N'CLIENTES_INACTIVAR')
      AND NOT EXISTS(SELECT 1 FROM dbo.PerfilPermisos x JOIN dbo.Permisos px ON px.PermisoId=x.PermisoId WHERE x.PerfilId=pp.PerfilId AND px.Codigo=N'CLIENTES_VER')) THROW 58413,N'Hay perfiles que mutan clientes sin poder verlos.',1;

-- La mutacion NO se reparte por tener solo la consulta: ningun perfil distinto del Administrador la recibio sin tenerla antes.
IF EXISTS(
    SELECT 1 FROM dbo.PerfilPermisos pp JOIN dbo.Permisos p ON p.PermisoId=pp.PermisoId JOIN dbo.Perfiles perfil ON perfil.PerfilId=pp.PerfilId
    WHERE pp.UsuarioAsignacionNombre=N'Migración 0313'
      AND p.Codigo IN (N'CLIENTES_CREAR',N'CLIENTES_EDITAR',N'CLIENTES_INACTIVAR',N'CONSULTAS_ATENDER',N'CREDITOS_CONFIGURAR',
                       N'INVENTARIO_CREAR',N'INVENTARIO_EDITAR',N'INVENTARIO_MOVIMIENTOS',N'INVENTARIO_ELIMINAR')
      AND perfil.Nombre<>N'Administrador') THROW 58414,N'La migracion reparto una mutacion a un perfil distinto del Administrador.',1;

-- Efecto observable: con la matriz de la base, un perfil de solo lectura no pasa la comprobacion de la accion.
DECLARE @lectura NVARCHAR(100) = (SELECT TOP (1) perfil.Nombre FROM dbo.Perfiles perfil
    WHERE perfil.Nombre LIKE N'Cr_dito y Cobro');
IF @lectura IS NOT NULL
BEGIN
    DECLARE @r TABLE(Tiene BIT);
    INSERT @r EXEC dbo.sp_Admin_HasPermissionByCode @NombreRol=@lectura, @Codigo=N'CREDITOS_CONFIGURAR';
    IF EXISTS(SELECT 1 FROM @r WHERE Tiene=1) THROW 58415,N'Credito y Cobro puede configurar credito.',1;
    DELETE @r;
    INSERT @r EXEC dbo.sp_Admin_HasPermissionByCode @NombreRol=@lectura, @Codigo=N'CREDITOS_MOVIMIENTOS';
    IF NOT EXISTS(SELECT 1 FROM @r WHERE Tiene=1) THROW 58416,N'Credito y Cobro perdio el registro de movimientos (cobros).',1;
END;
PRINT N'Verificacion 0313 completada.';
