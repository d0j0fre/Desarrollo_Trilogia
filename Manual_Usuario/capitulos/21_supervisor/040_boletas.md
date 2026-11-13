# Boletas de pago {#mod-boletas}

Cuando el Administrador paga la [planilla](#mod-planilla), cada pago genera una boleta con el detalle de ingresos y deducciones. En esta pantalla el supervisor consulta las boletas de todo el personal, las descarga en PDF y avisa por correo a cada persona que su boleta está lista.

Se abre en **Administración → Personal → Boletas de pago**.

## Consultar y descargar una boleta {#mod-boletas-consultar}

### Para qué sirve

Revisar cuánto se le pagó a una persona en un período y por qué conceptos, por ejemplo para responder una duda sobre su salario.

### Quién puede usarlo

Supervisor y Administrador. Cada empleado ve solo sus propias boletas en [Mis boletas de pago](#mod-mis-boletas).

### Pasos

1. Abra **Administración → Personal → Boletas de pago**. La lista muestra las boletas pagadas, de la más reciente a la más antigua.

   ![Lista de boletas de pago](capturas/supervisor/sup-boletas-01.png)

   1. Haga clic en **Ver** para abrir el detalle de la boleta.
   2. Haga clic en **Descargar** para guardar la boleta en PDF.
   3. **Notificar** envía el aviso por correo (vea el procedimiento siguiente).

### Resultado esperado

Al hacer clic en **Ver** se abre la boleta con el empleado, el período, cada concepto (con su **Código**, **Concepto**, **Tipo** y **Monto**) y los totales: **Salario base**, **Total bruto**, **Deducciones** y **Total neto**. El botón **Descargar PDF privado** guarda la misma boleta en PDF.

![Detalle de una boleta de pago](capturas/supervisor/sup-boletas-02.png)

> [!ADVERTENCIA] La boleta contiene datos privados del salario. Guarde los PDF en una carpeta protegida y no los envíe por chat ni por correo personal.

### Si algo sale mal

| Situación | Qué hacer |
|---|---|
| La lista dice «Todavía no hay boletas: se generan al calcular la planilla de un empleado.» o falta una persona. | Solo aparecen las boletas de planillas **pagadas** (o **revertidas** después de pagarse). Pregunte al Administrador si ya pagó la planilla del período. |

## Notificar una boleta por correo {#mod-boletas-notificar}

### Para qué sirve

Avisar a la persona por correo que su boleta está lista. El correo lleva la boleta en PDF y un enlace seguro a su portal.

### Quién puede usarlo

Supervisor y Administrador.

### Pasos

1. Abra **Administración → Personal → Boletas de pago**.
2. En la fila de la boleta, haga clic en **Notificar** (vea la imagen anterior, número 3). El botón solo se activa en las boletas **Pagadas**.

### Resultado esperado

Aparece el mensaje «Notificación de boleta procesada.». La columna **Último envío** muestra el resultado del envío: **Exitoso**, **Pendiente** o **Fallido**.

> [!CONSEJO] Puede volver a hacer clic en **Notificar** si la persona no recibió el correo. El sistema registra cada envío.

### Si algo sale mal

![Mensaje cuando no se pudo confirmar el envío](capturas/supervisor/sup-boletas-03.png)

| Mensaje | Qué hacer |
|---|---|
| No se pudo notificar la boleta: falta configurar la URL pública del sitio… | Falta un ajuste del servidor para armar el enlace del correo. | La boleta no se pierde: la persona la ve en **Mis boletas**. Avise a Soporte o al Administrador para que configuren el sitio. |
| No se pudo notificar la boleta: el correo del sistema no está configurado… | El sistema todavía no puede enviar correos. | Avise a Soporte o al Administrador. Mientras tanto, la persona ve su boleta en **Mis boletas**. |
| La boleta permanece disponible, pero no fue posible confirmar el envío del correo. | El correo no salió (por ejemplo, el servidor de correo no respondió). | Intente notificar más tarde. Si el error se repite con todas las boletas, avise a Soporte. |

> [!NOTA] Los montos se muestran en colones (por ejemplo, ₡255 000,00) y los períodos con día, mes y año (16/10/2026 — 31/10/2026). En el detalle, los conceptos son de tipo **Ingreso** o **Deducción**.
