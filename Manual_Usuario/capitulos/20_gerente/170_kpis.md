# Reporte de KPIs {#mod-kpis}

El Reporte de KPIs (indicadores clave) mide cuánto cumplió la fuerza de ventas su meta de un mes: el total del equipo y el detalle de cada vendedor.

Se abre con el botón **KPIs** del menú superior, o en **Administración → Reportes y Gerencia → Reporte de KPIs**. También se llega desde [Metas de ventas](#mod-metas), con el botón **Ver reporte de KPIs**.

## Consultar el cumplimiento de un mes {#mod-kpis-consultar}

### Para qué sirve

Evaluar el mes de ventas: si el equipo llegó a su meta y qué vendedores necesitan apoyo.

### Quién puede usarlo

Gerente y Administrador (solo consulta).

### Pasos

1. Abra **KPIs**. Al entrar muestra el mes actual.
2. Elija el período:

   ![Período del reporte y totales del equipo](capturas/gerente/ger-kpis-01.png)

   1. **Año**: escriba el año.
   2. **Mes**: elija el mes.
   3. Haga clic en **Generar reporte**.

### Resultado esperado

Arriba aparecen cuatro tarjetas:

| Tarjeta | Qué muestra |
|---|---|
| **Meta global** | La suma de las metas del mes y cuántos vendedores tienen meta. |
| **Venta global** | Lo facturado en el mes (facturas no anuladas) a los vendedores que tienen meta. Debajo, «Aparte, sin meta» muestra lo vendido por vendedores sin meta, que no cuenta en el cumplimiento. |
| **Cumplimiento** | Venta global entre meta global, en porcentaje. Verde desde 100 %, amarillo desde 70 % y rojo por debajo. |
| **Metas cumplidas** | Cuántos vendedores alcanzaron su meta, del total que tiene meta. |

Abajo, el gráfico **Meta vs. venta por vendedor** (barra gris = meta, barra morada = venta real) y la tabla **Detalle por vendedor**:

![Gráfico y detalle por vendedor](capturas/gerente/ger-kpis-02.png)

La columna **Estado** clasifica a cada vendedor:

| Estado | Qué significa |
|---|---|
| **Cumplida** | Vendió el 100 % o más de su meta. |
| **En riesgo** | Vendió entre el 70 % y el 99 %. |
| **Incumplida** | Vendió menos del 70 %. |
| **Sin meta** | Tuvo ventas, pero no tiene meta ese mes. Defínala en [Metas de ventas](#mod-metas). |

> [!NOTA] Una leyenda arriba del reporte lo recuerda: la venta son las facturas no anuladas de pedidos con vendedor asignado. Las compras que los clientes hacen solos en la tienda en línea no entran, por eso estas cifras pueden ser menores que las del [Tablero gerencial](#mod-tablero-gerencial) y las del [Reporte de ventas](#mod-reporte-ventas).

> [!CONSEJO] Revise el reporte a mediados de mes: un vendedor **En riesgo** todavía puede alcanzar su meta si se le apoya a tiempo.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No hay metas ni ventas por vendedor registradas en este período. | Elija otro mes, o defina las metas del mes en [Metas de ventas](#mod-metas). |

## Exportar el reporte a Excel {#mod-kpis-exportar}

### Para qué sirve

Llevar el cumplimiento del mes a una hoja de Excel, por ejemplo para una reunión de ventas.

### Quién puede usarlo

Gerente y Administrador.

### Pasos

1. Genere el reporte del mes que necesita (vea el procedimiento anterior).
2. Haga clic en **Exportar Excel** (arriba a la derecha, junto a **Gestionar metas**).

### Resultado esperado

El navegador descarga un archivo con nombre como `kpis-2026-10.xlsx`, con una hoja **KPIs**: los totales del equipo (incluida la venta de vendedores sin meta) y una fila por vendedor con su meta, venta real, facturas, cumplimiento y estado.

### Si algo sale mal

| Situación | Qué hacer |
|---|---|
| No se descarga nada, o el archivo no abre. | Revise que el navegador no haya bloqueado la descarga y que el reporte tenga datos. Si el reporte dice que no hay datos, no hay nada que exportar. |
