# Histórico de precios {#mod-historico-precios}

Este reporte muestra a qué precio se ha comprado un producto en cada orden de compra recibida, con qué proveedor y cuánto varió respecto de la compra anterior.

Se abre en **Administración → Finanzas → Histórico de precios**.

## Consultar el historial de compra de un producto {#mod-historico-precios-consultar}

### Para qué sirve

Detectar aumentos de precio de los proveedores y comparar proveedores antes de hacer una compra o de ajustar el precio de venta.

### Quién puede usarlo

Administrador. El puesto Compras no tiene hoy el permiso **Consultar histórico de precios**, por eso esta opción no aparece en su panel; si lo necesita, asígnelo en [Permisos](#mod-permisos).

### Pasos

1. Abra **Administración → Finanzas → Histórico de precios**.
2. Elija el producto:

   ![Historial de precios de un producto](capturas/administrador/adm-historico-01.png)

   1. Elija el producto en la lista.
   2. Haga clic en **Consultar**.

### Resultado esperado

La tabla muestra cada compra, de la más reciente a la más antigua: **Fecha**, **Proveedor**, **Precio**, **Precio anterior**, **Variación** y la **Orden / estado** (haga clic en el número para abrir la orden).

Las variaciones mayores al 15 %, para arriba o para abajo, se resaltan en amarillo para que las revise. En el ejemplo, el Cacique Superior 1L subió de ₡5 200 a ₡6 100 (17,31 %).

> [!NOTA] El reporte solo informa: no cambia ningún precio. Si decide subir el precio de venta, hágalo en [Inventario](#mod-inventario-editar).

### Si algo sale mal

| Situación | Qué hacer |
|---|---|
| La tabla no aparece después de **Consultar**. | Ese producto todavía no tiene compras recibidas. |
| 403 · Acceso denegado | Su puesto no tiene el permiso **Consultar histórico de precios**. Pida al Administrador que lo revise en [Permisos](#mod-permisos). |
