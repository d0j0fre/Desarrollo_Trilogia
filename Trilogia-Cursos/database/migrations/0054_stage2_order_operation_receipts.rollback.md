# Reversión de recibos de operación

Antes de instalar: respaldo COPY_ONLY CHECKSUM y VERIFYONLY; guardar ledger y huellas. Validar en copia propia restaurada.

Ante fallo, deshabilitar Commerce:OrdersEnabled y volver a la versión anterior del runtime. No eliminar ledger, facturas, mensajes, auditoría, pedidos ni hitos; no restaurar sobre principal. Una reversión SQL requiere otra migración revisada que acredite las huellas vigentes. No ejecutar 0051 sobre una instalación existente.

Los consumidores del resultado distinguen la operación original del estado actual consultado por HistoryV2/DetailsV2. No ampliar los estados admitidos del lector ni convertir una factura en prueba de entrega.
