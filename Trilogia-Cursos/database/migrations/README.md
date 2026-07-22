# Migraciones de base de datos

## Responsabilidad

Danny, Esteban, Gerald y David forman el equipo administrativo DEV mediante usuarios individuales con `db_owner`. Danny permanece como administrador Microsoft Entra individual del servidor por una limitacion del tenant.

Cada migracion tiene un unico ejecutor designado y registrado en el PR. Los demas administradores no ejecutan simultaneamente el mismo script.

## Flujo obligatorio

1. Crear un script incremental, idempotente y revisable dentro de `database/migrations/`.
2. Abrir un pull request y obtener revision antes de ejecutar.
3. Validar el script con Microsoft ScriptDom.
4. Crear y verificar un BACPAC antes de cualquier cambio compartido.
5. Aplicar el script durante una ventana controlada por el ejecutor designado.
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
- No ejecutar una migracion si otro administrador ya figura como ejecutor activo.

## Orden vigente

| Orden | Archivo | Propósito | Dependencia |
|---:|---|---|---|
| 0001 | `0001_create_schema_migration_history.sql` | Ledger de migraciones | Esquema base |
| 0002 | `0002_chat_private_security.sql` | Conversaciones privadas y pertenencia | Usuarios y perfiles |
| 0003 | `0003_chat_departments_and_search.sql` | Departamentos, miembros, búsqueda y auditoría | 0002 |
| 0004 | `0004_private_delivery_evidence.sql` | Evidencia privada y descarga autorizada | Rutas/entregas CU-081 a CU-083 |
| 0005 | `0005_atomic_checkout_promotions.sql` | Pedido, inventario y promociones atómicos | Checkout CU-097 y promociones CU-171 a CU-174 |
| 0006 | `0006_warranty_workflow.sql` | Garantías sin duplicados y resolución auditada | Pedidos, garantías y auditoría |

Los scripts no incluyen `USE`: el ejecutor debe seleccionar explícitamente la base antes de iniciar. Los hashes escritos por 0002–0006 son hashes de manifiesto para identificar versión; la evidencia de despliegue debe registrar además el SHA-256 real del archivo y actualizar el ledger si corresponde.

## Evidencia privada legada

Los registros anteriores a 0004 conservan `StorageStatus = Legacy` y no se sirven desde la aplicación. Antes de retirar cualquier carpeta pública histórica, un operador debe copiar cada archivo a `EvidenceStorage:RootPath`, asignar una clave generada, verificar la firma binaria y marcar el registro como `Ready`. No se debe marcar como listo un archivo inexistente o no validado.
