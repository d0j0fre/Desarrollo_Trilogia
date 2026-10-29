# Compensación de pedidos comerciales

0049/0050 emiten precios; 0051 consume únicamente la cotización publicada e
inmutable del login original. Antes de crear el pedido, valida dentro de la
transacción la relación vendedor–cliente, su revisión, la vigencia, el día y
la fuente canónica de precios. El mismo UUID no genera un segundo pedido.

El total final superior a ₡2.000.000 queda Retenido sin factura ni movimiento
de existencias. Los restantes pasan Liberado → Facturado en una transacción.
La decisión del supervisor exige su login original, capacidad explícita,
asignación al vendedor y revisión vigente. Revocar/reconceder la relación
no rehabilita un formulario anterior. No existe bypass de Administrador.

Las líneas conservan precio bruto y descuento agregado; no se reparte el
descuento redondeando precios unitarios. La factura usa el neto y el impuesto
agregado de 0039/PromotionEngine/0012. Ningún procedimiento acredita Entregado.
Los históricos, combos y regalías no se convierten ni se habilitan por esta vía.

La migración no concede permisos. APP recibirá por aprovisionamiento separado
solamente Consume/Decide/Clients/History/Details/Retained, después de comprobar
sus firmas, hashes y pruebas. Result/Finalize/RequireSupervisor permanecen
internos y sin EXECUTE directo. Ningún acceso directo a las tres tablas nuevas.

Ante un fallo, desactivar Commerce:OrdersEnabled y conservar el lector de
históricos. Retirar solo los grants nuevos que estén acreditados. Mantener
intentos, decisiones, hitos, ledger, facturas, movimientos y auditoría; no usar
escritores legacy como fallback. Revertir la configuración a su versión anterior.
La recuperación de un respaldo se prueba en un destino separado; no sobrescribir
principal ni borrar efectos financieros o existencias automáticamente.
