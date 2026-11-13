# Ingresos y egresos {#mod-ingresos-egresos}

Este reporte junta en una sola lista el dinero que entró (las ventas facturadas) y el que salió (gastos operativos aprobados o pagados) en un período, con el balance entre los dos.

Se abre en **Administración → Finanzas → Ingresos y Egresos**.

## Consultar los ingresos y egresos de un período {#mod-ingresos-egresos-consultar}

### Para qué sirve

Conocer el flujo de dinero de la empresa en un rango de fechas: cuánto se vendió, cuánto se gastó y cuál fue el resultado.

### Quién puede usarlo

Administrador. Gerente y Cajero tienen permiso de consulta (permiso **Ver ingresos y egresos**).

### Pasos

1. Abra **Administración → Finanzas → Ingresos y Egresos**.
2. Elija el período:

   ![Fechas del reporte de ingresos y egresos](capturas/administrador/adm-ingresos-01.png)

   1. **Fecha inicial**: el primer día del período.
   2. **Fecha final**: el último día (incluido).
   3. Haga clic en **Filtrar**.

### Resultado esperado

Las tarjetas muestran el **Total de ingresos**, el **Total de egresos** y el **Balance** (ingresos menos egresos). Una leyenda debajo de las tarjetas explica de dónde sale cada cifra. La tabla **Movimientos financieros** lista cada movimiento, del más reciente al más antiguo:

![Totales y movimientos del período](capturas/administrador/adm-ingresos-02.png)

| Tipo | De dónde sale | Referencia |
|---|---|---|
| Ingreso | Una factura no anulada (**Venta facturada**), con el IVA incluido. Es la misma cifra del [Reporte de ventas](#mod-reporte-ventas) y del [Tablero gerencial](#mod-tablero-gerencial). | Número de factura, número de pedido y forma de pago. |
| Egreso | Un gasto operativo **Aprobado** o **Pagado**, según su fecha de gasto. | Número de gasto y categoría. |

Si deja las fechas vacías, el reporte incluye todos los movimientos registrados.

> [!NOTA] Las ventas cuentan como ingreso desde que se factura el pedido, sea a crédito, en efectivo o con tarjeta. Para ver si ya se cobraron, consulte [Cuentas por cobrar](#mod-cxc) y [Liquidación de cobros](#mod-liquidacion).

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No hay movimientos registrados. | No hay facturas ni gastos aprobados en esas fechas. Amplíe el período. |
