# Plan de continuación para Codex — API, QA, documentación y cierre

**Proyecto:** Trilogia-Cursos / DistribuidoraJJ / Licorera La Bodega  
**Rama de trabajo:** `feature/qa-seguridad-arquitectura`  
**Ruta recomendada dentro del repo:**

```text
Trilogia-Cursos/docs/plan-continuacion-api-qa-codex.md
```

---

## Instrucción inicial para Codex

Quiero continuar el proyecto en la rama:

```text
feature/qa-seguridad-arquitectura
```

Leé primero:

- `AGENTS.md`
- `docs/auditoria-senior-qa-codex.md`
- `docs/plan-mejoras-senior-qa-codex.md`
- `docs/plan-cierre-final-codex.md`
- `docs/inventario-sql-directo.md`
- `docs/plan-continuacion-api-qa-codex.md`

---

# Contexto general

Ya se reforzó el proyecto con varios bloques de seguridad, SQL, facturación, permisos y documentación.

## Commits ya cerrados en esta rama

```text
cd9009a refactor(db): migrar comprobantes a procedimientos
2124e3a fix(pedidos): proteger estados de pedidos facturados
e2667fd fix(facturacion): usar reportes agregados desde procedimientos
bbeda13 fix(seguridad): ocultar mensajes tecnicos en controladores
73ca2c1 fix(seguridad): exigir UserId en autorizacion admin
ada8054 fix(seguridad): validar firma real de imagenes
29dcda6 fix(seguridad): agregar antiforgery en vistas legacy
3384769 feat(seguridad): preparar permisos granulares por accion
5cae901 fix(seguridad): aplicar permisos granulares a acciones criticas
8b84b47 fix(seguridad): proteger generacion de facturas
```

## Scripts SQL nuevos pendientes de ejecutar en SSMS

Estos scripts deben existir en `database/` y deben ejecutarse en la base `DistribuidoraJJ_DB` antes de pruebas funcionales completas:

```text
database/cu091_migracion_pedidos_facturacion_sp.sql
database/cu092_admin_estado_pedido_seguro.sql
database/cu093_admin_reportes_facturacion_sp.sql
database/cu094_permisos_granulares_acciones.sql
database/cu095_facturacion_generar_permiso.sql
```

---

# Reglas generales obligatorias

- No trabajar directo en `main`.
- No trabajar directo en `Danny`.
- No hacer merge a `main`.
- No hacer push a `main`.
- No tocar `appsettings`.
- No tocar cadenas de conexión.
- No subir `bin/`, `obj/`, `.vs/`, ZIPs ni archivos generados.
- No cambiar SQL ya creado salvo que se apruebe.
- No romper MVC.
- No romper login.
- No romper checkout.
- No romper pedidos.
- No romper facturación.
- No romper permisos.
- No tocar archivos fuera del bloque aprobado.
- Después de cada bloque ejecutar:

```bash
dotnet build Trilogia-Cursos\Proyecto_Final.slnx
```

- Cada bloque debe tener commit separado.
- Si aparece build roto, login roto, permisos rotos, SQL dudoso o archivo sensible modificado, detenerse y reportar.

---

# Estado actual del API

`Proyecto_FinalAPI` actualmente solo tiene autenticación:

```text
POST /api/auth/login
POST /api/auth/register
POST /api/auth/forgot-password
POST /api/auth/reset-password
```

No tiene endpoints de:

- productos,
- categorías,
- inventario,
- pedidos,
- facturación,
- clientes,
- portal cliente,
- empleados,
- roles/permisos.

---

# Objetivo general desde aquí

Completar de forma segura lo pendiente:

1. API pública de productos/categorías.
2. Documentación de endpoints API.
3. Diagnóstico de autenticación API futura.
4. Optimización y mantenibilidad.
5. Warnings/nullability.
6. QA final completo.
7. Documentación final.
8. Dejar rama lista para Pull Request hacia `main`.

---

# BLOQUE 5A — Implementar API pública de productos/categorías

## Estado

Este bloque está aprobado para implementar.

## Objetivo

Agregar endpoints públicos en `Proyecto_FinalAPI` para consultar productos, detalle y categorías.

## Endpoints esperados

```text
GET /api/products
GET /api/products?categoria=
GET /api/products?buscar=
GET /api/products/{id}
GET /api/products/categories
GET /api/products/featured?take=   // opcional si es simple
```

## Archivos permitidos

```text
Proyecto_FinalAPI/Controllers/ProductsController.cs
Proyecto_FinalAPI/Services/ProductsApiDbService.cs
Proyecto_FinalAPI/Models/ProductApiModels.cs
Proyecto_FinalAPI/Program.cs
```

`Program.cs` solo puede tocarse para registrar el servicio si hace falta.

## No tocar

```text
Proyecto_Final MVC
Proyecto_Final/Controllers
Proyecto_Final/Services
Proyecto_Final/Views
Proyecto_Final/Models
database/
appsettings.json
appsettings.Development.json
autenticación
pedidos
facturación
clientes
inventario admin
roles/permisos
bin/
obj/
.vs/
ZIPs
archivos generados
```

## Reglas

- Solo endpoints `GET` públicos.
- No crear autenticación nueva.
- No crear JWT.
- No exponer datos sensibles.
- No crear acciones de escritura.
- Usar procedimientos existentes si están disponibles:
  - `sp_Store_GetProducts`
  - `sp_Store_GetProductById`
  - `sp_Store_GetCategories`
- Crear DTOs propios del API.
- Crear servicio propio del API.
- No crear dependencia circular con `Proyecto_Final` MVC.
- Para producto inexistente devolver `404`.
- Para errores internos devolver mensaje genérico.

## Después de implementar

Ejecutar:

```bash
dotnet build Trilogia-Cursos\Proyecto_Final.slnx
```

Reportar:

1. Endpoints agregados.
2. Archivos creados/modificados.
3. DTOs creados.
4. Servicio creado.
5. Resultado del build.
6. Warnings.
7. `git status`.
8. Confirmación de que no se tocó MVC, SQL, appsettings, autenticación ni archivos generados.

## Commit sugerido

**Summary**

```text
feat(api): agregar endpoints publicos de productos
```

**Description**

```text
Agrega endpoints publicos de consulta para productos, detalle y categorias usando servicios propios del API y procedimientos almacenados existentes.

No cambia MVC, SQL, autenticacion, vistas ni appsettings.
```

---

# BLOQUE 5B — Documentar endpoints API

## Estado

Ejecutar después de cerrar 5A con build y commit.

## Objetivo

Crear documentación clara de los endpoints disponibles en el API.

## Archivo permitido

```text
docs/api-endpoints.md
```

## Debe incluir

Para cada endpoint:

- Método HTTP.
- Ruta.
- Descripción.
- Parámetros.
- Ejemplo de respuesta.
- Códigos esperados:
  - `200`
  - `404`
  - `500`
- Nota de que productos/categorías son públicos.
- Nota de que pedidos, cliente, facturación e inventario admin requieren autenticación futura.

## No tocar

- Código.
- SQL.
- MVC.
- API controllers.
- appsettings.

## Commit sugerido

**Summary**

```text
docs(api): documentar endpoints publicos
```

**Description**

```text
Documenta los endpoints publicos del API para productos, detalle y categorias, incluyendo parametros, respuestas esperadas y notas de seguridad para futuros endpoints protegidos.
```

---

# BLOQUE 5C — Diagnóstico de autenticación API futura

## Estado

Solo lectura.  
No implementar todavía.

## Objetivo

Analizar si conviene usar:

- JWT,
- API Key,
- cookie compartida,
- sesión,
- u otra estrategia para endpoints protegidos futuros.

## Revisar

```text
Proyecto_FinalAPI/Controllers/AuthController.cs
Proyecto_FinalAPI/Program.cs
Proyecto_FinalAPI/Models/*
Proyecto_FinalAPI/Services/*
flujo actual MVC/API
cómo MVC usa login API para crear sesión local
```

## Responder

1. Estado actual.
2. Riesgos.
3. Opción recomendada.
4. Qué endpoints podrían protegerse después.
5. Qué no conviene implementar aún.
6. Plan por bloques.
7. Archivos que se tocarían.
8. Riesgos de seguridad.
9. Pruebas necesarias.
10. Summary y Description sugeridos.

## Reglas

- No implementar sin aprobación.
- No crear JWT sin aprobación.
- No tocar login MVC sin aprobación.
- No cambiar appsettings.
- No romper autenticación existente.

---

# BLOQUE 6 — Optimización y mantenibilidad

## Estado

Primero solo lectura.

## Objetivo

Revisar deuda técnica y proponer mejoras pequeñas.

## Revisar

- Servicios demasiado grandes.
- Controladores cargados.
- Código repetido.
- CSS acumulado.
- Uso excesivo de `ViewBag` / `TempData`.
- JS frágil.
- Falta de paginación.
- Auditoría faltante.
- Consultas duplicadas.
- Vistas muy cargadas.

## Reglas

- No implementar cambios grandes sin aprobación.
- No refactorizar todo de una vez.
- No tocar SQL salvo bloque aprobado.
- No cambiar comportamiento funcional.
- No romper UI.

## Resultado esperado

Diagnóstico con:

- Riesgo.
- Prioridad.
- Archivos afectados.
- Plan por bloques.
- Qué conviene dejar para mejora futura.

---

# BLOQUE 7 — Warnings/nullability

## Objetivo

Reducir warnings conocidos sin cambiar lógica.

## Warnings conocidos

```text
Models/Perfil.cs
Models/Modulo.cs
Models/HistorialAuditoria.cs
Views/Security/Permisos.cshtml
Views/Home/Shop.cshtml
```

## Reglas

- No cambiar lógica.
- No tocar SQL.
- No tocar controladores salvo que sea necesario y aprobado.
- En modelos usar inicialización segura tipo `string.Empty` si aplica.
- En vistas usar null checks seguros.
- Build 0 errores.

## Commit sugerido

**Summary**

```text
chore: reducir advertencias de nullability
```

**Description**

```text
Reduce advertencias de nullability con inicializaciones y validaciones seguras, sin cambiar comportamiento funcional ni estructura de base de datos.
```

---

# BLOQUE 8 — QA final y documentación

## Objetivo

Crear documentación final y checklist QA.

## Archivos sugeridos

```text
docs/qa-final.md
docs/resumen-mejoras-seguridad.md
docs/resumen-final-proyecto.md
```

---

## QA cliente

- Login cliente.
- Home.
- Tienda.
- Detalle producto.
- Carrito.
- Checkout.
- Mis pedidos.
- Detalle pedido.
- Cancelación si aplica.
- Comprobante.
- Mi Perfil.

## QA admin

- Login admin.
- Dashboard.
- Inventario.
- Pedidos admin.
- Cambio de estado.
- Generar factura.
- Facturación.
- Detalle factura.
- Productos más vendidos.
- Ventas por mes.
- Clientes.
- Créditos.
- Consultas.
- Empleados.
- Roles.
- Permisos.
- Auditoría.

## QA empleado/vendedor

- Login si existen credenciales.
- Portal empleado.
- Pedidos vendedor.
- Accesos permitidos/bloqueados.

## QA técnico

- Build 0 errores.
- Warnings restantes documentados.
- Scripts SQL ejecutados.
- Appsettings limpios.
- Sin `bin/`, `obj/`, `.vs/`, ZIPs.
- Sin errores de consola.
- Sin 404 de scripts principales.
- Impresión comprobante/factura correcta.

---

# Entrega final esperada

Al final reportar:

1. Rama actual.
2. Commits creados.
3. Archivos modificados.
4. Scripts SQL pendientes de ejecutar.
5. Build final.
6. Warnings restantes.
7. Pruebas realizadas.
8. Pruebas pendientes.
9. Confirmación de appsettings limpios.
10. Confirmación de sin `bin/`, `obj/`, `.vs/`, ZIPs.
11. Recomendación final para PR:

```text
feature/qa-seguridad-arquitectura -> main
```

---

# Orden obligatorio desde este punto

```text
1. BLOQUE 5A — API pública productos/categorías
2. Build + commit
3. BLOQUE 5B — Documentación endpoints API
4. Build + commit
5. BLOQUE 5C — Diagnóstico auth API futura
6. BLOQUE 6 — Diagnóstico optimización/mantenibilidad
7. BLOQUE 7 — Warnings/nullability
8. BLOQUE 8 — QA final/documentación
9. Preparar PR hacia main
```

---

# Instrucción final para Codex

Empezá ahora únicamente con:

```text
BLOQUE 5A — Implementar API pública de productos/categorías
```

No avances a 5B sin terminar, hacer build y commitear 5A.
