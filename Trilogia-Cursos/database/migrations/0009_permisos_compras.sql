/* =========================================================
   CU-101 / CU-222 - Sprint 4: Permisos para el rol "Compras"
   Objetivo:
   - Registrar los códigos de permiso GASTOS_REGISTRAR y
     PROVEEDORES_GESTIONAR (Modulo = 'Compras').
   - Asignarlos al rol "Compras" para que pueda entrar a
     Gastos y Proveedores sin ser Administrador.
   Script seguro: usa MERGE / NOT EXISTS, no elimina datos.
   ========================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* 1. Catálogo de permisos nuevos */
MERGE dbo.Permisos AS target
USING (VALUES
    ('GASTOS_REGISTRAR', 'Compras', 'Registrar gastos', 'Permite registrar gastos operativos y cuentas presupuestarias.'),
    ('PROVEEDORES_GESTIONAR', 'Compras', 'Gestionar proveedores', 'Permite crear, editar y desactivar proveedores.')
) AS source (Codigo, Modulo, Nombre, Descripcion)
ON target.Codigo = source.Codigo
WHEN MATCHED THEN
    UPDATE SET
        target.Modulo = source.Modulo,
        target.Nombre = source.Nombre,
        target.Descripcion = source.Descripcion,
        target.Activo = 1
WHEN NOT MATCHED THEN
    INSERT (Codigo, Modulo, Nombre, Descripcion, Activo)
    VALUES (source.Codigo, source.Modulo, source.Nombre, source.Descripcion, 1);
GO

/* 2. Asignación al rol "Compras" (ya existe desde el seed demo) */
INSERT INTO dbo.PerfilPermisos (PerfilId, PermisoId, UsuarioAsignacionId, UsuarioAsignacionNombre)
SELECT p.PerfilId, pe.PermisoId, NULL, 'Carga CU-101/CU-222 Sprint 4'
FROM dbo.Perfiles p
INNER JOIN dbo.Permisos pe
    ON pe.Codigo IN ('GASTOS_REGISTRAR', 'PROVEEDORES_GESTIONAR')
WHERE p.Nombre = 'Compras'
  AND pe.Activo = 1
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.PerfilPermisos pp
      WHERE pp.PerfilId = p.PerfilId
        AND pp.PermisoId = pe.PermisoId
  );
GO

/* 3. Registro en la bitácora de migraciones */
IF OBJECT_ID('dbo.SchemaMigrationHistory', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0009_permisos_compras')
BEGIN
    INSERT INTO dbo.SchemaMigrationHistory
        (MigrationId, FileName, FileSha256, Status, AppliedBy, EnvironmentName, Notes)
    VALUES
        (N'0009_permisos_compras', N'0009_permisos_compras.sql', N'PENDIENTE_CALCULAR_SHA256',
         N'Applied', SUSER_SNAME(), N'DEV', N'CU-101/CU-222 - Permisos GASTOS_REGISTRAR y PROVEEDORES_GESTIONAR para rol Compras.');
END;
GO