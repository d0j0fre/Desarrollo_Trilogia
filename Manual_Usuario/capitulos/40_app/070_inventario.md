# Consulta de inventario {#app-inventario}

**Consulta de inventario** muestra las existencias de cada producto, las alertas de stock bajo o agotado y los últimos movimientos.

**Quién la ve:** Bodeguero, Gerente, Empleado, Compras y Administrador (sección **Bodega**). Es de consulta; registrar movimientos depende del puesto.

## Consultar existencias {#app-inventario-consultar}

### Para qué sirve

Saber al instante cuánto hay de un producto, por ejemplo para responder a un cliente o planificar una compra.

### Quién puede usarlo

Bodeguero, Gerente, Empleado, Compras y Administrador.

### Pasos

1. En la pantalla de inicio, toque **Consulta de inventario**.

   ![Lista de existencias](capturas/app/app-inventario-01.png "ancho=6")

   1. **Buscar por nombre, categoría o código**: escriba para filtrar.
   2. Filtros: **Todos**, **Stock bajo**, **Agotados** e **Inactivos**.
   3. Toque un producto para ver su ficha.

2. Revise la ficha:

   ![Ficha de un producto](capturas/app/app-inventario-02.png "ancho=6")

   1. **Existencias** y **Stock mínimo**.
   2. **Registrar movimiento**: abre [Movimientos de inventario](#app-movimientos) con el producto elegido (solo para los puestos que registran movimientos).
   3. **Últimos movimientos**: entradas, salidas y ajustes, con quién los hizo y cuándo.

### Resultado esperado

Cada producto muestra una etiqueta: **Disponible**, **Stock bajo** (5 unidades o menos), **Agotado** o **Inactivo**.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Sin productos — Ningún producto coincide con «…». | Escriba menos letras o revise la ortografía. |
| Sin movimientos registrados. | El producto no ha tenido movimientos. |
