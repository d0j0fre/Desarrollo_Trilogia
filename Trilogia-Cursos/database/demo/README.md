# Carga demo DEMO-2026-08

Estos scripts solo aceptan `DistribuidoraJJ_DB_DEV`. Son aditivos e idempotentes: crean un lote trazable, categorías de gasto y un presupuesto QA completamente distribuido. No crean usuarios, contraseñas, ventas, facturas ni modifican filas existentes.

No se ejecutan desde CI ni automáticamente. Antes de ejecutarlos, el responsable designado debe abrir/revisar el PR, verificar el ledger de migraciones y crear un BACPAC/restauración conforme a `database/migrations/README.md`.

Ejecutar con una identidad autorizada y seleccionando explícitamente la base DEV:

1. `seed_demo_full.sql`
2. `seed_demo_full.verify.sql`
3. `seed_demo_full.rollback.sql` solo para retirar el lote DEMO-2026-08.

La carga masiva integral de ventas, pedidos, compras, planilla, chat y usuarios no se incluyó deliberadamente: hacerlo requeriría una revisión autenticada del esquema aplicado y una identidad oficial para crear cuentas con el mecanismo de hash vigente. El seed histórico del repositorio no se reutiliza porque contiene un destino distinto y un flujo de contraseñas temporal que no cumple estas restricciones.
