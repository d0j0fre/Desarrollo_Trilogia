# Reversión de 0310

La reversión es **compensatoria**; no se restaura texto plano.

- No se puede (ni se debe) recuperar la contraseña original desde un hash: las columnas `Contrasena` quedaron vacías y así deben seguir.
- Para volver al comportamiento anterior sin perder cuentas: desplegar la API y el MVC anteriores. La API anterior verifica cualquier hash con prefijo `pbkdf2-sha256`, pero **no entiende el hash de transición `legacy-sha256`**; los usuarios que aún no hayan iniciado sesión desde 0310 tendrían que restablecer su clave por el flujo «Olvidé mi contraseña» o por un administrador.
- Retirar el disparador solo si se acepta que un script de datos o un procedimiento vuelva a guardar texto plano: `DROP TRIGGER dbo.tr_Usuarios_SinTextoPlano;`. Los procedimientos `sp_Auth_*`, `sp_Admin_CreateClient` y `sp_Admin_UpdateClient` conservan sus parámetros anteriores (`@Contrasena` sigue siendo aceptado), por lo que una API/MVC anteriores siguen funcionando contra ellos.
- Marcar la fila `0310_credenciales_solo_hash` como `RolledBack` en `dbo.SchemaMigrationHistory`.
- Una restauración del respaldo previo es el último recurso y solo procede si se revierten también los datos creados después de la migración.
