# Corrección post-cierre para Codex — Facturación, mojibake, Billing UI y Auth UI

**Proyecto:** Trilogia-Cursos / DistribuidoraJJ / Licorera La Bodega  
**Rama actual:** `feature/qa-seguridad-arquitectura`  
**Ruta recomendada dentro del proyecto:**

```text
Trilogia-Cursos/docs/correccion-post-cierre-facturacion-ui-codex.md
```

---

# Objetivo

Revisar y corregir problemas detectados después del cierre técnico:

1. Productos con texto mal codificado, por ejemplo: `Ron AÃ±ejo 750ml`.
2. Error al generar factura desde el detalle de pedido.
3. Facturas que no aparecen o no se reflejan correctamente en `Billing`.
4. Botón `Ver` cortado en la tabla de facturación.
5. Diseño de `Iniciar sesión` y `Registro`, que visualmente no convence.

Este bloque NO debe abrir cambios grandes sin diagnóstico. Primero se debe revisar, explicar la causa y proponer correcciones seguras.

---

# Reglas obligatorias

Primero trabajar en **SOLO LECTURA**.

```text
No edites archivos todavía.
No hagas commits.
No hagas push.
No hagas merge.
No toques appsettings.
No toques cadenas de conexión.
No toques bin, obj, .vs, ZIPs ni archivos generados.
No cambies SQL sin aprobación.
No cambies lógica de facturación sin diagnóstico.
No cambies diseño sin explicar qué archivos tocarías.
```

Esperá aprobación explícita:

```text
Aprobado, podés implementar
```

---

# Contexto visual detectado

## 1. Producto mal escrito en Checkout

En `Cart/Checkout` se ve:

```text
Ron AÃ±ejo 750ml
```

Debe verse:

```text
Ron Añejo 750ml
```

Esto puede venir de:

```text
datos guardados en base de datos
scripts SQL seed/demo
inserciones sin prefijo N en SQL Server
encoding de archivos
servicios o vistas
```

---

## 2. Error al generar factura

En `OrdersAdmin/Detail/42`, el pedido aparece como:

```text
Estado actual: Entregado
Cliente: Soda y Bar La Carreta
Total: ₡193 500,00
```

Pero al intentar generar factura aparece:

```text
No fue posible generar la factura del pedido. Verifique que no este cancelado ni facturado previamente.
```

Necesito saber si:

```text
es error mío por flujo incorrecto
el pedido ya tenía factura
el procedimiento bloquea correctamente duplicados
el procedimiento falla incorrectamente
la factura se genera pero no se lista
Billing no está leyendo bien
hay problema con líneas del pedido
hay problema con estado del pedido
```

---

## 3. Problema visual en Billing

En `Billing`, el botón `Ver` de la tabla aparece cortado hacia la derecha.

Problemas visibles:

```text
tabla demasiado ancha
acciones cortadas
posible overflow horizontal
botón al final sin espacio
layout no responsive
```

---

## 4. Login / Registro visualmente débiles

Las vistas de login y registro funcionan, pero visualmente no convencen.

Problemas percibidos:

```text
diseño pesado
panel negro demasiado dominante
layout rígido
poca armonía visual
formulario poco moderno
espaciado mejorable
jerarquía visual mejorable
```

Objetivo visual:

```text
más limpio
más moderno
más equilibrado
menos pesado
mantener identidad negro/dorado
mantener funcionalidad actual
no romper login
no cambiar rutas
no cambiar lógica
```

---

# PARTE 1 — Diagnóstico de mojibake / textos mal codificados

## Archivos y zonas a revisar

Revisar en solo lectura:

```text
database/*
Proyecto_Final/Services/*
Proyecto_Final/Controllers/*
Proyecto_Final/Views/*
Proyecto_Final/wwwroot/js/*
Proyecto_FinalAPI/*
docs/*
```

Buscar ejemplos como:

```text
Ã¡
Ã©
Ã­
Ã³
Ãº
Ã±
AÃ±ejo
estÃ¡
opciÃ³n
ediciÃ³n
contraseÃ±a
categorÃ­a
facturaciÃ³n
```

Comando sugerido:

```bash
git grep -n "Ã\|Â" -- Proyecto_Final Proyecto_FinalAPI database docs
```

## Revisar en SQL

Buscar en scripts:

```text
Ron AÃ±ejo
AÃ±ejo
Añejo
INSERT
UPDATE
Productos
Categorias
```

Determinar si los inserts usan `N'...'`.

Ejemplo incorrecto:

```sql
INSERT INTO Productos (Nombre) VALUES ('Ron Añejo 750ml')
```

Ejemplo correcto:

```sql
INSERT INTO Productos (Nombre) VALUES (N'Ron Añejo 750ml')
```

## Preguntas que debe responder Codex

1. ¿El texto mal escrito viene de base de datos?
2. ¿Viene de scripts SQL?
3. ¿Viene de vistas/servicios?
4. ¿Hay datos ya guardados con mojibake?
5. ¿Hay scripts que insertan texto con acentos sin prefijo `N`?
6. ¿Qué archivo SQL o servicio genera `Ron AÃ±ejo 750ml`?
7. ¿Se necesita script de corrección de datos?
8. ¿Se necesita corregir scripts seed/demo?
9. ¿Qué riesgo tiene corregirlo?

## Posible solución esperada

Si el problema viene de BD, proponer script idempotente, por ejemplo:

```text
database/cu096_corregir_mojibake_productos.sql
```

Debe ser seguro y específico, no masivo ni destructivo.

Ejemplo de idea:

```sql
UPDATE Productos
SET Nombre = N'Ron Añejo 750ml'
WHERE Nombre = N'Ron AÃ±ejo 750ml';
```

No implementar sin aprobación.

---

# PARTE 2 — Diagnóstico de generación de factura

## Archivos a revisar

Revisar en solo lectura:

```text
Proyecto_Final/Controllers/BillingController.cs
Proyecto_Final/Controllers/OrdersAdminController.cs
Proyecto_Final/Services/AdminDbService.cs
Proyecto_Final/Models/Admin/BillingViewModels.cs
Proyecto_Final/Views/OrdersAdmin/Detail.cshtml
Proyecto_Final/Views/Billing/Index.cshtml
Proyecto_Final/Views/Billing/Detail.cshtml
database/*
```

## Métodos a revisar

Revisar específicamente:

```text
BillingController.GenerateFromOrder(int pedidoId)
AdminDbService.GetInvoiceIdByOrderAsync(int pedidoId)
AdminDbService.GenerateInvoiceFromOrderAsync(int pedidoId, ...)
AdminDbService.GetSalesReportAsync()
AdminDbService.GetInvoiceDetailAsync(...)
```

## Procedimientos a revisar

Buscar y revisar:

```text
sp_Admin_GenerateInvoiceFromOrder
sp_Admin_GetInvoiceByOrderId
sp_Admin_GetInvoices
sp_Admin_GetInvoiceHeader
sp_Admin_GetInvoiceLines
sp_Admin_UpdateOrderStatus
sp_Admin_OrderHasInvoice
```

## Caso específico a diagnosticar

Pedido observado:

```text
OrdersAdmin/Detail/42
Estado: Entregado
Cliente: Soda y Bar La Carreta
Total: ₡193 500,00
```

Mensaje al generar factura:

```text
No fue posible generar la factura del pedido. Verifique que no este cancelado ni facturado previamente.
```

## Consultas SQL sugeridas para diagnóstico manual

Codex puede proponerlas, pero no ejecutarlas automáticamente.

```sql
SELECT PedidoId, Estado, UsuarioId, Total, FechaPedido
FROM Pedidos
WHERE PedidoId = 42;

SELECT *
FROM DetallePedidos
WHERE PedidoId = 42;

SELECT *
FROM Facturas
WHERE PedidoId = 42;

SELECT *
FROM DetalleFacturas
WHERE FacturaId IN (
    SELECT FacturaId FROM Facturas WHERE PedidoId = 42
);
```

Si las tablas tienen nombres distintos, Codex debe ajustarlas al esquema real.

## Preguntas que debe responder Codex

1. ¿El botón está llamando al endpoint correcto?
2. ¿La acción tiene `[HttpPost]`?
3. ¿La acción tiene `[ValidateAntiForgeryToken]`?
4. ¿La acción tiene `[AdminAuthorize("Facturacion", "FACTURACION_GENERAR")]`?
5. ¿El pedido #42 ya tiene factura?
6. ¿El pedido #42 tiene líneas válidas?
7. ¿El estado `Entregado` es permitido por el procedimiento?
8. ¿El procedimiento bloquea si el pedido ya está facturado?
9. ¿El procedimiento devuelve algún código o resultado que el C# interprete mal?
10. ¿La factura se genera pero no aparece en Billing?
11. ¿Billing lista todas las facturas o solo una parte?
12. ¿El problema es funcional, visual o ambos?
13. ¿Es error del usuario o del sistema?

## Veredicto esperado

Codex debe responder claramente:

```text
¿El error al generar factura es del usuario o del sistema?
```

Opciones:

```text
Usuario: el pedido ya estaba facturado o no cumple condición.
Sistema: el procedimiento/controlador/servicio bloquea incorrectamente.
Mixto: el sistema no explica bien el estado real.
```

---

# PARTE 3 — Diagnóstico de Billing UI

## Archivos a revisar

```text
Proyecto_Final/Views/Billing/Index.cshtml
Proyecto_Final/Views/Billing/Detail.cshtml
Proyecto_Final/wwwroot/css/*
Proyecto_Final/wwwroot/js/*
```

## Problemas a corregir

```text
botón Ver cortado
tabla demasiado ancha
acciones al extremo derecho sin espacio
scroll horizontal incómodo
responsive deficiente
encabezados muy anchos
acciones no visibles
```

## Preguntas que debe responder Codex

1. ¿El botón se corta por overflow?
2. ¿La tabla está dentro de un contenedor con ancho limitado?
3. ¿Falta columna de acciones fija o más compacta?
4. ¿Hay estilos globales que afectan la tabla?
5. ¿Conviene reducir columnas visibles?
6. ¿Conviene usar una columna de acciones con ancho fijo?
7. ¿Conviene agregar `table-responsive` o equivalente?
8. ¿Qué archivo exacto tocaría?
9. ¿Se puede corregir sin cambiar lógica?

## Resultado esperado

Proponer una corrección visual segura que:

```text
no cambie datos
no cambie rutas
no cambie controlador
no cambie SQL
solo mejore layout
```

---

# PARTE 4 — Diagnóstico Login / Registro UI

## Archivos a revisar

```text
Proyecto_Final/Views/Account/Login.cshtml
Proyecto_Final/Views/Account/Register.cshtml
Proyecto_Final/Views/Account/Registro.cshtml
Proyecto_Final/wwwroot/css/*
Proyecto_Final/wwwroot/js/*
```

> Revisar el nombre real de la vista de registro, porque puede llamarse `Register.cshtml`, `Registro.cshtml` u otro.

## Objetivo visual

Rediseñar login y registro para que se vean:

```text
más limpios
más modernos
más premium
más equilibrados
menos pesados
más claros para el usuario
consistentes con negro/dorado
responsive
```

## No cambiar

```text
nombres de inputs
asp-for
asp-action
asp-controller
validaciones
anti-forgery
rutas
lógica
modelo
controlador
servicio
login API
sesión MVC
```

## Preguntas que debe responder Codex

1. ¿Qué vista real usa login?
2. ¿Qué vista real usa registro?
3. ¿Qué CSS controla esas pantallas?
4. ¿Se puede rediseñar solo con Razor/CSS?
5. ¿Hay riesgo de romper validaciones?
6. ¿Qué archivos exactos tocaría?
7. ¿Qué elementos visuales cambiaría?
8. ¿Cómo mantendría la identidad negro/dorado?
9. ¿Cómo mejoraría responsive?
10. ¿Qué pruebas habría que hacer?

## Propuesta visual esperada

Codex debe proponer algo como:

```text
panel principal más centrado
tarjeta de login más limpia
menos negro en bloque izquierdo
mejor header visual
inputs más uniformes
botón principal más elegante
links mejor alineados
demo credentials más discreto
registro con estructura similar
responsive para móvil
```

---

# PARTE 5 — Bloques de implementación sugeridos

Después del diagnóstico, Codex debe proponer bloques pequeños.

No implementar todavía.

## Bloque sugerido A — Corrección funcional y datos

Posibles archivos:

```text
database/cu096_corregir_mojibake_productos.sql
database/cu097_fix_facturacion_pedido.sql
Proyecto_Final/Controllers/BillingController.cs
Proyecto_Final/Services/AdminDbService.cs
Proyecto_Final/Views/OrdersAdmin/Detail.cshtml
Proyecto_Final/Views/Billing/Index.cshtml
```

Solo tocar lo necesario según diagnóstico.

## Bloque sugerido B — UI Billing

Posibles archivos:

```text
Proyecto_Final/Views/Billing/Index.cshtml
Proyecto_Final/wwwroot/css/*
```

## Bloque sugerido C — UI Login / Registro

Posibles archivos:

```text
Proyecto_Final/Views/Account/Login.cshtml
Proyecto_Final/Views/Account/Register.cshtml
Proyecto_Final/Views/Account/Registro.cshtml
Proyecto_Final/wwwroot/css/*
```

## Bloque sugerido D — QA/docs update

Posibles archivos:

```text
docs/qa-final.md
docs/resumen-final-proyecto.md
docs/resumen-mejoras-seguridad.md
```

---

# PARTE 6 — Entregable del diagnóstico

La respuesta debe traer:

```text
1. Resumen ejecutivo.
2. Diagnóstico de productos con mojibake.
3. Causa probable del mojibake.
4. Diagnóstico de generación de factura.
5. Veredicto: error del usuario o del sistema.
6. Diagnóstico de Billing/Index y botón Ver cortado.
7. Diagnóstico de Login y Registro.
8. Archivos que tocaría.
9. SQL que propondría revisar o crear.
10. Riesgos.
11. Bloques de implementación sugeridos.
12. Pruebas exactas después de corregir.
13. Summary y Description tentativos para cada bloque.
14. Recomendación final.
```

---

# Pruebas que deberán hacerse después

## Productos / mojibake

```text
Tienda muestra Ron Añejo correctamente.
Checkout muestra Ron Añejo correctamente.
Billing muestra Ron Añejo correctamente si aparece en factura.
API /api/products muestra Ron Añejo correctamente.
```

## Facturación

```text
Pedido entregado sin factura permite generar factura.
Pedido ya facturado no permite duplicar factura y muestra mensaje claro.
Pedido cancelado no permite generar factura.
Factura generada aparece en Billing.
Botón Ver abre detalle de factura.
Detalle factura muestra líneas correctas.
```

## Billing UI

```text
Tabla no corta botón Ver.
Vista se ve bien en escritorio.
Vista se ve razonable en pantalla mediana.
No se pierde información crítica.
```

## Login / Registro

```text
Login admin funciona.
Login cliente funciona.
Credenciales inválidas muestran error.
Registro carga correctamente.
Validaciones siguen funcionando.
Recuperar contraseña sigue visible.
Responsive correcto.
```

## Build

```bash
dotnet build Trilogia-Cursos\Proyecto_Final.slnx
```

Debe quedar:

```text
0 errores
0 warnings
```

---

# Commit sugerido para diagnóstico documentado

Si se crea documento posterior con este diagnóstico:

**Summary**

```text
docs: diagnosticar correcciones post cierre
```

**Description**

```text
Documenta diagnóstico post-cierre sobre textos mal codificados, generación de facturas, visual de facturación y rediseño de login/registro antes de aplicar correcciones finales.
```
