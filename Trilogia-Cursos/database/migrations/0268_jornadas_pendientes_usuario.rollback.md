# Reversión de 0268

Restaurar la definición anterior de `dbo.sp_RRHH_ListarJornadasPendientes` (sin la columna `UsuarioId`):

```sql
CREATE OR ALTER PROCEDURE dbo.sp_RRHH_ListarJornadasPendientes @Desde DATE,@Hasta DATE AS
BEGIN SET NOCOUNT ON; SELECT j.JornadaId,j.EmpleadoId,u.NombreCompleto,j.Fecha,j.HorasOrdinarias,j.HorasExtra,j.HorasAusencia,j.Observaciones,j.Estado,j.RespuestaSupervisor,j.VersionFila FROM dbo.EmpleadoJornadas j JOIN dbo.Empleados e ON e.EmpleadoId=j.EmpleadoId JOIN dbo.Usuarios u ON u.UsuarioId=e.UsuarioId WHERE j.Estado=N'Enviada' AND j.Fecha BETWEEN @Desde AND @Hasta ORDER BY j.Fecha,u.NombreCompleto; END;
```

No cambia tablas ni datos. El código nuevo tolera la ausencia de la columna (no oculta la propia jornada, pero `sp_RRHH_ResolverJornada` sigue rechazándola). Marcar la fila `0268_jornadas_pendientes_usuario` como `RolledBack` en `dbo.SchemaMigrationHistory`.
