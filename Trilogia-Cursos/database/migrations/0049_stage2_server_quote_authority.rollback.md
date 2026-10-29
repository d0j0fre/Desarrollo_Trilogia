# Compensación de cotizaciones

Antes de instalar: acreditar origen, hashes y ledger, respaldo CHECKSUM/VERIFYONLY
y ensayo en copia restaurada. Aprovisionar por separado una identidad de precios
con EXECUTE únicamente Capture/Publish/ReadOwn; APP solo ReadOwn. Ninguna identidad
recibe SELECT/INSERT/UPDATE/DELETE de tablas ni EXECUTE de helpers internos.
No compartir credenciales de precios con MVC ni reutilizar bootstrap como autoridad.

Las fuentes son Productos/Promociones/Usuarios canónicos, bloqueados de forma
coherente. El propietario original de UUID, intención, revisión de la relación,
snapshot y vigencia quedan inmutables. La vigencia está acotada por sesión y día
comercial de Costa Rica. Un intento expirado no se renueva; otro login no lo adopta.
Las líneas monetarias requieren centavos canónicos y paridad con PromotionEngine.
Combos y regalías no se habilitan mediante este contrato.

La revisión de la fuente aumenta transaccionalmente por escrituras de precio,
actividad/nombre, promociones y segmento. Un cambio A→B→A no revive una cotización
anterior. Cambiar solo stock no incrementa la revisión de precios; la disponibilidad
se debe validar y descontar durante el consumo. El stock de una posible regalía
también queda en la huella de fuente, aunque su consumo continúe bloqueado.

La instalación no cambia stock, pedidos, facturas, clientes, segmentos ni grants.
El consumo 6B posterior debe validar login, relación/revisión, fuente y vigencia
dentro de la misma transacción que stock/factura/auditoría. No basta con ReadOwn.

Ante un fallo, mantener cerrado el feature y revocar solo grants nuevos revisados.
Conservar propietarios, cotizaciones, líneas, recibos y ledger; no borrar datos ni
activar escritor legacy como fallback. No restaurar sobre principal automáticamente.
