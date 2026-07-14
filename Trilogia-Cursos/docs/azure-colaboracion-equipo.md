# Colaboracion segura en Azure DEV

## Arquitectura

- Las escrituras de desarrollo se realizan en bases locales por colaborador.
- Azure SQL DEV se usa para integracion, lectura y verificacion compartida.
- Danny es el unico responsable autorizado para ejecutar cambios SQL en Azure.
- Todo cambio de esquema requiere script incremental, PR, BACPAC, ScriptDom y registro en el ledger.

## Accesos del equipo

| Colaborador | GitHub | Portal Azure | Azure SQL |
| ----------- | ------ | ------------ | --------- |
| Danny | `d0j0fre` | Propietario heredado | Administrador y ejecutor SQL |
| Esteban | `EstebanAVF` | Reader del Resource Group | Solo lectura con Microsoft Entra |
| Gerald | `GeraldRB` | Sin acceso | Solo lectura con Microsoft Entra |
| David | `MontDavidH` | Reader del Resource Group | Solo lectura con Microsoft Entra |

Los lectores SQL tienen un usuario individual, `db_datareader` y `VIEW DEFINITION`. No reciben escritura, DDL, administracion de usuarios ni acceso a la cuenta administradora SQL.

## Conexion con Microsoft Entra MFA

1. Abrir SSMS o una herramienta compatible con Azure SQL.
2. Usar el servidor y la base comunicados por Danny mediante el canal del equipo.
3. Elegir autenticacion Microsoft Entra con MFA.
4. Iniciar sesion con la cuenta individual autorizada.
5. Confirmar una consulta `SELECT` y no ejecutar scripts de cambio.

## Cambio de IP publica

1. Confirmar la IP nueva desde el equipo que se conectara.
2. Enviarla a Danny por un canal privado.
3. Danny crea una regla individual de una sola IP durante un bloque aprobado.
4. Se prueba lectura y se retira la regla anterior cuando ya no sea necesaria.

No se publican IPs en Git y no se crean rangos amplios.

## Flujo de migraciones

1. Crear un archivo incremental en `database/`.
2. Validarlo con ScriptDom y abrir un PR.
3. Revisar impacto y rollback.
4. Crear un BACPAC reciente.
5. Danny aplica el cambio en la ventana aprobada.
6. Registrar el resultado en `dbo.SchemaMigrationHistory`.
7. Ejecutar verificaciones de solo lectura.

## Secretos

- No modificar `appsettings` versionados con valores reales.
- No compartir connection strings, contrasenas, tokens, claves ni perfiles de publicacion.
- No usar ni distribuir la cuenta administradora SQL.
- Guardar credenciales operativas solo en el mecanismo privado aprobado.
- Rotar una credencial inmediatamente si se expone.
