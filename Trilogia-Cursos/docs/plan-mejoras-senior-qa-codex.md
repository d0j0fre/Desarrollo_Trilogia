# Plan de mejoras Senior QA / Seguridad / Arquitectura para Codex

**Proyecto:** Trilogia-Cursos / DistribuidoraJJ / Licorera La Bodega  
**Uso recomendado:** guardar este archivo en `docs/plan-mejoras-senior-qa-codex.md` dentro del repositorio.

---

## Instrucción corta para iniciar con Codex

Leé primero:

- `AGENTS.md`
- `docs/plan-mejoras-senior-qa-codex.md`

Actuá como **Senior QA Engineer, Security Reviewer y Arquitecto .NET**.

Trabajá en una rama nueva desde `main`:

```bash
git switch main
git pull origin main
git switch -c feature/qa-seguridad-arquitectura
```

No trabajés directo en `main`.  
No trabajés directo en `Danny`.  
No hagás push a `main`.  
No hagás merge a `main`.

Este plan se ejecuta por bloques.  
Cada bloque debe iniciar con **SOLO LECTURA**, diagnóstico y propuesta.  
No implementés un bloque hasta recibir la frase exacta:

> Aprobado, podés implementar

Si aparece build roto, login roto, checkout roto, permisos rotos, impresión rota, archivo sensible modificado o duda de lógica, detenete y reportá.

---

# Reglas globales obligatorias

## No tocar nunca sin aprobación explícita

- `appsettings.json`
- `appsettings.Development.json`
- cadenas de conexión
- secretos
- `bin/`
- `obj/`
- `.vs/`
- ZIPs
- archivos generados
- `main` directo

## Reglas de SQL

El profesor indicó:

> “No se permiten las sentencias de base de datos directamente en el código de .Net.”

Por eso:

- No crear SQL inline nuevo en C#.
- Todo SQL nuevo debe ir como script separado en `database/`.
- Preferir procedimientos almacenados.
- C# debe llamar procedimientos con parámetros.
- No concatenar strings SQL.
- No crear scripts SQL sin explicar qué hacen y cómo probarlos.
- Si una mejora requiere SQL, crear bloque separado.

## Reglas de seguridad

- Mantener `AntiForgeryToken` en formularios POST.
- Mantener validaciones de propiedad por usuario en portal cliente.
- Mantener validaciones de rol/módulo.
- No debilitar `AdminAuthorizeAttribute`.
- No debilitar `SessionAuthorizeAttribute`.
- No mostrar errores técnicos al usuario final.
- No exponer `FacturaId` al cliente si el flujo usa `PedidoId`.
- No crear rutas administrativas accesibles para cliente/empleado.
- No tocar recuperación de contraseña sin respetar rate-limit y mensajes genéricos.

## Reglas de Git

Cada bloque debe terminar con:

1. `dotnet build Trilogia-Cursos\Proyecto_Final.slnx`
2. `git status`
3. lista de archivos modificados
4. pruebas realizadas
5. Summary y Description para GitHub Desktop

Commits separados por bloque.  
Al final se debe preparar Pull Request:

```text
feature/qa-seguridad-arquitectura -> main
```

---

# Estado conocido del proyecto

Ya existen mejoras recientes:

- Portal cliente con historial y detalle.
- Cancelación segura de pedidos pendientes.
- Comprobante visible para cliente.
- Mi Perfil con nombre, correo, teléfono y dirección.
- Checkout modernizado.
- Scripts locales restaurados para checkout.
- Provincia/cantón/distrito en checkout.
- Detalle de producto modernizado.
- Portal cliente visual modernizado.
- Comprobante cliente visual modernizado.
- Login y Registro modernizados.
- `EmployeesController` integrado con `[AdminAuthorize("Empleados")]`.
- Build actual conocido: 0 errores.
- Advertencias conocidas de nullability en modelos/vistas.

---

# Prioridad general de implementación

Orden recomendado:

1. **Bloque 1:** Diagnóstico real del flujo compra → pedido → admin → factura.
2. **Bloque 2:** Corrección del flujo admin de pedidos/facturación.
3. **Bloque 3:** Migración gradual de SQL inline a procedimientos almacenados.
4. **Bloque 4:** Revisión y refuerzo de seguridad/autorización.
5. **Bloque 5:** Fortalecimiento del API.
6. **Bloque 6:** Optimización y mantenibilidad.
7. **Bloque 7:** Corrección de advertencias seguras.
8. **Bloque 8:** QA final y documentación.

---

# BLOQUE 1 — Diagnóstico del flujo compra → pedido → admin → factura

## Objetivo

Entender exactamente qué pasa cuando un cliente compra y por qué una compra puede no verse claramente en administración o facturación.

## Modo

SOLO LECTURA.  
No edites nada todavía.

## Revisar archivos

- `Proyecto_Final/Controllers/CartController.cs`
- `Proyecto_Final/Controllers/OrdersAdminController.cs`
- `Proyecto_Final/Controllers/BillingController.cs`
- `Proyecto_Final/Controllers/ClientPortalController.cs`
- `Proyecto_Final/Services/StoreDbService.cs`
- `Proyecto_Final/Services/AdminDbService.cs`
- `Proyecto_Final/Models/Store/*`
- `Proyecto_Final/Models/Admin/*`
- `Proyecto_Final/Views/Cart/Checkout.cshtml`
- `Proyecto_Final/Views/Cart/Confirmation.cshtml`
- `Proyecto_Final/Views/OrdersAdmin/*`
- `Proyecto_Final/Views/Billing/*`
- `Proyecto_Final/Views/ClientPortal/*`
- `database/*.sql`

## Preguntas que debe responder Codex

1. ¿Qué método se ejecuta al finalizar compra?
2. ¿Se crea pedido?
3. ¿En qué tabla se crea?
4. ¿Se crea factura automáticamente?
5. ¿En qué estado queda el pedido?
6. ¿El pedido aparece en admin?
7. ¿Qué vista/admin debe mostrarlo?
8. ¿Existe acción para aprobar/procesar/finalizar pedido?
9. ¿Existe acción para generar factura desde pedido?
10. ¿Por qué una compra puede no verse en facturación?
11. ¿El cliente puede ver comprobante solo cuando existe factura?
12. ¿Qué está incompleto?
13. ¿Qué debe implementarse para cerrar el flujo?

## Resultado esperado

Entregar diagnóstico con:

- flujo actual real,
- tablas afectadas,
- procedimientos afectados,
- archivos involucrados,
- problema exacto,
- propuesta de implementación por sub-bloques.

## No tocar

- SQL
- controladores
- servicios
- vistas
- modelos
- API
- appsettings

## Summary sugerido si solo se documenta

```text
docs(qa): diagnosticar flujo de pedidos y facturacion
```

---

# BLOQUE 2 — Corrección del flujo pedido admin → facturación

> Este bloque solo se implementa después del diagnóstico del Bloque 1.

## Objetivo

Que una compra de cliente quede claramente visible para el administrador y pueda avanzar por el flujo profesional:

```text
Cliente compra
↓
Pedido queda Pendiente
↓
Admin ve pedido
↓
Admin revisa detalle
↓
Admin aprueba/procesa/finaliza
↓
Se genera factura/comprobante si corresponde
↓
Cliente ve estado y comprobante
```

## Posibles archivos a tocar

Depende del diagnóstico, pero probablemente:

- `Proyecto_Final/Controllers/OrdersAdminController.cs`
- `Proyecto_Final/Controllers/BillingController.cs`
- `Proyecto_Final/Controllers/ClientPortalController.cs` solo si hace falta mostrar estado
- `Proyecto_Final/Services/AdminDbService.cs`
- `Proyecto_Final/Models/Admin/*`
- `Proyecto_Final/Models/Store/*`
- `Proyecto_Final/Views/OrdersAdmin/Index.cshtml`
- `Proyecto_Final/Views/OrdersAdmin/Detail.cshtml` si existe o si se requiere crear
- `Proyecto_Final/Views/Billing/Index.cshtml`
- `Proyecto_Final/Views/Billing/Detail.cshtml`
- `Proyecto_Final/Views/ClientPortal/Index.cshtml`
- `Proyecto_Final/Views/ClientPortal/Detail.cshtml`
- `database/cuXXX_flujo_pedidos_facturacion.sql`

## Reglas

- No SQL inline nuevo.
- Si hace falta actualizar estados, crear procedimiento almacenado.
- Si hace falta generar factura desde pedido, crear procedimiento almacenado.
- No usar `FacturaId` directo en rutas de cliente.
- Admin sí puede usar rutas administrativas, pero protegidas con autorización.
- Cliente solo ve datos propios.
- Mantener auditoría en acciones importantes.

## Funcionalidades esperadas

1. Admin ve pedidos creados desde checkout.
2. Admin abre detalle del pedido.
3. Admin puede cambiar estado de pedido de forma controlada.
4. Admin puede generar/finalizar factura si el pedido está en estado válido.
5. Cliente ve el nuevo estado.
6. Cliente ve comprobante solo si existe factura.
7. No se duplica factura por el mismo pedido.
8. No se puede facturar pedido cancelado.
9. No se puede cancelar pedido ya facturado.
10. Auditoría registra acción crítica.

## Pruebas

- Cliente compra producto.
- Pedido aparece como pendiente en admin.
- Admin ve detalle.
- Admin procesa/aprueba.
- Admin genera factura.
- Cliente ve comprobante.
- Intentar generar factura dos veces debe fallar controladamente.
- Intentar facturar pedido cancelado debe fallar.
- Build 0 errores.

## Summary sugerido

```text
feat(pedidos): completar flujo admin de facturacion
```

## Description sugerida

```text
Completa el flujo de pedido desde compra de cliente hasta gestion administrativa y facturacion, usando procedimientos almacenados, validaciones de estado y auditoria.
```

---

# BLOQUE 3 — Inventario de SQL directo en C# y migración gradual

## Objetivo

Cumplir la observación del profesor: evitar sentencias SQL directas dentro del código .NET.

## Modo inicial

SOLO LECTURA.

## Revisar

Buscar en todo el proyecto:

- `SqlCommand("SELECT`
- `SqlCommand("INSERT`
- `SqlCommand("UPDATE`
- `SqlCommand("DELETE`
- `CommandText`
- `.CommandType = CommandType.Text`
- concatenaciones SQL
- interpolaciones SQL `$"SELECT ..."`
- servicios con queries grandes

## Clasificar

1. SQL directo peligroso.
2. SQL directo parametrizado, pero no ideal.
3. Procedimientos almacenados ya correctos.
4. Queries duplicadas.
5. Queries candidatas a migración inmediata.

## Resultado esperado

Crear inventario en:

```text
docs/inventario-sql-directo.md
```

## Sub-bloques de implementación

### 3A — Migrar SQL crítico de pedidos/facturación

Probables archivos:

- `AdminDbService.cs`
- `StoreDbService.cs`
- scripts en `database/`

### 3B — Migrar SQL de usuarios/perfil/auth

Probables archivos:

- `AccountDbService.cs`
- scripts en `database/`

### 3C — Migrar SQL de inventario/clientes/empleados

Probables archivos:

- servicios correspondientes
- scripts en `database/`

## Reglas

- No migrar todo en un solo commit.
- Cada migración debe tener script SQL.
- Cada migración debe mantener el mismo comportamiento.
- Build 0 errores.
- Prueba funcional del módulo migrado.

## Summary sugerido para inventario

```text
docs(db): inventariar consultas sql directas
```

## Summary sugerido para migración

```text
refactor(db): migrar consultas a procedimientos almacenados
```

---

# BLOQUE 4 — Seguridad y autorización

## Objetivo

Detectar y corregir rutas, acciones POST o módulos que no estén protegidos correctamente.

## Modo inicial

SOLO LECTURA.

## Revisar

- `Controllers/*Controller.cs`
- `Filters/AdminAuthorizeAttribute.cs`
- `Filters/SessionAuthorizeAttribute.cs`
- vistas con formularios POST
- acciones administrativas
- acciones cliente
- acciones empleado/vendedor
- rutas de comprobantes/pedidos
- cancelación de pedidos
- actualización de estados
- eliminación/edición de productos
- subida de imágenes

## Checklist

- ¿Todas las acciones admin tienen autorización?
- ¿Todas las acciones cliente validan usuario dueño?
- ¿Todos los POST importantes tienen `[ValidateAntiForgeryToken]`?
- ¿El cliente puede acceder a pedido ajeno?
- ¿El cliente puede acceder a comprobante ajeno?
- ¿Empleado puede entrar a admin?
- ¿Un usuario sin sesión puede acceder a rutas protegidas?
- ¿Hay errores técnicos visibles?
- ¿Subida de archivos está limitada?

## Posibles archivos

- filtros
- controladores específicos
- vistas POST si falta antiforgery
- docs de seguridad

## Reglas

- No debilitar autorización existente.
- No cambiar roles sin revisar permisos.
- No inventar módulos.
- Si `AdminAuthorize` requiere módulo, usar módulo existente en SQL/permisos.

## Pruebas

- Acceso sin sesión.
- Acceso cliente a rutas admin.
- Acceso empleado a rutas admin.
- Pedido ajeno.
- Comprobante ajeno.
- POST sin token si se puede simular.
- Build 0 errores.

## Summary sugerido

```text
fix(security): reforzar autorizacion y antiforgery
```

---

# BLOQUE 5 — Fortalecer Proyecto_FinalAPI

## Objetivo

Hacer que el API tenga más valor real en el proyecto, sin romper MVC.

## Modo inicial

SOLO LECTURA.

## Diagnóstico requerido

1. Endpoints actuales.
2. Controladores API actuales.
3. Servicios usados.
4. Autenticación actual del API.
5. Qué módulos MVC no tienen API.
6. Qué endpoints faltan.
7. Qué endpoints conviene implementar primero.

## Prioridad recomendada

### 5A — API de productos públicos

Endpoints sugeridos:

- `GET /api/productos`
- `GET /api/productos/{id}`
- `GET /api/categorias`
- filtros por categoría/búsqueda

No debe requerir login si son productos públicos.

### 5B — API de inventario admin

Endpoints sugeridos:

- `GET /api/admin/inventario`
- `GET /api/admin/inventario/{id}`
- `POST /api/admin/inventario`
- `PUT /api/admin/inventario/{id}`

Debe requerir autorización.

### 5C — API de pedidos

Endpoints sugeridos:

- `GET /api/admin/pedidos`
- `GET /api/admin/pedidos/{id}`
- `POST /api/admin/pedidos/{id}/estado`
- `POST /api/admin/pedidos/{id}/facturar`

Debe requerir autorización admin.

### 5D — API de portal cliente

Endpoints sugeridos:

- `GET /api/cliente/pedidos`
- `GET /api/cliente/pedidos/{id}`
- `POST /api/cliente/pedidos/{id}/cancelar`

Debe validar usuario dueño.

## Reglas

- No duplicar lógica de negocio si se puede reutilizar servicios.
- No exponer datos sensibles.
- No abrir endpoints admin sin protección.
- No tocar MVC innecesariamente.
- No crear JWT si el proyecto no está listo para eso; primero diagnosticar autenticación API.
- Documentar endpoints.

## Archivos probables

- `Proyecto_FinalAPI/Controllers/*`
- `Proyecto_FinalAPI/Models/*`
- servicios compartidos si ya existen
- docs de API

## Pruebas

- Build API.
- Probar endpoints con navegador/Postman.
- 401/403 donde aplique.
- No romper MVC.

## Summary sugerido

```text
feat(api): agregar endpoints base de productos y pedidos
```

---

# BLOQUE 6 — Optimización y mantenibilidad

## Objetivo

Mejorar organización sin cambiar comportamiento.

## Revisar

- servicios muy grandes,
- controladores con lógica repetida,
- ViewModels faltantes,
- ViewBag/TempData excesivo,
- CSS acumulado,
- scripts faltantes,
- tablas sin paginación,
- queries duplicadas,
- falta de logs/auditoría.

## Reglas

- No hacer refactor grande de una vez.
- No cambiar comportamiento.
- No tocar base de datos salvo bloque separado.
- Documentar antes de mover.

## Posibles mejoras

- Extraer helpers privados.
- Unificar ViewModels.
- Agregar paginación en admin si las listas son grandes.
- Mejorar auditoría en acciones críticas.
- Documentar servicios.

## Summary sugerido

```text
refactor: mejorar mantenibilidad sin cambiar comportamiento
```

---

# BLOQUE 7 — Corregir advertencias de nullability seguras

## Objetivo

Reducir warnings sin cambiar lógica.

## Advertencias conocidas

- `Models/Perfil.cs`
- `Models/Modulo.cs`
- `Models/HistorialAuditoria.cs`
- `Views/Security/Permisos.cshtml`
- `Views/Home/Shop.cshtml`

## Reglas

- En modelos, preferir `string.Empty` si aplica.
- En vistas, usar null checks seguros.
- No ocultar errores reales.
- No cambiar base de datos.
- No cambiar lógica.
- Si hay duda, reportar antes.

## Pruebas

- Build 0 errores.
- Reducir warnings.
- Revisar Shop.
- Revisar Security/Permisos.

## Summary sugerido

```text
chore: reducir advertencias de nullability
```

---

# BLOQUE 8 — QA final y documentación

## Objetivo

Cerrar con evidencia de calidad.

## Documentos sugeridos

- `docs/qa-final.md`
- `docs/mejoras-seguridad-arquitectura.md`
- `docs/api-plan.md`
- `docs/inventario-sql-directo.md`

## QA cliente

- Login cliente.
- Home.
- Tienda.
- Detalle producto.
- Carrito.
- Checkout.
- Provincia/cantón/distrito.
- Confirmación.
- Mis pedidos.
- Detalle pedido.
- Cancelación.
- Comprobante.
- Impresión.
- Mi Perfil.
- Logout.

## QA admin

- Login admin.
- Dashboard.
- Inventario.
- Crear/editar producto.
- Pedidos admin.
- Facturación admin.
- Factura admin.
- Clientes.
- Créditos.
- Consultas.
- Empleados.
- Roles/permisos.
- Seguridad.
- Logout.

## QA empleado/vendedor

- Login empleado/vendedor si existen credenciales.
- Portal empleado.
- Pedidos vendedor.
- Acciones permitidas.
- Accesos bloqueados.

## QA técnico

- Build solución.
- Build API si aplica.
- Git status limpio.
- Sin appsettings modificados.
- Sin bin/obj/.vs/ZIPs.
- Sin errores de consola.
- Sin 404 de scripts principales.
- Responsive básico.
- Impresión de comprobante/factura.

## Summary sugerido

```text
docs(qa): agregar evidencia de pruebas finales
```

---

# Orden de ejecución recomendado

1. Bloque 1 — Diagnóstico flujo compra/pedido/factura.
2. Bloque 2 — Corrección flujo admin/facturación.
3. Bloque 3 — Inventario SQL directo.
4. Bloque 4 — Seguridad y autorización.
5. Bloque 5 — Fortalecer API.
6. Bloque 6 — Optimización.
7. Bloque 7 — Warnings.
8. Bloque 8 — QA y documentación.

---

# Criterio para detenerse

Detenete y reportá si ocurre cualquiera de estos casos:

- build falla,
- login falla,
- checkout falla,
- admin no puede entrar,
- cliente ve datos ajenos,
- empleado ve admin sin permiso,
- comprobante/factura imprime mal,
- SQL requiere decisión,
- API requiere autenticación no definida,
- aparece appsettings modificado,
- aparecen bin/obj/.vs/ZIPs,
- hay conflicto con `main`.

---

# Entrega final esperada

Al finalizar todos los bloques, reportá:

1. Rama actual.
2. Commits creados.
3. Archivos modificados por bloque.
4. Scripts SQL creados.
5. Build final.
6. Warnings restantes.
7. Pruebas realizadas.
8. Pruebas pendientes.
9. Riesgos restantes.
10. Confirmación de no appsettings/bin/obj/.vs/ZIPs.
11. Recomendación para Pull Request a `main`.

No hagas merge a `main`.  
No hagas push directo a `main`.
