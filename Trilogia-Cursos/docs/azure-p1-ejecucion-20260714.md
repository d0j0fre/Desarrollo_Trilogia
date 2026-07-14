# Ejecucion Azure P1 - 2026-07-14

## Resultado

- Se creo y verifico `DistribuidoraJJ_DB_DEV_pre_P1_20260714-021824.bacpac` con 76,402 bytes.
- El BACPAC anterior se conservo y el contenedor tiene dos respaldos.
- Se auditaron 28 cuentas: 27 activas y 1 inactiva antes de la contencion.
- Se rotaron 12 cuentas operativas privilegiadas con credenciales DPAPI y se desactivo 1 cuenta demo inequivoca.
- Quedaron 26 cuentas activas, 2 inactivas y 1 administrador activo.
- Danny quedo configurado como administrador Microsoft Entra de Azure SQL.
- Esteban, Gerald y David tienen usuarios Entra individuales de solo lectura en la base.
- Esteban y David tienen exactamente una asignacion directa Reader en el Resource Group.
- Gerald no tiene acceso RBAC al Portal.

## Firewall

Se crearon reglas individuales, sin documentar direcciones:

- `owner-danny-20260714`
- `collab-esteban-20260714`
- `collab-gerald-20260714`
- `collab-david-20260714`
- `app-api-out-01` a `app-api-out-33`

Las aplicaciones comparten las IPs de salida normalizadas del mismo plan. La regla amplia de servicios Azure fue retirada y no fue necesario restaurarla.

## Pruebas

- API raiz, health y Swagger: aprobados.
- Productos API y tienda MVC con lectura de Azure SQL: aprobados.
- Login con una credencial rotada: aprobado.
- MVC Home y pagina de login: aprobados.
- Lectura SQL directa de Danny: aprobada.
- Los tres colaboradores: `db_datareader` y `VIEW DEFINITION` aprobados.
- INSERT, UPDATE, DELETE, ALTER, CREATE TABLE, CONTROL y administracion de usuarios: no permitidos.

## Ledger e inventario SQL

- Confirmado: `0001_create_schema_migration_history.sql` aplicado.
- Confirmado: CU101 y CU102 registrados como `BaselineVerified` tras comprobar objetos y parametros.
- Faltante: ningun script adicional fue declarado faltante durante P1.2.
- Desconocido: CU090-CU100 permanecen sin evidencia suficiente de ejecucion previa.
- No aplicable: los historicos de `database_Esteban/` no son fuente de verdad de Azure DEV.
- Prohibido en esta fase: baselines, seeds, CU098 y `sp_Store_GetProducts.sql` completo.

## Rollback

- Dos intentos de validacion de la rotacion se revirtieron antes del commit.
- La transaccion definitiva se confirmo y supero la verificacion posterior.
- No se uso rollback de firewall.
- El inventario previo y los resultados sanitizados permanecen fuera del repositorio.

## Riesgos pendientes

- El sistema sigue comparando contrasenas de aplicacion en texto plano.
- SMTP historico continua pendiente de rotacion o revocacion.
- SCM y FTP basic auth siguen habilitados.
- Logging, health check administrado, Key Vault e identidad administrada siguen pendientes.
- Storage conserva temporalmente dos BACPAC y Shared Key habilitado.
- El historial Git historico requiere una decision coordinada independiente.
- CU090-CU100 no tienen evidencia suficiente en el ledger y no se ejecutaron.
