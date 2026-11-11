# Reversión de 0312

Reversión compensatoria: volver a definir `dbo.sp_Kilometraje_Cerrar` con la firma de dos parámetros (`@KilometrajeId`, `@KmFinal`) que tenía antes (definición en `database_Esteban/` y en la migración que la creó). Eso **reabre** el defecto (un chofer puede cerrar la jornada de otro) y debe hacerse solo junto con el MVC anterior, que no envía el actor.

El permiso `FLOTA_KILOMETRAJE_ADMIN` y su asignación al perfil Administrador pueden conservarse (son inocuos); si se retiran, borrar primero las filas de `dbo.PerfilPermisos` que lo referencian. Marcar la fila `0312_kilometraje_propiedad` como `RolledBack` en `dbo.SchemaMigrationHistory`.

Importante: el MVC nuevo llama al procedimiento con `@ActorUsuarioId`; el MVC anterior no lo envía y, contra esta migración, recibe el error 53047 al cerrar (falla cerrado). Desplegar la base antes que la web.
