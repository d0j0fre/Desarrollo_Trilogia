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
| 0007 | `0007_secure_document_management.sql` | Documentos privados, versiones y catálogos operativos | Usuarios y permisos |
| 0008 | `0008_document_expiration_alerts.sql` | Alertas documentales idempotentes | 0007 |
| 0009 | `0009_annual_department_budgets.sql` | Presupuestos anuales normalizados | 0007 (departamentos) |
| 0010 | `0010_operating_expenses_alignment.sql` | Gastos operativos, comprobantes privados y estados | 0009; esquema CU-222 legado opcional |
| 0011 | `0011_budget_actual_comparison.sql` | Comparación presupuesto versus real | 0009 y 0010 |
| 0012 | `0012_inventory_combos_transformations_intelligence.sql` | Combos vendibles, snapshots de pedido/factura, checkout idempotente, transformación atómica, inteligencia y permisos específicos | Esquema de productos/pedidos/usuarios/facturas/permisos; 0005 para checkout/promociones; dependencias Sprint 4 verificadas cuando correspondan |

Los scripts no incluyen `USE`: el ejecutor debe seleccionar explícitamente la base antes de iniciar. Los hashes escritos por 0002–0011 son hashes de manifiesto para identificar versión; la evidencia de despliegue debe registrar además el SHA-256 real del archivo y actualizar el ledger si corresponde.

Al 23 de julio de 2026, Azure DEV registra 0007–0011 como aplicadas y se verificaron sus objetos, columnas principales, índices, constraints y permisos. No deben volver a ejecutarse en esa base. Esta evidencia no demuestra que 0002–0006 estén aplicadas ni convierte el ledger histórico en una secuencia completa. Antes de cualquier corrección se requiere BACPAC, ejecutor único, script incremental nuevo y QA posterior. `database_Esteban/cu222_gastos_presupuesto.sql` es sólo referencia histórica.

## Ejecución controlada de 0012

La migración 0012 todavía **no se ha aplicado en Azure DEV**. No debe ejecutarse
sin BACPAC verificado, ejecutor único y verificación posterior. Tampoco deben
ejecutarse las migraciones `0002`–`0006` del PR #114.

Use el ejecutor seguro para calcular el SHA-256 real del archivo final e
inyectarlo por `sqlcmd`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/database/Invoke-Migration0012.ps1 -ServerInstance "<instancia>" -Database "<base>"
```

El modo predeterminado es `-AuthenticationMode Entra` y agrega `sqlcmd -G` para
usuarios individuales de Microsoft Entra. Para LocalDB se usa
`-AuthenticationMode Windows`, que agrega `sqlcmd -E`. El ejecutor no acepta
contraseñas, no usa `-P` y transmite el hash únicamente como una variable
SQLCMD: `-v "MigrationSha256=<SHA-256>"`.

`-DryRun` solo calcula y valida el hash, informa la ruta y el modo seleccionado
sin abrir conexión. El script usa `sqlcmd -b`, no recibe ni imprime connection
strings o contraseñas y falla si la expansión de hash no es válida. La prueba
sin conexión `scripts/database/Test-InvokeMigration0012.ps1` cubre los modos
Entra/Windows, la variable SQLCMD y las protecciones del ejecutor. Después debe ejecutarse
`0012_inventory_combos_transformations_intelligence.verify.sql` en modo de solo
lectura. El rollback continúa documentado en el archivo correspondiente.

### Compatibilidad legada validada localmente

La versión final de 0012 acepta tanto una instalación sin tablas de combos como
el esquema legado confirmado en el BACPAC previo de Azure DEV. La reconciliación
renombra columnas legadas, conserva claves y datos, rellena las columnas
canónicas, valida antes de crear restricciones y reconstruye
`PedidoComboDetalle` únicamente desde `PedidoDetalle.PedidoComboId`. Cualquier
cantidad no divisible, referencia huérfana, duplicado o snapshot contradictorio
produce `THROW` y rollback.

Los componentes legados dejan de contarse como productos sueltos en pedidos,
checkout, inteligencia, restauración y facturación. `FacturaCombos` no recibe
backfill de facturas antiguas para evitar doble contabilización; se usa para
facturas generadas después de 0012.

`database/verify/0012_legacy_reconciliation_local.sql` es una prueba en dos
fases exclusiva de LocalDB desechable: antes de 0012 captura evidencia agregada
sin datos personales y después compara conteos, claves, totales, inventario y
la reconstrucción, además de ejecutar `DBCC CHECKDB`.

## Evidencia privada legada

Los registros anteriores a 0004 conservan `StorageStatus = Legacy` y no se sirven desde la aplicación. Antes de retirar cualquier carpeta pública histórica, un operador debe copiar cada archivo a `EvidenceStorage:RootPath`, asignar una clave generada, verificar la firma binaria y marcar el registro como `Ready`. No se debe marcar como listo un archivo inexistente o no validado.
