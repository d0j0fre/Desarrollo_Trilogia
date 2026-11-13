# Reversión de 0300

Solo cambia un texto de `dbo.sp_Auth_UpdatePassword` (el mensaje «Credencial inválida.» deja de salir con caracteres dañados). No toca tablas ni datos. Revertir no es recomendable porque devolvería el texto dañado; si hiciera falta, ejecutar `CREATE OR ALTER` de `dbo.sp_Auth_UpdatePassword` con la definición de `0022_password_hash_transition.sql` y marcar la fila `0300_reparar_texto_contrasena` como `RolledBack` en `dbo.SchemaMigrationHistory`. Restaurar un BACPAC es el último recurso.
