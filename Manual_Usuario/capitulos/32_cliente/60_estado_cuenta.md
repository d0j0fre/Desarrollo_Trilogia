# Estado de cuenta {#mod-estado-cuenta}

El **Estado de cuenta** reúne en una sola página su crédito, sus facturas, sus cargos y pagos, y su historial de pedidos. Lo puede imprimir o descargar en PDF.

## Consultar el estado de cuenta {#mod-estado-cuenta-consultar}

### Para qué sirve

Saber cuánto debe, cuánto tiene vencido y cuánto crédito le queda disponible, con el detalle de cada movimiento.

### Quién puede usarlo

Clientes con sesión iniciada. La parte de crédito tiene datos solo si la empresa le aprobó crédito.

### Pasos

1. En el menú superior, haga clic en **Mi cuenta → Estado de cuenta** (o en **Ver estado de cuenta** dentro de **Mis pedidos**).
2. Revise la información:

   ![Estado de cuenta de un cliente mayorista con crédito](capturas/cliente/cli-estado-01.png)

   1. **Descargar PDF**: guarda el estado de cuenta en su equipo (vea [Descargar el estado de cuenta en PDF](#mod-estado-cuenta-pdf)).
   2. **Imprimir**: lo imprime tal como se ve.
   3. **Crédito**: **Límite**, **Saldo pendiente** (lo que debe), **Saldo vencido** (en rojo si hay) y **Disponible** para nuevas compras.
   4. **Facturas**: cada factura con su forma de pago y total. Haga clic en el número para verla.
   5. **Cargos y pagos**: cada compra a crédito (**Cargo**), cada **Pago** y los ajustes, con el saldo después de cada uno.
   6. **Historial de pedidos**: todos sus pedidos con su estado.

### Resultado esperado

Los montos del estado de cuenta son los mismos que tiene registrados el departamento de Crédito y Cobro.

Cómo se calcula lo vencido:

- Cada compra a crédito vence a los días de plazo que le dio la empresa (normalmente 30 días).
- Sus pagos se aplican primero a las compras más antiguas.
- **Saldo vencido** es lo que queda sin pagar de las compras que ya vencieron.

> [!ADVERTENCIA] Si tiene saldo vencido, su vendedor no le puede registrar pedidos a crédito hasta que se ponga al día. Puede seguir comprando con pago contra entrega.

Un cliente sin crédito ve sus compras y facturas, y en **Crédito** el mensaje «No hay crédito asignado actualmente.»:

![Estado de cuenta de un cliente minorista sin crédito](capturas/cliente/cli-estado-03.png)

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No fue posible cargar el estado de cuenta. | Falla momentánea. Recargue la página. |
| Los montos no coinciden con lo que usted pagó | Puede que un pago aún no esté registrado. Comuníquese con la empresa con el comprobante del pago. |

## Descargar el estado de cuenta en PDF {#mod-estado-cuenta-pdf}

### Para qué sirve

Guardar o enviar su estado de cuenta, por ejemplo a su contador.

### Quién puede usarlo

Clientes con sesión iniciada.

### Pasos

1. En **Estado de cuenta**, haga clic en **Descargar PDF** (arriba a la derecha).
2. Espere unos segundos mientras aparece «Generando estado-de-cuenta-….pdf...».

### Resultado esperado

Aparece «Descarga completada: el archivo estado-de-cuenta-….pdf se guardó en su equipo.». El archivo queda en la carpeta **Descargas**.

![Confirmación de la descarga del PDF](capturas/cliente/cli-estado-02.png)

El PDF incluye un resumen (límite, saldo pendiente, saldo vencido, crédito disponible, facturas, pagos registrados y plazo de crédito), las facturas, los cargos y pagos, y los pedidos. Los montos están en colones.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No fue posible descargar el archivo. Intente nuevamente. | Revise la conexión y vuelva a hacer clic en **Descargar PDF**. |
| No fue posible generar el estado de cuenta. | Falla momentánea. Intente en unos minutos. |
