# Reversión de 0267

```sql
DROP PROCEDURE dbo.sp_Payroll_ListarPeriodosAbiertos;
DROP PROCEDURE dbo.sp_Payroll_ListarEmpleadosActivos;
DROP PROCEDURE dbo.sp_Payroll_ListarReglas;
DELETE pp FROM dbo.PerfilPermisos pp
JOIN dbo.Perfiles pf ON pf.PerfilId = pp.PerfilId
JOIN dbo.Permisos pm ON pm.PermisoId = pp.PermisoId
WHERE pf.Nombre = N'Gerente' AND pm.Codigo IN (N'PLANILLA_VER', N'PLANILLA_APROBAR')
  AND pp.UsuarioAsignacionNombre = N'Migracion 0267 planilla gerente';
```

Los procedimientos solo leen. Si el Gerente ya tenía alguno de los dos permisos antes de 0267 la asignación no se duplicó, así que el `DELETE` solo retira lo que insertó esta migración (la marca va en `UsuarioAsignacionNombre`). Marcar la fila `0267_planilla_aprobacion_gerente_y_listas` como `RolledBack` en `dbo.SchemaMigrationHistory`.
