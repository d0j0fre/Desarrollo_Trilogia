# Compras {#app-compras}

**Compras** muestra las órdenes de compra a proveedores y una sugerencia de qué productos reponer según las ventas.

**Quién la ve:** Compras y Administrador (sección **Oficina**). Es de consulta. Las órdenes se crean en el sitio web (vea [Órdenes de compra](#mod-ordenes-compra)); la mercadería se recibe en [Recepción de compras](#app-recepcion).

## Consultar órdenes de compra {#app-compras-ordenes}

### Para qué sirve

Ver qué está pedido a cada proveedor y cuánto ha llegado.

### Quién puede usarlo

Compras y Administrador.

### Pasos

1. En la pantalla de inicio, toque **Compras**.

   ![Órdenes de compra](capturas/app/app-compras-01.png "ancho=6")

   1. Pestaña **Órdenes**.
   2. Pestaña **Sugerencias** (vea [Ver sugerencias de reposición](#app-compras-sugerencias)).
   3. Filtros: **Abiertas**, **Todas**, **Recibida**, **Con discrepancia**, **Cancelada**.
   4. Toque una orden para ver sus líneas: lo pedido, lo recibido y lo pendiente.

### Resultado esperado

Cada orden muestra el proveedor, el estado, las unidades recibidas, el monto y la fecha.

## Ver sugerencias de reposición {#app-compras-sugerencias}

### Para qué sirve

Saber qué productos conviene pedir y cuántas unidades, antes de armar la orden.

### Quién puede usarlo

Compras y Administrador.

### Pasos

1. En **Compras**, toque la pestaña **Sugerencias**.

   ![Sugerencias de reposición](capturas/app/app-compras-02.png "ancho=6")

### Resultado esperado

Cada producto muestra su stock, el mínimo, cuánto se vende por mes y la cantidad **sugerido**. El cálculo usa las ventas de los últimos 3 meses para cubrir 2 meses más el stock mínimo.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Nada que reponer | Con las ventas recientes, el stock alcanza para dos meses más. |
| Sin órdenes de compra — No hay órdenes con este filtro. | Cambie el filtro. |
