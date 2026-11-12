# Metas de ventas {#mod-metas}

En Metas de ventas usted define cuánto debe vender cada vendedor en un mes y ve, en la misma pantalla, cuánto lleva cada uno. El vendedor ve su meta y su avance en [Mi meta](#mod-mi-meta).

Se abre en **Administración → Reportes y Gerencia → Metas de ventas**.

## Definir o cambiar una meta {#mod-metas-definir}

### Para qué sirve

Fijar el objetivo de ventas del mes de cada vendedor, para medir su cumplimiento en el [Reporte de KPIs](#mod-kpis) y en [Desempeño de vendedores](#mod-desempeno).

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra **Metas de ventas**. Al entrar muestra el mes actual.
2. Si la meta es para otro mes, elija el período:

   ![Elegir el período de las metas](capturas/gerente/ger-metas-01.png)

   1. **Año**: escriba el año.
   2. **Mes**: elija el mes.
   3. Haga clic en **Ver período**.

3. En **Definir meta**, a la izquierda, complete los datos:

   ![Formulario Definir meta](capturas/gerente/ger-metas-02.png "ancho=8")

   1. **Vendedor**: elija el vendedor.
   2. **Meta de ingresos (₡)**: el monto que debe vender en el mes, sin símbolos ni espacios. Por ejemplo, 300000.
   3. **Observaciones**: un comentario sobre la meta. Es opcional.
   4. Revise el **Período**: la meta se guarda para el mes que se muestra aquí.
   5. Haga clic en **Guardar meta**.

### Resultado esperado

Aparece el mensaje «Meta guardada correctamente.» y el vendedor aparece en la tabla **Metas de …** con su meta, sus **Ventas reales** del mes y el porcentaje de **Cumplimiento**.

![Meta guardada en la tabla del mes](capturas/gerente/ger-metas-03.png)

Para **cambiar** una meta, repita los pasos con el mismo vendedor y el mismo mes. Antes de guardar, el sistema le avisa con una ventana: «… ya tiene una meta de ₡… en …. ¿Desea reemplazarla por ₡…?». Haga clic en **Aceptar** para reemplazarla (aparece «Meta reemplazada correctamente.») o en **Cancelar** para dejar la meta anterior.

> [!NOTA] Por ahora no se puede borrar una meta. Si un vendedor no debe tener meta ese mes, ajuste el monto o déjelo como está y explíquelo en **Observaciones**.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Revise los datos de la meta (vendedor, período y monto). | Falta el vendedor, o el monto no es un número mayor que cero. Corrija y vuelva a guardar. |
| No hay usuarios con perfil "Vendedor" registrados. | No hay vendedores activos. Pida al Administrador que registre al vendedor con el perfil **Vendedor**. |
| Un mensaje que explica la causa (por ejemplo, que el vendedor ya no es válido). | Léalo, actualice la página y corrija el dato indicado. |
| No fue posible guardar la meta. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Revisar el cumplimiento del mes {#mod-metas-cumplimiento}

### Para qué sirve

Ver de un vistazo qué vendedores ya alcanzaron su meta y cuáles van atrasados.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Abra **Metas de ventas** y elija el período (vea el procedimiento anterior).
2. Revise la columna **Cumplimiento** de la tabla:

   ![Cumplimiento de las metas de octubre](capturas/gerente/ger-metas-04.png)

### Resultado esperado

Cada vendedor tiene una barra con su porcentaje:

| Color | Qué significa |
|---|---|
| Verde | Llegó al 100 % o más de la meta. |
| Amarillo | Lleva entre el 70 % y el 99 %. |
| Rojo | Lleva menos del 70 %. |

**Ventas reales** suma las facturas del mes (no anuladas) de los pedidos que registró ese vendedor. Para el detalle y la comparación del equipo completo, haga clic en **Ver reporte de KPIs** (vea [Reporte de KPIs](#mod-kpis)).

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No hay metas definidas para este período. | Todavía no hay metas para ese mes. Defínalas con **Guardar meta**. |
