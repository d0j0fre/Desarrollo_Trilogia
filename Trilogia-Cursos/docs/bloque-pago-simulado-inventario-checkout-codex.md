# Bloque para Codex — Pago Simulado + Inventario Checkout

**Proyecto:** Trilogia-Cursos / DistribuidoraJJ / Licorera La Bodega  
**Rama actual:** `feature/qa-seguridad-arquitectura`  
**Ruta recomendada dentro del proyecto:**

```text
Trilogia-Cursos/docs/bloque-pago-simulado-inventario-checkout-codex.md
```

---

# Objetivo del bloque

Implementar un flujo funcional más completo para el checkout:

1. Agregar **pago simulado** al finalizar pedido.
2. Descontar inventario al crear el pedido.
3. Evitar stock negativo.
4. Registrar movimiento de inventario si el esquema lo permite.
5. Evitar doble descuento al generar factura.
6. Restaurar inventario si se cancela un pedido pendiente antes de facturarse.
7. Mantener la facturación como proceso posterior, sin descontar inventario otra vez.

---

# Contexto actual

Actualmente:

```text
Cliente agrega productos al carrito.
Cliente finaliza pedido.
Se crea el pedido.
Admin revisa el pedido.
Admin puede generar factura.
```

Problema detectado:

```text
Si un producto tiene stock 15 y el cliente compra 10, el stock sigue quedando en 15.
```

Comportamiento esperado:

```text
Stock inicial: 15
Cliente compra: 10
Stock después de finalizar pedido: 5
```

Diagnóstico previo:

```text
El pedido se crea desde CartController.
CartController llama a StoreDbService.CreateOrderAsync.
El procedimiento dbo.sp_Store_CreateOrder valida stock, crea Pedidos y PedidoDetalle, pero no descuenta Productos.Stock.
El stock real está en dbo.Productos.Stock.
Existe dbo.MovimientosInventario.
La facturación crea Facturas y FacturaDetalle, pero no debe descontar inventario para evitar doble descuento.
```

---

# Regla de negocio esperada

Como no hay pasarela real de pago, el sistema debe manejar un **pago simulado académico**.

```text
Finalizar pedido = confirmar pedido con pago simulado.
```

En ese momento:

```text
Se valida stock.
Se crea el pedido.
Se guarda el método de pago simulado.
Se descuenta inventario una sola vez.
Se registra movimiento de inventario si se puede.
```

La factura:

```text
Se genera después desde administración.
NO descuenta inventario.
Solo formaliza la venta.
```

Cancelación:

```text
Si el pedido está pendiente, no tiene factura y ya descontó inventario, al cancelarlo debe devolver el stock una sola vez.
```

---

# Reglas obligatorias

```text
No tocar appsettings.
No tocar cadenas de conexión.
No tocar API.
No tocar AuthController.
No tocar Login.
No tocar Registro.
No tocar CSS visual general.
No tocar Billing UI visual.
No implementar pasarelas reales.
No usar APIs externas.
No guardar datos sensibles.
No guardar número real de tarjeta.
No guardar CVV.
No guardar datos bancarios.
No descontar inventario al generar factura.
No cambiar rutas existentes.
No cambiar permisos existentes.
No tocar bin, obj, .vs, ZIPs ni archivos generados.
```

---

# Archivos permitidos

Podés tocar únicamente si hace falta:

```text
database/cu097_pago_simulado_inventario_checkout.sql
Proyecto_Final/Controllers/CartController.cs
Proyecto_Final/Services/StoreDbService.cs
Proyecto_Final/Models/*
Proyecto_Final/Views/Cart/Checkout.cshtml
Proyecto_Final/Views/OrdersAdmin/Detail.cshtml
Proyecto_Final/Views/Billing/Detail.cshtml
docs/qa-final.md
docs/resumen-final-proyecto.md
docs/resumen-mejoras-seguridad.md
```

Notas:

```text
Solo tocar Models/* si el checkout usa un ViewModel específico.
Solo tocar Billing/Detail.cshtml si mostrar método de pago no requiere cambios grandes.
No tocar BillingController ni AdminDbService salvo que sea estrictamente necesario y se justifique antes.
```

---

# Paso 1 — Revisar esquema real antes de implementar

Antes de cambiar el SQL, revisar los scripts y confirmar nombres reales de tablas/columnas:

```text
dbo.Pedidos
dbo.Productos
dbo.PedidoDetalle
dbo.MovimientosInventario
dbo.sp_Store_CreateOrder
dbo.sp_Client_CancelPendingOrder
dbo.sp_Admin_UpdateOrderStatus
procedimientos de detalle de pedido
procedimientos de detalle de factura
```

Confirmar:

```text
Productos.Stock existe y es el stock real.
PedidoDetalle es la tabla real de líneas de pedido.
MovimientosInventario existe y permite registrar salidas/entradas.
sp_Store_CreateOrder es el único punto que crea pedidos desde checkout.
```

---

# Paso 2 — Crear script SQL cu097

Crear:

```text
database/cu097_pago_simulado_inventario_checkout.sql
```

El script debe ser:

```text
idempotente
seguro
sin borrado de datos
sin cambios masivos
sin datos demo innecesarios
sin romper pedidos existentes
sin cambiar estructura ajena al pago/inventario
```

---

# Paso 3 — Agregar columnas de pago simulado a Pedidos

Agregar columnas solo si no existen:

```sql
MetodoPago NVARCHAR(40) NOT NULL
EstadoPago NVARCHAR(30) NOT NULL
ReferenciaPago NVARCHAR(80) NULL
FechaPago DATETIME2 NULL
InventarioDescontado BIT NOT NULL
```

Valores default sugeridos para pedidos existentes:

```text
MetodoPago: N'No especificado'
EstadoPago: N'Pendiente'
ReferenciaPago: NULL
FechaPago: NULL
InventarioDescontado: 0
```

Valores permitidos sugeridos para `MetodoPago`:

```text
Efectivo contra entrega
SINPE Móvil simulado
Tarjeta demo
Transferencia simulada
```

Valores permitidos sugeridos para `EstadoPago`:

```text
Pendiente
Simulado
Confirmado simulado
```

---

# Paso 4 — Modificar dbo.sp_Store_CreateOrder

Modificar `dbo.sp_Store_CreateOrder` dentro del script `cu097`.

Debe:

1. Mantener validación de productos activos.
2. Mantener validación de stock suficiente.
3. Recibir método de pago y referencia.
4. Validar método de pago contra la lista permitida.
5. Crear pedido con:
   - `MetodoPago`
   - `EstadoPago`
   - `ReferenciaPago`
   - `FechaPago`
   - `InventarioDescontado`
6. Descontar `dbo.Productos.Stock` dentro de la misma transacción.
7. Evitar stock negativo.
8. Marcar `InventarioDescontado = 1` cuando el descuento se aplique correctamente.
9. Registrar movimiento en `dbo.MovimientosInventario` si el esquema real lo permite.
10. No generar factura.
11. No tocar lógica de facturación.

---

# Paso 5 — Evitar stock negativo

El descuento debe ser seguro ante concurrencia.

La lógica debe evitar esto:

```text
Stock actual: 15
Cliente A compra 10
Cliente B compra 10 al mismo tiempo
Resultado incorrecto: stock negativo
```

La actualización debe validar stock en el momento de descontar.

Ejemplo conceptual:

```sql
UPDATE p
SET p.Stock = p.Stock - i.Cantidad
FROM dbo.Productos p
INNER JOIN @Items i ON i.ProductoId = p.ProductoId
WHERE p.Stock >= i.Cantidad;
```

Luego validar que se actualizaron todos los productos necesarios.

No copiar este SQL sin adaptarlo al SP real.

---

# Paso 6 — Registrar movimiento de inventario

Si `dbo.MovimientosInventario` permite registrar salidas, registrar una salida por venta/pedido.

Debe quedar claro:

```text
Tipo de movimiento: Salida / Venta / Pedido
Referencia: PedidoId
Cantidad: cantidad vendida
Usuario: cliente o sistema, según esquema
Fecha: fecha actual
```

Si el esquema no permite registrar de forma segura, reportarlo y no forzar el registro.

---

# Paso 7 — Cancelación de pedido pendiente

Revisar y modificar si corresponde:

```text
dbo.sp_Client_CancelPendingOrder
dbo.sp_Admin_UpdateOrderStatus
```

## Cancelación cliente

Si el cliente cancela un pedido:

```text
Pedido está Pendiente.
Pedido no tiene factura.
Pedido tiene InventarioDescontado = 1.
```

Entonces debe:

```text
devolver stock a Productos.Stock
registrar movimiento de entrada/cancelación si se puede
poner InventarioDescontado = 0
marcar pedido como Cancelado
evitar doble devolución
```

No restaurar stock si:

```text
pedido ya tiene factura
InventarioDescontado = 0
pedido no está Pendiente
```

## Cancelación admin

Si el admin cambia estado a `Cancelado` en un pedido no facturado:

```text
si InventarioDescontado = 1, devolver stock una sola vez.
si InventarioDescontado = 0, no hacer nada al stock.
```

No tocar pedidos facturados.

---

# Paso 8 — C# Checkout

Modificar `CartController` y `StoreDbService` según sea necesario.

## CartController

Debe:

```text
recibir MetodoPago
recibir ReferenciaPago opcional
validar método de pago permitido
no recibir datos sensibles
mantener validaciones actuales del carrito
mostrar error claro si stock es insuficiente
```

## StoreDbService

Debe:

```text
pasar MetodoPago y ReferenciaPago al SP
mantener ItemsJson
mantener usuario/dirección/tipo entrega
manejar errores del SP con mensaje claro
no exponer errores técnicos al usuario
```

---

# Paso 9 — Vista Checkout

Modificar la vista real del checkout, probablemente:

```text
Proyecto_Final/Views/Cart/Checkout.cshtml
```

Agregar sección:

```text
Pago simulado
```

Debe incluir texto visible:

```text
Este pago es una simulación académica. No se realizará ningún cobro real.
```

Opciones:

```text
Efectivo contra entrega
SINPE Móvil simulado
Tarjeta demo
Transferencia simulada
```

Referencia opcional:

```text
Mostrar campo de referencia para SINPE Móvil simulado o Transferencia simulada.
No pedir número de tarjeta real.
No pedir CVV.
No guardar datos bancarios.
```

---

# Paso 10 — Admin detalle de pedido

Modificar si es seguro:

```text
Proyecto_Final/Views/OrdersAdmin/Detail.cshtml
```

Mostrar:

```text
Método de pago
Estado de pago
Referencia de pago, si existe
Fecha de pago, si existe
```

Si el ViewModel no trae esos datos, revisar el método que carga detalle de pedido y proponer cambio mínimo.

Si requiere tocar mucho, reportar antes de implementar.

---

# Paso 11 — Factura detalle

Modificar solo si es seguro:

```text
Proyecto_Final/Views/Billing/Detail.cshtml
```

Mostrar método de pago si el modelo ya lo permite o si se puede agregar de forma segura.

Si requiere cambios grandes en SP/modelos de factura, no implementarlo todavía y reportarlo.

---

# Paso 12 — Documentación

Actualizar:

```text
docs/qa-final.md
docs/resumen-final-proyecto.md
docs/resumen-mejoras-seguridad.md
```

Agregar que el checkout ahora tiene:

```text
pago simulado
rebaja de inventario al crear pedido
prevención de stock negativo
restauración de stock al cancelar pedido pendiente
facturación sin doble descuento
```

---

# Paso 13 — Pruebas esperadas

Agregar pruebas a documentación:

## Inventario

```text
Producto con stock 15.
Comprar 10.
Stock esperado: 5.
```

```text
Intentar comprar más que stock disponible.
Debe fallar con mensaje claro.
Stock no debe cambiar.
```

## Cancelación

```text
Crear pedido pendiente con stock descontado.
Cancelar pedido.
Stock debe restaurarse.
InventarioDescontado debe quedar en 0.
```

## Facturación

```text
Crear pedido.
Stock se descuenta al crear pedido.
Generar factura.
Stock NO debe descontarse otra vez.
Factura se genera correctamente.
Factura aparece en reportes.
```

## Pago simulado

```text
Crear pedido con Efectivo contra entrega.
Crear pedido con SINPE Móvil simulado.
Crear pedido con Tarjeta demo.
Crear pedido con Transferencia simulada.
Confirmar que no se guardan datos sensibles.
Confirmar que admin puede ver método de pago.
```

---

# Paso 14 — SQL que el usuario debe ejecutar manualmente

No ejecutar SQL desde Codex.

Después de implementar, el usuario debe ejecutar en SSMS:

```text
database/cu097_pago_simulado_inventario_checkout.sql
```

Si el proyecto necesita todos los scripts recientes en orden:

```text
database/cu090_admin_facturar_pedido.sql
database/cu091_migracion_pedidos_facturacion_sp.sql
database/cu092_admin_estado_pedido_seguro.sql
database/cu093_admin_reportes_facturacion_sp.sql
database/cu094_permisos_granulares_acciones.sql
database/cu095_facturacion_generar_permiso.sql
database/cu096_corregir_mojibake_productos.sql
database/cu097_pago_simulado_inventario_checkout.sql
```

---

# Paso 15 — Build obligatorio

Ejecutar:

```bash
dotnet build Trilogia-Cursos\\Proyecto_Final.slnx
```

Resultado esperado:

```text
0 errores
0 warnings
```

---

# Reporte final esperado

Reportar:

1. Archivos modificados/creados.
2. Columnas agregadas a `Pedidos`.
3. Cambios en `sp_Store_CreateOrder`.
4. Cambios en cancelación cliente.
5. Cambios en cancelación admin, si aplica.
6. Cambios en `CartController`.
7. Cambios en `StoreDbService`.
8. Cambios en Checkout.
9. Cambios visibles en admin pedido.
10. Si se registran movimientos de inventario.
11. Lista final de scripts SQL a ejecutar.
12. Resultado del build.
13. Warnings.
14. `git status`.
15. Confirmación de que no tocaste:
   - appsettings
   - API
   - Login
   - Registro
   - Billing UI visual
   - rutas
   - permisos
   - facturación para descontar inventario
   - archivos generados
16. Pruebas manuales pendientes.

---

# Commit sugerido

**Summary**

```text
fix(checkout): agregar pago simulado y descontar inventario
```

**Description**

```text
Agrega flujo de pago simulado en checkout y refuerza la creación de pedidos para validar stock, descontar inventario una sola vez y restaurarlo al cancelar pedidos pendientes cuando corresponde.

Mantiene la facturación sin descuento adicional de inventario y no integra pasarelas reales ni guarda datos sensibles.
```

---

# Veredicto esperado después del bloque

El flujo correcto debe quedar así:

```text
Stock inicial agua: 15
Cliente compra 10 aguas
Se crea pedido con pago simulado
Stock queda en 5
Admin genera factura
Stock sigue en 5
Factura aparece en reportes
Si se cancela pedido pendiente antes de facturar, stock vuelve a 15
```
