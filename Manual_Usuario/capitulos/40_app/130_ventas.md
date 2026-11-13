# Ventas en campo {#app-ventas}

**Ventas en campo** es la venta móvil en la aplicación: el vendedor toma el pedido del cliente en tres pasos, incluso sin señal, y sigue su estado.

**Quién la ve:** Vendedor, Empleado y Administrador (sección **Ventas**). En el sitio web, vea [Venta móvil (web)](#mod-venta-movil).

## Registrar un pedido {#app-ventas-registrar}

### Para qué sirve

Dejar en firme el pedido del cliente durante la visita.

### Quién puede usarlo

Vendedor, Empleado y Administrador.

### Pasos

1. En la pantalla de inicio, toque **Ventas en campo**. Verá **Mis últimas ventas** con su estado y su factura. Toque **Nuevo pedido**.

   ![Mis últimas ventas](capturas/app/app-ventas-01.png "ancho=6")

2. **Paso 1. Cliente**:

   ![Paso 1: elegir el cliente](capturas/app/app-ventas-02.png "ancho=6")

   1. **Buscar cliente por nombre, correo o teléfono**: escriba para filtrar.
   2. Toque el cliente. La aplicación pasa sola al paso 2.

3. **Paso 2. Productos**:

   ![Paso 2: agregar productos](capturas/app/app-ventas-03.png "ancho=6")

   1. **Buscar producto o categoría**: escriba para filtrar. Solo aparecen productos activos con stock.
   2. Toque el botón redondo **+** (Agregar) de cada producto. Luego use **−** y **+** para cambiar la cantidad.
   3. Toque **Continuar**. La barra inferior muestra las unidades y el total.

4. **Paso 3. Entrega**: revise el resumen (puede tocar **Cambiar** para cambiar de cliente) y complete:

   ![Paso 3: entrega](capturas/app/app-ventas-04.png "ancho=6")

   1. El tipo de entrega: **Entrega por vendedor**, **Retiro en local** o **Envío a domicilio**.
   2. **Dirección de entrega**: viene con la dirección del cliente. Corríjala si hace falta.
   3. **Observaciones (opcional)**.
   4. Toque **Registrar**.

5. Revise el resumen y toque **Registrar** para confirmar.

   ![Confirmar el pedido](capturas/app/app-ventas-05.png "ancho=6")

### Resultado esperado

Con señal aparece «Pedido enviado. El estado aparece en sus ventas.». Sin señal, «Pedido guardado. Se envía al recuperar señal.» y el pedido espera en [Por enviar](#app-por-enviar).

El pedido aparece en **Mis últimas ventas** con su estado. Igual que en la web, si supera ₡50 000 antes de impuestos queda **Retenido** hasta que el gerente lo apruebe; si no, se factura de inmediato.

> [!NOTA] En la aplicación todos los pedidos son de pago contra entrega. Para vender a crédito o enviar el comprobante por WhatsApp, use [Venta móvil (web)](#mod-venta-movil).

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Sin clientes — Ningún cliente activo coincide con la búsqueda. | Revise lo escrito. Si el cliente es nuevo, pida a administración que lo registre. |
| Sin productos — Solo se muestran productos activos con stock. | El producto está agotado o inactivo. |
| Indique la dirección de entrega. | Escriba la dirección. |
| Agregá al menos un producto. | Vuelva al paso 2 y agregue productos. |
| Un pedido admite hasta 60 productos. | Divida el pedido en dos. |
| En **Por enviar**: «No hay stock suficiente para uno o más productos.» | Otro vendedor se llevó el stock mientras usted estaba sin señal. Confirme con el cliente y registre un pedido nuevo con lo disponible. |
