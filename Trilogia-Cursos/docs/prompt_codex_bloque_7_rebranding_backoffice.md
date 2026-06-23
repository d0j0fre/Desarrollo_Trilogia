# Prompt Codex — Bloque 7: Rebranding visual del backoffice

## Contexto

Quiero continuar con el **Bloque 7** del rebranding visual del proyecto:

**Trilogia-Cursos / Proyecto_Final**

Ya se trabajó el rebranding visual en:

- Layout compartido.
- Assets de marca y favicon.
- Login y Registro.
- Home, Shop y Detail.
- Carrito, Checkout y Confirmación.
- Portal cliente, comprobantes y facturación.

## Marca

Nombre visible:

```text
Supermercado Mayoreo
```

Subtítulo:

```text
Licorera - Distribuidora
```

## Paleta oficial

```text
Rojo principal: #8B0E16
Rojo secundario: #C41E26
Dorado principal: #D4AF37
Dorado claro: #F2D77A
Negro carbón: #1F1F1F
Blanco: #FFFFFF
```

---

# Reglas obligatorias

Antes de tocar archivos:

1. Confirmar rama actual.
2. Confirmar `git status`.
3. Diagnosticar primero.
4. No implementar sin explicar el alcance.

No tocar:

```text
main
Danny
appsettings.json
appsettings.Development.json
SQL
database/
controladores
servicios
modelos
rutas
lógica de negocio
permisos
sesiones
facturación
inventario
pedidos
roles
autenticación
procedimientos almacenados
```

No hacer:

```text
push
merge
Pull Request
cambios masivos
renombres técnicos
```

Solo se permiten cambios visuales.

Preferir cambios en:

```text
Proyecto_Final/wwwroot/css/brand-overrides.css
```

Usar otros CSS solo si es estrictamente necesario.

---

# Objetivo del Bloque 7

Aplicar el rebranding visual al backoffice y portales internos:

- Dashboard administrativo.
- Inventario.
- Movimientos de inventario.
- Pedidos admin.
- Detalle de pedido admin.
- Venta móvil / SellerOrders.
- Portal empleado.
- Roles.
- Permisos.
- Seguridad / administración de usuarios si aplica.

---

# Archivos probables

Revisar primero si existen y si aplican:

```text
Proyecto_Final/Views/Admin/Index.cshtml
Proyecto_Final/Views/Inventory/Index.cshtml
Proyecto_Final/Views/Inventory/Movements.cshtml
Proyecto_Final/Views/Inventory/Create.cshtml
Proyecto_Final/Views/Inventory/Edit.cshtml
Proyecto_Final/Views/OrdersAdmin/Index.cshtml
Proyecto_Final/Views/OrdersAdmin/Detail.cshtml
Proyecto_Final/Views/SellerOrders/Index.cshtml
Proyecto_Final/Views/EmployeePortal/Index.cshtml
Proyecto_Final/Views/Roles/*.cshtml
Proyecto_Final/Views/Permissions/*.cshtml
Proyecto_Final/Views/Security/*.cshtml
Proyecto_Final/wwwroot/css/brand-overrides.css
```

---

# Reglas técnicas

No cambiar:

```text
asp-controller
asp-action
asp-route
asp-for
name de inputs
id de campos funcionales
formularios POST
tokens antiforgery
atributos de autorización
validaciones
scripts funcionales
```

No eliminar botones ni acciones existentes.

No tocar:

```text
AdminController
InventoryController
OrdersAdminController
SellerOrdersController
EmployeePortalController
RolesController
PermissionsController
AdminDbService
StoreDbService
modelos
SQL
```

---

# Tareas

## 1. Diagnóstico inicial

Antes de editar, revisar:

- Qué vistas controlan dashboard, inventario, pedidos, venta móvil, portal empleado, roles y permisos.
- Qué clases CSS usan actualmente.
- Qué partes ya tienen estilos `s3-*`, `s4-*` o similares.
- Dónde conviene aplicar overrides sin romper el resto.
- Qué vistas realmente necesitan edición y cuáles se pueden mejorar solo con CSS.

Entregar diagnóstico antes de implementar.

## 2. Dashboard admin

Mejorar visualmente:

- Tarjetas.
- Métricas.
- Encabezados.
- Tablas.
- Botones.
- Accesos rápidos.

Mantener enlaces y rutas intactas.

## 3. Inventario

Mejorar visualmente:

- Tabla/listado de productos.
- Botones Crear, Editar, Movimientos, Ver o similares.
- Badges de stock.
- Estados.
- Formularios de creación/edición si aplica.

Mantener intactos:

- Uploads de imágenes.
- Inputs.
- Formularios.
- Validaciones.
- Antiforgery.

## 4. Movimientos de inventario

Mejorar visualmente:

- Tablas.
- Filtros.
- Badges.
- Estados.
- Encabezados.

Mantener datos y acciones intactas.

## 5. Pedidos admin

Mejorar visualmente:

- Tabla de pedidos.
- Estados.
- Botones.
- Detalle del pedido.
- Totales.
- Acciones administrativas.

Mantener intactos:

- UpdateStatus.
- GenerateInvoice.
- Ver.
- Formularios.
- Antiforgery.
- Reglas de pedidos.
- Reglas de facturación.

## 6. Venta móvil / SellerOrders

Mejorar visualmente:

- Vista de vendedor/empleado.
- Tarjetas.
- Tablas.
- Botones.
- Estados.

Mantener formularios y acciones intactas.

## 7. Portal empleado

Mejorar visualmente:

- Tarjetas de perfil.
- Permisos visibles.
- Tareas o datos visibles.
- Botones y enlaces.

Mantener lógica intacta.

## 8. Roles, permisos y seguridad

Mejorar visualmente:

- Formularios.
- Tablas.
- Checkboxes.
- Badges.
- Botones.
- Alertas.

Mantener intactos:

- Antiforgery.
- Permisos.
- Rutas.
- Acciones POST.
- Campos.
- Validaciones.

---

# Validaciones obligatorias

Validar por HTTP cuando sea posible:

```text
/Admin
/Inventory
/Inventory/Movements
/OrdersAdmin
/SellerOrders
/EmployeePortal
/Roles
/Permissions
/Security
/css/brand-overrides.css
/brand/logo-icon.png
```

Las rutas protegidas pueden responder con redirección a Login. Eso está bien si el comportamiento ya era así.

Verificar en HTML/Razor:

- Formularios presentes.
- Antiforgery presente donde aplique.
- `asp-controller` intacto.
- `asp-action` intacto.
- `asp-route` intacto.
- `asp-for` intacto.
- Botones de acción no eliminados.
- No se cambian nombres de campos.
- No se cambian rutas.

---

# Build obligatorio

Ejecutar:

```bash
dotnet build Trilogia-Cursos\Proyecto_Final.slnx
```

Resultado esperado:

```text
0 errores
0 warnings
```

---

# Git status

Confirmar que solo aparecen archivos visuales esperados.

No deben aparecer:

```text
ZIPs
bin/
obj/
.vs/
appsettings.json
appsettings.Development.json
database/
controladores
servicios
modelos
archivos generados
```

---

# Criterios de aceptación

El bloque se considera correcto si:

- Backoffice queda alineado con la nueva marca.
- Inventario y pedidos se ven más profesionales.
- Roles, permisos y seguridad mantienen funcionamiento intacto.
- Portal empleado y venta móvil mantienen navegación y acciones.
- No se toca lógica.
- No se tocan controladores.
- No se tocan servicios.
- No se tocan modelos.
- No se toca SQL.
- No se tocan appsettings.
- Build queda en 0 errores / 0 warnings.

---

# Salida final esperada

Al terminar, entregar:

1. Diagnóstico inicial.
2. Archivos modificados.
3. Cambios realizados.
4. Validación HTTP.
5. Validación de formularios/enlaces/antiforgery.
6. Resultado de build.
7. Resultado de git status.
8. Commit sugerido.

Formato del commit:

```text
Summary:
style(admin): aplicar rebranding al backoffice

Description:
Actualiza visualmente dashboard administrativo, inventario, pedidos, venta móvil, portal empleado, roles y permisos.

Aplica la identidad de Supermercado Mayoreo / Licorera - Distribuidora usando la paleta corporativa roja, dorada, carbón y blanca.

Conserva rutas, formularios, antiforgery, permisos, controladores, servicios, modelos, SQL y lógica de negocio sin modificaciones.

Validación:
- Rutas principales revisadas por HTTP o redirección esperada a Login.
- Formularios y acciones principales conservados.
- Assets de marca responden HTTP 200.
- CSS responde HTTP 200.
- Build 0 errores / 0 warnings.
- Solo se modificaron archivos esperados.
```
