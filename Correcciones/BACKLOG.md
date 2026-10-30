# Lista de errores a corregir

Fuente: `Entregas/G8_SC803_J_BugReport.pdf` · detalle en [`fuentes/BugReport_G8_SC803_J.txt`](fuentes/BugReport_G8_SC803_J.txt) · plan en [`PLAN_CORRECCIONES.md`](PLAN_CORRECCIONES.md) · reparto en `Reparto_Correcciones.xlsx` (OneDrive).

Estados: `Pendiente` → `En proceso` → `Corregido` (unido a `main`) → `Verificado` (re-probado). `Bloqueado` con nota si depende de otra persona.

Cada persona actualiza **solo sus filas**, en el mismo PR de la corrección.

| Paquete | Responsable | Errores | Esfuerzo |
|---|---|---:|---:|
| 1 | Esteban | 44 | 96 |
| 2 | Danny | 16 | 36 |
| 3 | Gerald | 14 | 25 |
| 4 | David | 17 | 26 |

**Total 91** — P1: 7 · P2: 19 · P3: 65.

| Error | Prio | Paquete | Responsable | Área | Título | Dificultad | Estado | Rama / PR | Nota |
|---|---|---|---|---|---|---|---|---|---|
| BUG-01-01 | P2 | 1 | Esteban | Recuperación de contraseña y correos | No llega el correo con el enlace de recuperación de contraseña | Alta | Pendiente | | |
| BUG-01-02 | P3 | 1 | Esteban | Recuperación de contraseña y correos | La recuperación de contraseña no indica que el correo no está registrado | Baja | Pendiente | | |
| BUG-01-03 | P3 | 1 | Esteban | Recuperación de contraseña y correos | Al superar el límite de solicitudes de recuperación el sistema muestra una página en blanco con error 429 | Baja | Pendiente | | |
| BUG-02-01 | P3 | 3 | Gerald | Mi perfil | Ante una falla de conexión, Mi Perfil no muestra una vista ni un mensaje de error controlado | Media | Pendiente | | |
| BUG-02-02 | P3 | 3 | Gerald | Mi perfil | El campo Teléfono de Mi Perfil acepta más de 10 dígitos | Baja | Pendiente | | |
| BUG-03-01 | P2 | 1 | Esteban | Recuperación de contraseña y correos | El sistema no envía correos: ni la confirmación de consultas, ni el comprobante de pedidos, ni la recuperación de contraseña | Alta | Pendiente | | |
| BUG-04-01 | P2 | 2 | Danny | Roles y permisos | El sistema permite inactivar un rol que tiene usuarios asignados | Media | Pendiente | | |
| BUG-04-02 | P3 | 2 | Danny | Roles y permisos | El rol Administrador no tiene asignados todos los permisos: muestra 98 de 99 | Baja | Pendiente | | |
| BUG-04-03 | P2 | 2 | Danny | Roles y permisos | Un usuario de un rol inactivo conserva el acceso a la Planilla | Alta | Pendiente | | |
| BUG-05-01 | P1 | 1 | Esteban | Productos e inventario | Al guardar un producto (nuevo o editado) con datos válidos el sistema responde HTTP 405 y no guarda | Alta | Pendiente | | |
| BUG-05-02 | P3 | 1 | Esteban | Productos e inventario | Los productos no tienen fecha de vencimiento ni advertencia de productos próximos a vencer | Alta | Pendiente | | |
| BUG-05-03 | P3 | 1 | Esteban | Productos e inventario | El buscador de inventario no encuentra productos por código SKU | Baja | Pendiente | | |
| BUG-05-04 | P3 | 1 | Esteban | Productos e inventario | El sistema permite inactivar un producto que todavía tiene stock en bodega | Media | Pendiente | | |
| BUG-06-01 | P3 | 1 | Esteban | Clientes y crédito | El sistema permite inactivar un cliente que tiene deuda pendiente | Media | Pendiente | | |
| BUG-06-02 | P2 | 1 | Esteban | Clientes y crédito | El crédito del cliente no se usa en los pedidos: no existe pedido a crédito ni validación de pagos vencidos | Alta | Pendiente | | |
| BUG-06-03 | P3 | 1 | Esteban | Clientes y crédito | El detalle del cliente no muestra sus facturas ni su saldo | Media | Pendiente | | |
| BUG-06-04 | P3 | 3 | Gerald | Rutas, entregas y liquidaciones | Un cliente inactivo sigue apareciendo con sus pedidos pendientes al crear rutas | Baja | Pendiente | | |
| BUG-07-01 | P2 | 1 | Esteban | App móvil y notificaciones | La app móvil del vendedor no permite crear un pedido sin conexión: la lista de clientes no carga | Alta | Pendiente | | |
| BUG-07-02 | P1 | 1 | Esteban | App móvil y notificaciones | Los pedidos guardados sin conexión no se sincronizan: «Sincronizar ahora» queda en Error | Alta | Pendiente | | |
| BUG-08-01 | P3 | 1 | Esteban | App móvil y notificaciones | Al asignar una ruta al chofer no se genera ninguna notificación; la web no tiene campana ni bandeja de notificaciones | Alta | Pendiente | | |
| BUG-09-01 | P3 | 1 | Esteban | Pedidos y facturación | El comprobante no se envía por mensajería digital: la factura solo ofrece correo | Media | Pendiente | | |
| BUG-09-02 | P2 | 2 | Danny | Roles y permisos | El Gerente no tiene la opción Ingresos y Egresos en el menú y los demás accesos de Finanzas devuelven Acceso denegado | Media | Pendiente | | |
| BUG-09-03 | P1 | 1 | Esteban | Pedidos y facturación | El detalle de cualquier pedido en Administración devuelve "No podemos completar esta solicitud" y no permite generar facturas | Alta | Pendiente | | |
| BUG-09-04 | P1 | 1 | Esteban | Pedidos y facturación | La pantalla Ingresos y Egresos (/FinancialReportAdmin) devuelve "No podemos completar esta solicitud" con Administrador y Gerente | Alta | Pendiente | | |
| BUG-09-05 | P1 | 1 | Esteban | Pedidos y facturación | Registrar un pedido en Venta móvil devuelve "No podemos completar esta solicitud" e impide generar el comprobante de facturación  | Alta | Pendiente | | |
| BUG-10-01 | P3 | 3 | Gerald | Rutas, entregas y liquidaciones | La secuenciación automática de rutas ordena solo por cercanía y no considera la prioridad de los pedidos | Media | Pendiente | | |
| BUG-10-02 | P3 | 3 | Gerald | Rutas, entregas y liquidaciones | El tablero de entregas lista rutas y contadores, no los pedidos En ruta ni los pendientes de entrega | Media | Pendiente | | |
| BUG-10-03 | P3 | 3 | Gerald | Rutas, entregas y liquidaciones | La liquidación de cobros cuadrada no genera ningún recibo | Media | Pendiente | | |
| BUG-10-04 | P2 | 3 | Gerald | Rutas, entregas y liquidaciones | El sistema liquida una ruta que sigue Despachada, sin haber finalizado | Media | Pendiente | | |
| BUG-10-05 | P2 | 3 | Gerald | Rutas, entregas y liquidaciones | El faltante de la liquidación no queda asociado al chofer | Media | Pendiente | | |
| BUG-10-06 | P2 | 1 | Esteban | App móvil y notificaciones | El pedido no cambia de estado cuando el chofer lo marca En ruta o No entregado desde la app | Alta | Pendiente | | |
| BUG-10-07 | P2 | 3 | Gerald | Rutas, entregas y liquidaciones | No existe la liquidación de mercadería de una ruta finalizada con pedidos no entregados | Alta | Pendiente | | |
| BUG-10-08 | P3 | 1 | Esteban | App móvil y notificaciones | Al reasignar el chofer de una ruta, el nuevo chofer no recibe ninguna notificación | Media | Pendiente | | |
| BUG-11-01 | P3 | 2 | Danny | Empleados y planilla | El formulario de empleado no tiene campo de identificación | Media | Pendiente | | |
| BUG-11-02 | P2 | 2 | Danny | Empleados y planilla | No se puede calcular la planilla de un empleado con jornadas aprobadas: responde «Servicio temporalmente no disponible» | Alta | Pendiente | | |
| BUG-11-03 | P3 | 2 | Danny | Empleados y planilla | La planilla no calcula la comisión del vendedor: las comisiones se digitan a mano | Alta | Pendiente | | |
| BUG-11-04 | P2 | 2 | Danny | Empleados y planilla | Aprobar, pagar, revertir y notificar planillas no se registra en la auditoría | Media | Pendiente | | |
| BUG-11-05 | P3 | 2 | Danny | Empleados y planilla | La boleta de pago se entrega como página HTML y no como PDF con el desglose | Alta | Pendiente | | |
| BUG-11-06 | P2 | 2 | Danny | Empleados y planilla | Un empleado inactivado conserva el acceso mientras su sesión sigue abierta | Alta | Pendiente | | |
| BUG-11-07 | P3 | 2 | Danny | Empleados y planilla | El ajuste salarial no permite indicar una fecha de vigencia | Media | Pendiente | | |
| BUG-12-01 | P3 | 1 | Esteban | Carrito, compra en línea y portal del cliente | El portal del cliente no tiene lista de facturas ni muestra facturas y pagos en el estado de cuenta | Media | Pendiente | | |
| BUG-12-02 | P3 | 1 | Esteban | Carrito, compra en línea y portal del cliente | El estado de cuenta y las facturas no se pueden descargar: el botón solo imprime la pantalla | Media | Pendiente | | |
| BUG-12-03 | P2 | 1 | Esteban | Carrito, compra en línea y portal del cliente | El checkout acepta un pedido con más unidades que el stock disponible y solo descuenta el stock que existe | Alta | Pendiente | | |
| BUG-13-01 | P3 | 4 | David | Reportes y tablero gerencial | No existe exportación del inventario ni un reporte de inventario en Reportes | Media | Pendiente | | |
| BUG-13-02 | P3 | 4 | David | Reportes y tablero gerencial | Los indicadores del tablero gerencial no se pueden seleccionar para ver gráficos detallados | Media | Pendiente | | |
| BUG-13-03 | P3 | 4 | David | Reportes y tablero gerencial | El tablero gerencial intercambia en silencio un rango con la fecha inicial mayor a la final, sin avisar | Baja | Pendiente | | |
| BUG-14-01 | P3 | 1 | Esteban | Devoluciones y garantías | El historial de devoluciones no muestra el pedido asociado ni el cliente o referencia | Baja | Pendiente | | |
| BUG-14-02 | P3 | 1 | Esteban | Devoluciones y garantías | No existe un reporte ni exportación de devoluciones para analizar tendencias | Media | Pendiente | | |
| BUG-14-03 | P1 | 1 | Esteban | Carrito, compra en línea y portal del cliente | El detalle de un pedido del portal del cliente responde «Servicio temporalmente no disponible» y bloquea la solicitud de garantía | Alta | Pendiente | | |
| BUG-15-01 | P3 | 3 | Gerald | Flota y mantenimiento | Una orden de mantenimiento correctivo En proceso no saca el vehículo de circulación | Media | Pendiente | | |
| BUG-15-02 | P3 | 3 | Gerald | Flota y mantenimiento | El historial de mantenimientos no muestra la fecha de cada orden ni el próximo kilometraje | Baja | Pendiente | | |
| BUG-16-01 | P3 | 1 | Esteban | Activos y comodatos (equipos prestados a clientes) | Al asignar un comodato no se emite el contrato de comodato | Alta | Pendiente | | |
| BUG-16-02 | P3 | 1 | Esteban | Activos y comodatos (equipos prestados a clientes) | El comodato no se bloquea por morosidad: el cliente se escribe como texto libre sin validar su deuda | Media | Pendiente | | |
| BUG-16-03 | P3 | 1 | Esteban | Activos y comodatos (equipos prestados a clientes) | Al devolver un equipo con avería no se genera ninguna alerta al área de taller o cobros | Media | Pendiente | | |
| BUG-16-04 | P3 | 1 | Esteban | Activos y comodatos (equipos prestados a clientes) | Rentabilidad de equipos no aplica la regla de 30 días ni genera una alerta de baja rentabilidad | Media | Pendiente | | |
| BUG-16-05 | P3 | 1 | Esteban | Activos y comodatos (equipos prestados a clientes) | El historial de comodatos del activo no muestra la condición de entrega ni de devolución | Baja | Pendiente | | |
| BUG-16-06 | P3 | 1 | Esteban | Activos y comodatos (equipos prestados a clientes) | El reporte de equipos prestados no muestra la ubicación ni la fecha de entrega del equipo | Baja | Pendiente | | |
| BUG-16-07 | P2 | 2 | Danny | Roles y permisos | El rol Auditor recibe «Acceso denegado» en el reporte de equipos prestados que la HU le asigna | Baja | Pendiente | | |
| BUG-17-01 | P2 | 1 | Esteban | Promociones y combos | El sistema permite crear una segunda promoción del mismo producto con vigencia traslapada | Media | Pendiente | | |
| BUG-17-02 | P3 | 1 | Esteban | Promociones y combos | Las promociones no tienen límite de unidades ni se inactivan al alcanzarlo | Media | Pendiente | | |
| BUG-18-01 | P3 | 1 | Esteban | Promociones y combos | Al crear un combo sin productos o con un producto repetido no se muestra ningún mensaje de validación | Baja | Pendiente | | |
| BUG-20-01 | P3 | 1 | Esteban | Productos e inventario | Las alertas de vencimiento no se generan automáticamente: solo aparecen al presionar "Generar sin duplicar" | Media | Pendiente | | |
| BUG-21-01 | P3 | 4 | David | Metas y desempeño de vendedores | Una nueva meta del mismo vendedor y mes sobrescribe la anterior sin advertencia ni confirmación | Baja | Pendiente | | |
| BUG-21-02 | P3 | 4 | David | Metas y desempeño de vendedores | El Reporte de KPIs no tiene opción para exportar a Excel (.xlsx) | Baja | Pendiente | | |
| BUG-21-03 | P3 | 4 | David | Metas y desempeño de vendedores | El cumplimiento global del Reporte de KPIs suma ventas de un vendedor sin meta | Media | Pendiente | | |
| BUG-22-01 | P1 | 2 | Danny | Presupuestos y gastos | La pantalla de detalle de cualquier presupuesto devuelve HTTP 503 "No podemos cargar este presupuesto" | Alta | Pendiente | | |
| BUG-22-02 | P2 | 2 | Danny | Presupuestos y gastos | El Gerente ve el menú Finanzas pero Presupuestos, Gastos y Presupuesto vs. real responden 403 Acceso denegado | Media | Pendiente | | |
| BUG-22-03 | P3 | 2 | Danny | Presupuestos y gastos | Un gasto sin presupuesto disponible se registra con "Nivel: Normal (0.00%)" y sin alerta de Presupuesto Excedido | Media | Pendiente | | |
| BUG-22-04 | P3 | 2 | Danny | Presupuestos y gastos | El comparativo Presupuesto vs. real exporta un CSV solo con el detalle de gastos, no un Excel (.xlsx) con los datos de pantalla | Media | Pendiente | | |
| BUG-23-01 | P3 | 4 | David | Chat interno | Los mensajes directos del chat no muestran doble check cuando el destinatario los recibe | Media | Pendiente | | |
| BUG-23-02 | P3 | 4 | David | Chat interno | El chat no muestra ningún aviso al enviar un mensaje vacío o solo con espacios | Baja | Pendiente | | |
| BUG-23-03 | P3 | 4 | David | Chat interno | La búsqueda de un solo carácter muestra "No se encontraron coincidencias." en lugar de pedir al menos dos caracteres | Baja | Pendiente | | |
| BUG-23-04 | P3 | 4 | David | Chat interno | Los resultados de la búsqueda del chat no tienen opción para exportar a Excel (.xlsx) | Baja | Pendiente | | |
| BUG-23-05 | P3 | 1 | Esteban | App móvil y notificaciones | No se envía notificación push al celular del receptor de un mensaje directo (la app móvil no tiene chat ni notificaciones) | Alta | Pendiente | | |
| BUG-24-01 | P3 | 4 | David | Sugerencias de compra y tendencias | Un producto sin historial de ventas se etiqueta "Sin ventas suficientes" y no solicita ingreso manual de la cantidad | Media | Pendiente | | |
| BUG-24-02 | P2 | 4 | David | Sugerencias de compra y tendencias | Las Sugerencias de compra muestran promedio mensual 0.0 y cantidad 1 en productos con ventas, contradiciendo Inteligencia de inventario | Alta | Pendiente | | |
| BUG-24-03 | P3 | 4 | David | Sugerencias de compra y tendencias | No existe la alerta gerencial de estancamiento ni la sugerencia de incluir el producto en promociones | Media | Pendiente | | |
| BUG-24-04 | P3 | 4 | David | Sugerencias de compra y tendencias | La Tendencia estacional no tiene filtro por categoría de producto | Baja | Pendiente | | |
| BUG-24-05 | P3 | 4 | David | Sugerencias de compra y tendencias | Los totales de la Tendencia estacional no coinciden con el Reporte de ventas del mismo mes | Media | Pendiente | | |
| BUG-24-06 | P3 | 4 | David | Sugerencias de compra y tendencias | La Tendencia estacional acepta Año inicial mayor que Año final y rangos de más de 10 años sin mensaje de validación | Baja | Pendiente | | |
| BUG-24-07 | P3 | 4 | David | Sugerencias de compra y tendencias | La Tendencia estacional no tiene opción para exportar a Excel (.xlsx) | Baja | Pendiente | | |
| BUG-25-01 | P3 | 3 | Gerald | Orden de visitas de las rutas | Secuenciar una ruta con clientes sin coordenadas la procesa en lugar de pedir fijar el punto antes | Media | Pendiente | | |
| BUG-25-02 | P3 | 3 | Gerald | Orden de visitas de las rutas | Al secuenciar una ruta no se muestra el tiempo total estimado | Media | Pendiente | | |
| BUG-25-03 | P3 | 3 | Gerald | Orden de visitas de las rutas | El detalle de la ruta no tiene opción para exportar la hoja de ruta a Excel (.xlsx) | Baja | Pendiente | | |
| BUG-25-04 | P3 | 1 | Esteban | App móvil y notificaciones | La app móvil del chofer no muestra el mapa interactivo con la secuencia de visitas en la ruta | Alta | Pendiente | | |
| BUG-26-01 | P3 | 1 | Esteban | Asistente virtual y sugerencias en Venta móvil | El Asistente no muestra el mensaje "Escribe una pregunta." al enviar una pregunta vacía | Baja | Pendiente | | |
| BUG-26-02 | P3 | 1 | Esteban | Asistente virtual y sugerencias en Venta móvil | El Asistente responde "¿Cómo registro una devolución?" y "¿Cómo creo una ruta?" con estadísticas en lugar del paso a paso | Media | Pendiente | | |
| BUG-26-03 | P3 | 1 | Esteban | Asistente virtual y sugerencias en Venta móvil | El Asistente no tiene opción para exportar la conversación a Excel (.xlsx) | Baja | Pendiente | | |
| BUG-26-04 | P3 | 1 | Esteban | Asistente virtual y sugerencias en Venta móvil | El resumen del Asistente indica "2 entregas pendientes" cuando las rutas despachadas tienen 6 paradas pendientes | Media | Pendiente | | |
| BUG-26-05 | P3 | 1 | Esteban | Asistente virtual y sugerencias en Venta móvil | Venta móvil no sugiere ningún artículo complementario al agregar un producto al pedido | Media | Pendiente | | |
| BUG-26-06 | P3 | 1 | Esteban | Asistente virtual y sugerencias en Venta móvil | El Asistente no entiende "¿Cuánto se vendió este mes?" y no muestra "Información restringida" al Chofer | Media | Pendiente | | |
