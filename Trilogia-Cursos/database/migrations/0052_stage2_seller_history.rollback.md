# Compensación del lector de historial

0052 agrega HistoryV2 para conservar el motivo de rechazo y la fecha de
actualización en Mis pedidos. DetailsV2 agrega el desglose monetario inmutable
para órdenes nuevas y mantiene la lectura original de los históricos.
No altera el lector ni los archivos aplicados
de 0051. Lee únicamente pedidos del vendedor acreditado por login original.
No reconstruye motivos históricos ausentes ni reescribe pedidos.

Conservar el módulo, ledger, decisiones y respaldos. Ante un fallo, desactivar
el flujo nuevo y retirar únicamente su grant revisado; no volver a escritores
legacy ni conceder SELECT de las tablas privadas. No sobrescribir principal.
