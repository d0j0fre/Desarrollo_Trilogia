# Créditos {#mod-creditos}

En Créditos se decide cuánto puede comprar cada cliente al crédito, en qué plazo debe pagar y se registran sus cargos y abonos. La deuda de cada cliente se calcula sola a partir de esos movimientos.

Se abre en **Administración → Clientes → Créditos**.

> [!NOTA] [Cuentas por cobrar](#mod-cxc) muestra la misma información con otra presentación, pensada para el cobro. Los cambios que haga en un módulo se ven en el otro.

## Consultar el crédito de los clientes {#mod-creditos-consultar}

### Para qué sirve

Saber qué clientes deben dinero, cuánto les queda disponible y cuáles tienen el crédito bloqueado.

### Quién puede usarlo

Administrador. Crédito y Cobro lo consulta en **Administración → Clientes → Créditos** o en la aplicación móvil (vea [Crédito de clientes](#app-credito)). Si su puesto solo puede consultar, en el detalle del cliente no aparecen los formularios y la pantalla dice «Su perfil puede consultar el crédito, pero no configurarlo.» o «Su perfil puede consultar el crédito, pero no registrar movimientos.».

### Pasos

1. Abra **Administración → Clientes → Créditos**.

   ![Filtro y lista de créditos](capturas/administrador/adm-creditos-01.png)

   1. Escriba el nombre, el correo o el teléfono del cliente (opcional).
   2. Elija un filtro: **Crédito activo**, **Crédito bloqueado**, **Crédito inactivo**, **Con deuda**, **Sin deuda** o **Sin crédito asignado**.
   3. Haga clic en **Buscar**.
   4. Haga clic en **Administrar** para abrir el crédito de un cliente.

### Resultado esperado

La tabla muestra, para cada cliente, el **Estado crédito**, el **Límite**, la **Deuda actual**, el crédito **Disponible** (límite menos deuda) y la fecha del último movimiento. Los clientes con más deuda aparecen primero.

Al hacer clic en **Administrar** se abre la pantalla **Crédito de cliente**:

![Resumen del crédito de un cliente](capturas/administrador/adm-creditos-02.png)

Debajo de **Deuda actual**, el sistema avisa si el cliente tiene pagos vencidos según su plazo de crédito (en la imagen, «Sin pagos vencidos (plazo 30 días)»).

## Configurar el crédito de un cliente {#mod-creditos-configurar}

### Para qué sirve

Asignar o cambiar el límite de crédito y el plazo de pago, o bloquear el crédito de un cliente que no está pagando.

### Quién puede usarlo

Administrador.

### Pasos

1. Abra el crédito del cliente (**Administrar**).
2. En el recuadro **Configurar crédito**:

   ![Formulario Configurar crédito](capturas/administrador/adm-creditos-03.png)

   1. **Límite de crédito**: el monto máximo que el cliente puede deber, en colones, sin separador de miles (por ejemplo, 700000).
   2. **Plazo de crédito (días)**: cuántos días tiene para pagar cada cargo (de 0 a 365). Los abonos pagan primero los cargos más antiguos.
   3. **Crédito activo** y **Crédito bloqueado**: desmarque **Crédito activo** para quitarle el crédito, o marque **Crédito bloqueado** para suspenderlo temporalmente.
   4. **Motivo de bloqueo o desactivación**: obligatorio si bloquea o desactiva el crédito.
   5. Haga clic en **Guardar configuración** y luego confirme con **Guardar configuración** en la ventana que aparece.


### Resultado esperado

Aparece el mensaje «Configuración de crédito actualizada correctamente.» y las tarjetas de arriba muestran el límite, el estado y el crédito disponible nuevos.

### Si algo sale mal

![Mensaje cuando la configuración no es válida](capturas/administrador/adm-creditos-06.png)

| Mensaje | Causa más común | Qué hacer |
|---|---|---|
| Revise los datos de configuración del crédito. | Bloqueó o desactivó el crédito sin escribir el motivo, o el límite o el plazo no son válidos. | Escriba el motivo. Revise que el límite sea 0 o más y el plazo esté entre 0 y 365 días. |
| No se pudo actualizar la configuracion del credito. / Ocurrió un error al actualizar la configuración del crédito. | Error al guardar. | Vuelva a cargar la página e intente de nuevo. Si continúa, avise a Soporte. |

## Registrar un abono o un cargo {#mod-creditos-movimiento}

### Para qué sirve

Anotar lo que el cliente paga (abono) o lo que se le carga a su cuenta (por ejemplo, una compra al crédito), para que su deuda quede al día.

### Quién puede usarlo

Administrador.

### Pasos

1. Abra el crédito del cliente (**Administrar**).
2. En el recuadro **Registrar movimiento**:

   ![Formulario Registrar movimiento](capturas/administrador/adm-creditos-04.png)

   1. **Tipo**: **Abono** (el cliente pagó), **Cargo** (el cliente debe más), **Ajuste positivo** (aumenta la deuda por una corrección) o **Ajuste negativo** (la disminuye).
   2. **Monto**: en colones, mayor que 0.
   3. **Referencia**: el número de recibo, transferencia, factura o pedido. Es opcional, pero facilita las revisiones.
   4. **Descripción**: el detalle del movimiento. Es obligatoria.
   5. Haga clic en **Registrar movimiento** y confirme en la ventana **Confirmar movimiento financiero**.

### Resultado esperado

Aparece el mensaje «Movimiento de crédito registrado correctamente.». La **Deuda actual** y el **Crédito disponible** se recalculan, y el movimiento aparece de primero en el **Historial de movimientos**, con su fecha y quién lo registró.

![Abono registrado: deuda actualizada e historial](capturas/administrador/adm-creditos-05.png)

> [!ADVERTENCIA] Los movimientos no se pueden borrar ni editar. Si se equivocó, registre un **Ajuste** en sentido contrario y explique en la descripción qué corrige.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Revise los datos del movimiento de crédito. | Revise que el monto sea mayor que 0 y que la descripción esté escrita. |
| No se pudo registrar el movimiento de credito. / Ocurrió un error al registrar el movimiento de crédito. | Vuelva a cargar la página e intente de nuevo. Si continúa, avise a Soporte. |
