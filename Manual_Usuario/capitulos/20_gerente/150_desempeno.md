# Desempeño de vendedores {#mod-desempeno}

Desempeño de vendedores compara lo que vendió cada vendedor en un rango de fechas: ventas, pedidos, clientes atendidos y cuánto lleva de su meta.

Se abre con el botón **Vendedores** del menú superior, o en **Administración → Reportes y Gerencia → Desempeño de vendedores**.

## Consultar el desempeño {#mod-desempeno-consultar}

### Para qué sirve

Saber quién está vendiendo más, quién va atrasado con su meta y a cuántos clientes atiende cada uno, para dar seguimiento al equipo.

### Quién puede usarlo

Gerente y Administrador (solo consulta). Cada vendedor ve solo su propio avance en [Mi meta](#mod-mi-meta).

### Pasos

1. Abra **Vendedores**. Al entrar muestra el mes actual.
2. Elija los filtros:

   ![Filtros del desempeño de vendedores](capturas/gerente/ger-desempeno-01.png)

   1. **Desde**: la fecha inicial.
   2. **Hasta**: la fecha final. El rango puede ser de hasta dos años.
   3. **Vendedor**: deje **Todos** o elija uno.
   4. **Orden**: **Ventas ↓** (de mayor a menor), **Ventas ↑**, **Nombre** o **Cumplimiento ↓** (el que va más adelantado con su meta primero).
   5. Haga clic en **Consultar**.

### Resultado esperado

Arriba aparecen los totales del equipo (**Ventas**, **Pedidos** y **Ticket promedio**). Abajo, una fila por vendedor.

![Desempeño del equipo en octubre](capturas/gerente/ger-desempeno-02.png)

| Columna | Qué muestra |
|---|---|
| **Ventas** | Lo facturado de los pedidos que registró el vendedor. |
| **Pedidos** / **Facturas** | Cuántos pedidos y facturas tuvo. |
| **Ticket** | Venta promedio por pedido. |
| **Clientes** | Cuántos clientes distintos atendió. |
| **Meta** | La suma de sus metas mensuales del rango, o **Sin meta** si no tiene. Las metas se definen en [Metas de ventas](#mod-metas). |
| **Cumplimiento** | Ventas entre meta, en porcentaje. 100 % o más significa que alcanzó la meta. |

> [!CONSEJO] Para comparar contra la meta, consulte meses completos (del día 1 al último día del mes): la meta se suma por mes completo aunque el rango tome solo una parte.

Los botones **Imprimir** y **CSV** funcionan igual que en el Reporte de ventas (vea [Imprimir o exportar el reporte](#mod-reporte-ventas-exportar)). El archivo se llama, por ejemplo, `vendedores-20261001-20261031.csv`.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| La fecha inicial no puede superar la final. | Revise **Desde** y **Hasta**. (El mensaje puede salir dos veces.) |
| El reporte admite un rango máximo de dos años. | Acorte el rango. |
| No hay datos de vendedores para este período. | No hay vendedores activos ni ventas en esas fechas. Amplíe el rango. |
