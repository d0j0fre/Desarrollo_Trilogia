# Backlog de correcciones

Fuente: `Entregas/G8_SC803_J_BugReport.pdf` · detalle en [`fuentes/BugReport_G8_SC803_J.txt`](fuentes/BugReport_G8_SC803_J.txt) · plan en [`PLAN_CORRECCIONES.md`](PLAN_CORRECCIONES.md).

Estados: `Pendiente` → `En curso` → `Corregido` (mergeado en `main`) → `Verificado` (re-probado en la ronda final). `Bloqueado` con nota si depende de otro lote.

Cada lote actualiza **solo sus filas** en el mismo PR de la corrección.

| Lote | Responsable | Bugs | | Lote | Responsable | Bugs |
|---|---|---:|---|---|---|---:|
| E1 | Esteban (IA) | 7 | | E7 | Esteban (IA) | 4 |
| E2 | Esteban (IA) | 7 | | E8 | Esteban (IA) | 9 |
| E3 | Esteban (IA) | 7 | | E9 | Esteban (IA) | 6 |
| E4 | Esteban (IA) | 10 | | D | David | 10 |
| E5 | Esteban (IA) | 6 | | N | Danny | 10 |
| E6 | Esteban (IA) | 6 | | G | Gerald | 9 |

**Total 91** — P1: 7 · P2: 19 · P3: 65 — Esteban 62 · compañeros 29.

| Bug | Prio | Lote | Responsable | Título | Estado | Rama / PR | Nota |
|---|---|---|---|---|---|---|---|
| BUG-01-01 | P2 | E5 | Esteban | [CU-012-01, CU-013-09] No llega el correo con el enlace de recuperación de contraseña | Pendiente | | |
| BUG-01-02 | P3 | G | Gerald | [CU-012-02] La recuperación de contraseña no indica que el correo no está registrado | Pendiente | | |
| BUG-01-03 | P3 | G | Gerald | [CU-012-07] Al superar el límite de solicitudes de recuperación el sistema muestra una página en blanco con error 429 | Pendiente | | |
| BUG-02-01 | P3 | G | Gerald | [CU-021-03, CU-022-03] Ante una falla de conexión, Mi Perfil no muestra una vista ni un mensaje de error controlado | Pendiente | | |
| BUG-02-02 | P3 | G | Gerald | [CU-022-06] El campo Teléfono de Mi Perfil acepta más de 10 dígitos | Pendiente | | |
| BUG-03-01 | P2 | E5 | Esteban | [CU-031-05, CU-032-03, CU-074-02, CU-121-03, CU-114-01] El sistema no envía correos: ni la confirmación de consultas, ni el comprobante de pedidos, ni la recuperación de contraseña | Pendiente | | |
| BUG-04-01 | P2 | E2 | Esteban | [CU-041-06] El sistema permite inactivar un rol que tiene usuarios asignados | Pendiente | | |
| BUG-04-02 | P3 | E2 | Esteban | [CU-042-05] El rol Administrador no tiene asignados todos los permisos: muestra 98 de 99 | Pendiente | | |
| BUG-04-03 | P2 | E2 | Esteban | [CU-041-08] Un usuario de un rol inactivo conserva el acceso a la Planilla | Pendiente | | Probable fix en restructuracion-2.0 |
| BUG-05-01 | P1 | E1 | Esteban | [CU-053-01, CU-053-02, CU-053-04, CU-055-01, CU-055-04] Al guardar un producto (nuevo o editado) con datos válidos el sistema responde HTTP 405 y no guarda | Pendiente | | |
| BUG-05-02 | P3 | E8 | Esteban | [CU-052-04, CU-053-05, CU-054-05, CU-055-05] Los productos no tienen fecha de vencimiento ni advertencia de productos próximos a vencer | Pendiente | | |
| BUG-05-03 | P3 | G | Gerald | [CU-054-02] El buscador de inventario no encuentra productos por código SKU | Pendiente | | |
| BUG-05-04 | P3 | E6 | Esteban | [CU-056-02] El sistema permite inactivar un producto que todavía tiene stock en bodega | Pendiente | | |
| BUG-06-01 | P3 | E6 | Esteban | [CU-063-02] El sistema permite inactivar un cliente que tiene deuda pendiente | Pendiente | | |
| BUG-06-02 | P2 | E6 | Esteban | [CU-062-01, CU-062-02] El crédito del cliente no se usa en los pedidos: no existe pedido a crédito ni validación de pagos vencidos | Pendiente | | |
| BUG-06-03 | P3 | E6 | Esteban | [CU-061-07] El detalle del cliente no muestra sus facturas ni su saldo | Pendiente | | |
| BUG-06-04 | P3 | E4 | Esteban | [CU-063-01] Un cliente inactivo sigue apareciendo con sus pedidos pendientes al crear rutas | Pendiente | | |
| BUG-07-01 | P2 | E3 | Esteban | [CU-071-06] La app móvil del vendedor no permite crear un pedido sin conexión: la lista de clientes no carga | Pendiente | | Probable fix en restructuracion-2.0 |
| BUG-07-02 | P1 | E3 | Esteban | [CU-072-02, CU-072-04, CU-073-06] Los pedidos guardados sin conexión no se sincronizan: «Sincronizar ahora» queda en Error | Pendiente | | Probable fix en restructuracion-2.0 |
| BUG-08-01 | P3 | E3 | Esteban | [CU-081-04] Al asignar una ruta al chofer no se genera ninguna notificación; la web no tiene campana ni bandeja de notificaciones | Pendiente | | |
| BUG-09-01 | P3 | E5 | Esteban | [CU-092-02] El comprobante no se envía por mensajería digital: la factura solo ofrece correo | Pendiente | | |
| BUG-09-02 | P2 | E2 | Esteban | [CU-094-01, CU-094-04] El Gerente no tiene la opción Ingresos y Egresos en el menú y los demás accesos de Finanzas devuelven Acceso denegado | Pendiente | | Probable fix en restructuracion-2.0 |
| BUG-09-03 | P1 | E1 | Esteban | [CU-091-01] El detalle de cualquier pedido en Administración devuelve "No podemos completar esta solicitud" y no permite generar facturas | Pendiente | | |
| BUG-09-04 | P1 | E1 | Esteban | [CU-094-01, CU-094-02, CU-094-03, CU-094-04] La pantalla Ingresos y Egresos (/FinancialReportAdmin) devuelve "No podemos completar esta solicitud" con Administrador y Gerente | Pendiente | | |
| BUG-09-05 | P1 | E1 | Esteban | [CU-092-01] Registrar un pedido en Venta móvil devuelve "No podemos completar esta solicitud" e impide generar el comprobante de facturación  (en el PDF aparece duplicado también como P2) | Pendiente | | |
| BUG-10-01 | P3 | E4 | Esteban | [CU-101-01] La secuenciación automática de rutas ordena solo por cercanía y no considera la prioridad de los pedidos | Pendiente | | |
| BUG-10-02 | P3 | E4 | Esteban | [CU-104-01, CU-104-02] El tablero de entregas lista rutas y contadores, no los pedidos En ruta ni los pendientes de entrega | Pendiente | | |
| BUG-10-03 | P3 | E4 | Esteban | [CU-106-01] La liquidación de cobros cuadrada no genera ningún recibo | Pendiente | | |
| BUG-10-04 | P2 | E4 | Esteban | [CU-105-04] El sistema liquida una ruta que sigue Despachada, sin haber finalizado | Pendiente | | |
| BUG-10-05 | P2 | E4 | Esteban | [CU-106-02] El faltante de la liquidación no queda asociado al chofer | Pendiente | | |
| BUG-10-06 | P2 | E3 | Esteban | [CU-102-01, CU-102-03] El pedido no cambia de estado cuando el chofer lo marca En ruta o No entregado desde la app | Pendiente | | |
| BUG-10-07 | P2 | E4 | Esteban | [CU-105-01, CU-105-02] No existe la liquidación de mercadería de una ruta finalizada con pedidos no entregados | Pendiente | | |
| BUG-10-08 | P3 | E3 | Esteban | [CU-101-03] Al reasignar el chofer de una ruta, el nuevo chofer no recibe ninguna notificación | Pendiente | | |
| BUG-11-01 | P3 | E7 | Esteban | [CU-111-01] El formulario de empleado no tiene campo de identificación | Pendiente | | |
| BUG-11-02 | P2 | E1 | Esteban | [CU-112-01, CU-112-02, CU-112-05] No se puede calcular la planilla de un empleado con jornadas aprobadas: responde «Servicio temporalmente no disponible» | Pendiente | | |
| BUG-11-03 | P3 | E7 | Esteban | [CU-113-02] La planilla no calcula la comisión del vendedor: las comisiones se digitan a mano | Pendiente | | |
| BUG-11-04 | P2 | E7 | Esteban | [CU-113-06] Aprobar, pagar, revertir y notificar planillas no se registra en la auditoría | Pendiente | | |
| BUG-11-05 | P3 | E5 | Esteban | [CU-114-01] La boleta de pago se entrega como página HTML y no como PDF con el desglose | Pendiente | | |
| BUG-11-06 | P2 | E2 | Esteban | [CU-111-05] Un empleado inactivado conserva el acceso mientras su sesión sigue abierta | Pendiente | | Probable fix en restructuracion-2.0 |
| BUG-11-07 | P3 | E7 | Esteban | [CU-111-04] El ajuste salarial no permite indicar una fecha de vigencia | Pendiente | | |
| BUG-12-01 | P3 | E6 | Esteban | [CU-123-02, CU-124-01, CU-124-04] El portal del cliente no tiene lista de facturas ni muestra facturas y pagos en el estado de cuenta | Pendiente | | |
| BUG-12-02 | P3 | E5 | Esteban | [CU-123-03, CU-124-02, CU-124-03] El estado de cuenta y las facturas no se pueden descargar: el botón solo imprime la pantalla | Pendiente | | |
| BUG-12-03 | P2 | E6 | Esteban | [CU-122-07] El checkout acepta un pedido con más unidades que el stock disponible y solo descuenta el stock que existe | Pendiente | | Probable fix en restructuracion-2.0 |
| BUG-13-01 | P3 | D | David | [CU-133-03] No existe exportación del inventario ni un reporte de inventario en Reportes | Pendiente | | |
| BUG-13-02 | P3 | D | David | [CU-131-03] Los indicadores del tablero gerencial no se pueden seleccionar para ver gráficos detallados | Pendiente | | |
| BUG-13-03 | P3 | D | David | [CU-131-04] El tablero gerencial intercambia en silencio un rango con la fecha inicial mayor a la final, sin avisar | Pendiente | | |
| BUG-14-01 | P3 | D | David | [CU-141-04, CU-143-01] El historial de devoluciones no muestra el pedido asociado ni el cliente o referencia | Pendiente | | |
| BUG-14-02 | P3 | D | David | [CU-143-04] No existe un reporte ni exportación de devoluciones para analizar tendencias | Pendiente | | |
| BUG-14-03 | P1 | E1 | Esteban | [CU-144-01, CU-144-02, CU-144-03, CU-144-04, CU-144-05, CU-144-06, CU-164-03] El detalle de un pedido del portal del cliente responde «Servicio temporalmente no disponible» y bloquea la solicitud de garantía | Pendiente | | |
| BUG-15-01 | P3 | N | Danny | [CU-153-02] Una orden de mantenimiento correctivo En proceso no saca el vehículo de circulación | Pendiente | | |
| BUG-15-02 | P3 | N | Danny | [CU-153-04] El historial de mantenimientos no muestra la fecha de cada orden ni el próximo kilometraje | Pendiente | | |
| BUG-16-01 | P3 | E5 | Esteban | [CU-162-01] Al asignar un comodato no se emite el contrato de comodato | Pendiente | | |
| BUG-16-02 | P3 | N | Danny | [CU-162-03] El comodato no se bloquea por morosidad: el cliente se escribe como texto libre sin validar su deuda | Pendiente | | |
| BUG-16-03 | P3 | N | Danny | [CU-163-02] Al devolver un equipo con avería no se genera ninguna alerta al área de taller o cobros | Pendiente | | |
| BUG-16-04 | P3 | N | Danny | [CU-164-02] Rentabilidad de equipos no aplica la regla de 30 días ni genera una alerta de baja rentabilidad | Pendiente | | |
| BUG-16-05 | P3 | N | Danny | [CU-163-03] El historial de comodatos del activo no muestra la condición de entrega ni de devolución | Pendiente | | |
| BUG-16-06 | P3 | N | Danny | [CU-164-01] El reporte de equipos prestados no muestra la ubicación ni la fecha de entrega del equipo | Pendiente | | |
| BUG-16-07 | P2 | E2 | Esteban | [CU-164-01] El rol Auditor recibe «Acceso denegado» en el reporte de equipos prestados que la HU le asigna | Pendiente | | Probable fix en restructuracion-2.0 |
| BUG-17-01 | P2 | N | Danny | [CU-171-03] El sistema permite crear una segunda promoción del mismo producto con vigencia traslapada | Pendiente | | |
| BUG-17-02 | P3 | N | Danny | [CU-171-05] Las promociones no tienen límite de unidades ni se inactivan al alcanzarlo | Pendiente | | |
| BUG-18-01 | P3 | N | Danny | [CU-181-03] Al crear un combo sin productos o con un producto repetido no se muestra ningún mensaje de validación | Pendiente | | |
| BUG-20-01 | P3 | E8 | Esteban | [CU-202-01] Las alertas de vencimiento no se generan automáticamente: solo aparecen al presionar "Generar sin duplicar" | Pendiente | | |
| BUG-21-01 | P3 | D | David | [CU-211-03] Una nueva meta del mismo vendedor y mes sobrescribe la anterior sin advertencia ni confirmación | Pendiente | | |
| BUG-21-02 | P3 | D | David | [CU-213-04] El Reporte de KPIs no tiene opción para exportar a Excel (.xlsx) | Pendiente | | |
| BUG-21-03 | P3 | D | David | [CU-213-01] El cumplimiento global del Reporte de KPIs suma ventas de un vendedor sin meta | Pendiente | | |
| BUG-22-01 | P1 | E1 | Esteban | [CU-221-01, CU-221-02, CU-221-04, CU-221-05, CU-221-06, CU-222-01, CU-222-03, CU-223-01] La pantalla de detalle de cualquier presupuesto devuelve HTTP 503 "No podemos cargar este presupuesto" | Pendiente | | |
| BUG-22-02 | P2 | E2 | Esteban | [CU-222-01, CU-223-01] El Gerente ve el menú Finanzas pero Presupuestos, Gastos y Presupuesto vs. real responden 403 Acceso denegado | Pendiente | | Probable fix en restructuracion-2.0 |
| BUG-22-03 | P3 | D | David | [CU-222-02] Un gasto sin presupuesto disponible se registra con "Nivel: Normal (0.00%)" y sin alerta de Presupuesto Excedido | Pendiente | | |
| BUG-22-04 | P3 | D | David | [CU-223-03] El comparativo Presupuesto vs. real exporta un CSV solo con el detalle de gastos, no un Excel (.xlsx) con los datos de pantalla | Pendiente | | |
| BUG-23-01 | P3 | G | Gerald | [CU-231-01] Los mensajes directos del chat no muestran doble check cuando el destinatario los recibe | Pendiente | | |
| BUG-23-02 | P3 | G | Gerald | [CU-231-03] El chat no muestra ningún aviso al enviar un mensaje vacío o solo con espacios | Pendiente | | |
| BUG-23-03 | P3 | G | Gerald | [CU-233-03] La búsqueda de un solo carácter muestra "No se encontraron coincidencias." en lugar de pedir al menos dos caracteres | Pendiente | | |
| BUG-23-04 | P3 | G | Gerald | [CU-233-05] Los resultados de la búsqueda del chat no tienen opción para exportar a Excel (.xlsx) | Pendiente | | |
| BUG-23-05 | P3 | E3 | Esteban | [CU-231-02] No se envía notificación push al celular del receptor de un mensaje directo (la app móvil no tiene chat ni notificaciones) | Pendiente | | |
| BUG-24-01 | P3 | E8 | Esteban | [CU-241-02] Un producto sin historial de ventas se etiqueta "Sin ventas suficientes" y no solicita ingreso manual de la cantidad | Pendiente | | |
| BUG-24-02 | P2 | E8 | Esteban | [CU-241-03] Las Sugerencias de compra muestran promedio mensual 0.0 y cantidad 1 en productos con ventas, contradiciendo Inteligencia de inventario | Pendiente | | |
| BUG-24-03 | P3 | E8 | Esteban | [CU-242-01, CU-242-02] No existe la alerta gerencial de estancamiento ni la sugerencia de incluir el producto en promociones | Pendiente | | |
| BUG-24-04 | P3 | E8 | Esteban | [CU-243-01] La Tendencia estacional no tiene filtro por categoría de producto | Pendiente | | |
| BUG-24-05 | P3 | E8 | Esteban | [CU-243-02] Los totales de la Tendencia estacional no coinciden con el Reporte de ventas del mismo mes | Pendiente | | |
| BUG-24-06 | P3 | E8 | Esteban | [CU-243-03] La Tendencia estacional acepta Año inicial mayor que Año final y rangos de más de 10 años sin mensaje de validación | Pendiente | | |
| BUG-24-07 | P3 | E8 | Esteban | [CU-243-04] La Tendencia estacional no tiene opción para exportar a Excel (.xlsx) | Pendiente | | |
| BUG-25-01 | P3 | E4 | Esteban | [CU-251-02] Secuenciar una ruta con clientes sin coordenadas la procesa en lugar de pedir fijar el punto antes | Pendiente | | |
| BUG-25-02 | P3 | E4 | Esteban | [CU-251-01, CU-253-01] Al secuenciar una ruta no se muestra el tiempo total estimado | Pendiente | | |
| BUG-25-03 | P3 | E4 | Esteban | [CU-253-05] El detalle de la ruta no tiene opción para exportar la hoja de ruta a Excel (.xlsx) | Pendiente | | |
| BUG-25-04 | P3 | E3 | Esteban | [CU-252-01] La app móvil del chofer no muestra el mapa interactivo con la secuencia de visitas en la ruta | Pendiente | | |
| BUG-26-01 | P3 | E9 | Esteban | [CU-261-03] El Asistente no muestra el mensaje "Escribe una pregunta." al enviar una pregunta vacía | Pendiente | | |
| BUG-26-02 | P3 | E9 | Esteban | [CU-263-01, CU-263-02] El Asistente responde "¿Cómo registro una devolución?" y "¿Cómo creo una ruta?" con estadísticas en lugar del paso a paso | Pendiente | | |
| BUG-26-03 | P3 | E9 | Esteban | [CU-263-03] El Asistente no tiene opción para exportar la conversación a Excel (.xlsx) | Pendiente | | |
| BUG-26-04 | P3 | E9 | Esteban | [CU-261-06] El resumen del Asistente indica "2 entregas pendientes" cuando las rutas despachadas tienen 6 paradas pendientes | Pendiente | | |
| BUG-26-05 | P3 | E9 | Esteban | [CU-262-01, CU-262-02] Venta móvil no sugiere ningún artículo complementario al agregar un producto al pedido | Pendiente | | |
| BUG-26-06 | P3 | E9 | Esteban | [CU-261-04] El Asistente no entiende "¿Cuánto se vendió este mes?" y no muestra "Información restringida" al Chofer | Pendiente | | |
