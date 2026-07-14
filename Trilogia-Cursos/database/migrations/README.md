# Migraciones de base de datos

## Responsabilidad

Danny es el unico ejecutor autorizado de cambios SQL sobre Azure SQL DEV. Los colaboradores trabajan con bases locales para escritura y usan Azure SQL solo para lectura, integracion y verificacion.

## Flujo obligatorio

1. Crear un script incremental, idempotente y revisable dentro de `database/`.
2. Abrir un pull request y obtener revision antes de ejecutar.
3. Validar el script con Microsoft ScriptDom.
4. Crear y verificar un BACPAC antes de cualquier cambio compartido.
5. Aplicar el script durante una ventana controlada por el responsable SQL.
6. Registrar SHA-256, estado, fecha UTC, ejecutor, ambiente y notas en `dbo.SchemaMigrationHistory`.
7. Ejecutar consultas de verificacion y documentar el rollback.

## Rollback

Cada migracion debe indicar su estrategia de rollback antes de ejecutarse. Los cambios transaccionales deben usar `XACT_ABORT` y revertirse ante una validacion fallida. Restaurar un BACPAC es el ultimo recurso y requiere aprobacion expresa.

## Prohibiciones en DEV compartido

- No ejecutar `00_todo_en_uno.sql` ni `DistribuidoraJJ_DB.sql` sobre una base existente.
- No ejecutar scripts de `database_Esteban/`.
- No ejecutar seeds ni datos demo.
- No ejecutar CU090-CU100 sin evidencia, revision y bloque separado.
- No aplicar cambios manuales sin script, PR, backup y registro en el ledger.
- No compartir la cuenta administradora SQL.
