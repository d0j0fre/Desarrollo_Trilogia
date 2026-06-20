# Diagnóstico pendiente para Codex — Bloque 6, API al 100% y cierre real

**Proyecto:** Trilogia-Cursos / DistribuidoraJJ / Licorera La Bodega  
**Rama actual:** `feature/qa-seguridad-arquitectura`  
**Ruta recomendada dentro del proyecto:**

```text
Trilogia-Cursos/docs/diagnostico-bloque6-api-cierre-codex.md
```

---

# Objetivo de este documento

Este documento existe para evitar cerrar el proyecto antes de tiempo.

Aunque ya se trabajaron seguridad, SQL, permisos, facturación, API pública, documentación, warnings y QA preliminar, todavía falta revisar dos puntos importantes:

1. **Bloque 6 — Optimización y mantenibilidad.**
2. **Cierre real del API**, para confirmar si quedó al 100% dentro del alcance actual del proyecto.

La idea es que Codex haga primero un diagnóstico completo, sin modificar nada, y luego proponga bloques pequeños si encuentra pendientes reales.

---

# Instrucción principal para Codex

Hacé un diagnóstico final en la rama:

```text
feature/qa-seguridad-arquitectura
```

Este diagnóstico debe cubrir:

```text
Bloque 6 — Optimización y mantenibilidad
API al 100% según alcance actual
Riesgos restantes antes del PR
Pendientes reales antes del cierre
```

---

# Reglas obligatorias

No modificar nada todavía.

```text
No edites archivos.
No crees archivos.
No generes scripts.
No hagas commits.
No hagas push.
No hagas merge.
No toques SQL.
No toques appsettings.
No toques código C#.
No toques vistas.
No toques CSS/JS.
No toques docs.
No borres archivos.
No limpies archivos automáticamente.
No cambies formato.
No refactorices.
```

Solo diagnóstico.

Esperá aprobación explícita antes de implementar cualquier cosa:

```text
Aprobado, podés implementar
```

---

# Contexto actual del proyecto

Ya se cerraron y commitearon varios bloques importantes.

## Seguridad, SQL, permisos y facturación

Se trabajó en:

- Migración de consultas SQL directas a procedimientos almacenados.
- Protección de estados de pedidos facturados.
- Corrección de reportes de facturación.
- Mensajes genéricos en errores visibles.
- Refuerzo de `AdminAuthorize` exigiendo `UserId`.
- Validación real de firma de imágenes.
- Antiforgery en vistas legacy.
- Permisos granulares por acción.
- Protección de generación de facturas con `FACTURACION_GENERAR`.

## API

Ya se agregó API pública de productos/categorías.

Endpoints actuales esperados:

```text
POST /api/auth/login
POST /api/auth/register
POST /api/auth/forgot-password
POST /api/auth/reset-password

GET /api/products
GET /api/products?categoria=
GET /api/products?buscar=
GET /api/products/{id}
GET /api/products/categories
GET /api/products/featured?take=
```

## Documentación

Ya existen o deben existir:

```text
docs/api-endpoints.md
docs/api-auth-futura.md
docs/qa-final.md
docs/resumen-mejoras-seguridad.md
docs/resumen-final-proyecto.md
docs/plan-continuacion-api-qa-codex.md
```

## Warnings

El build ya debe estar limpio:

```text
0 errores
0 warnings
```

---

# Importante antes de iniciar

Verificá si este archivo sigue sin commitear:

```text
docs/plan-continuacion-api-qa-codex.md
```

Si está sin trackear, solo reportalo.

No lo modifiques.

---

# Scripts SQL importantes

Estos scripts deben existir en `database/`:

```text
database/cu091_migracion_pedidos_facturacion_sp.sql
database/cu092_admin_estado_pedido_seguro.sql
database/cu093_admin_reportes_facturacion_sp.sql
database/cu094_permisos_granulares_acciones.sql
database/cu095_facturacion_generar_permiso.sql
```

También deben ejecutarse manualmente en SSMS contra la base:

```text
DistribuidoraJJ_DB
```

No los ejecutes desde Codex.

Solo reportá si existen o no existen.

---

# Parte A — Diagnóstico Bloque 6: Optimización y mantenibilidad

## Objetivo

Revisar si el proyecto tiene deuda técnica o riesgos de mantenibilidad que convenga corregir antes del Pull Request.

No se buscan refactors gigantes. Se buscan problemas reales, pequeños, seguros y defendibles.

---

## Archivos/carpetas a revisar

Revisar en solo lectura:

```text
Proyecto_Final/Controllers/*
Proyecto_Final/Services/*
Proyecto_Final/Models/*
Proyecto_Final/Views/*
Proyecto_Final/wwwroot/*
Proyecto_Final/Middleware/*
Proyecto_Final/Filters/*
Proyecto_FinalAPI/*
database/*
docs/*
```

---

## Puntos específicos a revisar

### 1. Controladores demasiado grandes

Revisar si hay controladores con demasiada lógica, por ejemplo:

```text
InventoryController
OrdersAdminController
BillingController
AccountController
ClientPortalController
CartController
```

Responder:

- ¿El controlador tiene demasiada lógica de negocio?
- ¿Esa lógica debería estar en servicio?
- ¿Es grave para entrega?
- ¿Conviene corregir ahora o dejar como mejora futura?

---

### 2. Servicios demasiado grandes

Revisar servicios como:

```text
AdminDbService
StoreDbService
AccountDbService
ProductsApiDbService
AccountApiDbService
```

Responder:

- ¿Hay servicios demasiado cargados?
- ¿Hay métodos muy largos?
- ¿Hay repetición de acceso a datos?
- ¿Hay mezcla de responsabilidades?
- ¿Conviene dividir ahora o sería riesgoso?

---

### 3. Código repetido

Buscar duplicación en:

```text
lectura de sesión
validaciones de usuario
manejo de errores
TempData
ViewBag
lectura de parámetros
validación de imágenes
lectura de datos SQL
mapeo de modelos
```

Responder:

- ¿Qué duplicación existe?
- ¿Afecta seguridad?
- ¿Afecta mantenimiento?
- ¿Conviene corregir antes del PR?

---

### 4. Lógica de negocio dentro de controladores

Detectar lógica que debería vivir en servicios, por ejemplo:

```text
validaciones de estados
validaciones de permisos
cálculos de totales
validación de archivos
armado de reportes
lógica de pedidos
lógica de facturación
```

Clasificar:

```text
Crítico
Alto
Medio
Bajo
Mejora futura
```

---

### 5. Uso de ViewBag / TempData

Revisar:

```text
ViewBag
ViewData
TempData
```

Responder:

- ¿Se usan de forma razonable?
- ¿Hay riesgo de errores por nombres mágicos?
- ¿Hay vistas que deberían usar ViewModels más claros?
- ¿Conviene corregir algo ahora?

---

### 6. Vistas con lógica compleja

Revisar vistas principales:

```text
Views/Home/Shop.cshtml
Views/Home/ProductDetail.cshtml
Views/Cart/*
Views/Checkout/*
Views/ClientPortal/*
Views/Billing/*
Views/OrdersAdmin/*
Views/Inventory/*
Views/Security/*
```

Responder:

- ¿Hay lógica muy compleja dentro de Razor?
- ¿Hay validaciones que deberían estar en ViewModel?
- ¿Hay riesgo de nulls?
- ¿Ya quedó bien con warnings en 0?
- ¿Conviene corregir algo o dejar como futuro?

---

### 7. CSS/JS duplicado o frágil

Revisar:

```text
wwwroot/css/*
wwwroot/js/*
```

Especialmente:

```text
checkout.js
scripts de carrito
scripts de tienda
scripts admin
CSS personalizado
```

Responder:

- ¿Hay JS que pueda romperse fácilmente?
- ¿Hay rutas quemadas?
- ¿Hay selectores demasiado frágiles?
- ¿Hay CSS duplicado?
- ¿Conviene tocar ahora o no?

---

### 8. Falta de paginación

Revisar listados:

```text
productos
pedidos
clientes
empleados
facturas
auditoría
consultas
inventario
```

Responder:

- ¿Hay listados que podrían crecer demasiado?
- ¿Hay paginación?
- ¿Es necesario corregir ahora?
- ¿O basta documentarlo como mejora futura?

---

### 9. Auditoría en acciones sensibles

Revisar si acciones críticas tienen auditoría o registro:

```text
cambio de estado de pedido
generar factura
cancelación de pedido
cambio de permisos
crear/editar roles
activar/inactivar roles
inventario
clientes
créditos
empleados
```

Responder:

- ¿Qué acciones ya auditan?
- ¿Qué acciones no auditan?
- ¿Cuáles deberían auditarse antes del PR?
- ¿Qué se puede dejar como mejora futura?

---

### 10. Manejo de errores inconsistente

Revisar si todavía hay:

```text
ex.Message visible al usuario
mensajes técnicos
stack traces
errores inconsistentes
catch genéricos sin mensaje seguro
```

Responder:

- ¿Quedó algún mensaje técnico visible?
- ¿En qué archivo?
- ¿Es crítico o medio?
- ¿Conviene corregirlo ahora?

---

### 11. Rutas o vistas legacy que confunden

Revisar:

```text
SecurityController
Views/Security/*
wrappers antiguos
redirecciones legacy
```

Responder:

- ¿Son necesarias todavía?
- ¿Pueden confundir al profesor o al equipo?
- ¿Conviene documentarlas?
- ¿Conviene eliminarlas? Solo si es seguro. No implementar sin aprobación.

---

### 12. Documentación incompleta

Revisar docs existentes:

```text
docs/api-endpoints.md
docs/api-auth-futura.md
docs/qa-final.md
docs/resumen-mejoras-seguridad.md
docs/resumen-final-proyecto.md
docs/plan-continuacion-api-qa-codex.md
```

Responder:

- ¿Falta actualizar algo?
- ¿La documentación coincide con el código real?
- ¿Hay endpoints documentados que no existen?
- ¿Hay mejoras documentadas que no se implementaron?
- ¿Faltan scripts SQL en resumen final?

---

# Clasificación requerida de hallazgos Bloque 6

Cada hallazgo debe venir con esta estructura:

```text
ID:
Título:
Archivo(s):
Prioridad: Crítico / Alto / Medio / Bajo / Futuro
Descripción:
Riesgo:
Recomendación:
¿Corregir antes del PR?: Sí / No
Bloque sugerido:
```

---

# Parte B — Diagnóstico API al 100% según alcance actual

## Objetivo

Confirmar que el API está completo y defendible dentro del alcance actual.

El alcance actual NO incluye JWT implementado.

El alcance actual SÍ incluye:

```text
auth básica existente
productos públicos
categorías públicas
documentación clara
no exponer datos sensibles
no exponer endpoints protegidos sin token
```

---

## Revisar archivos API

Revisar en solo lectura:

```text
Proyecto_FinalAPI/Controllers/AuthController.cs
Proyecto_FinalAPI/Controllers/ProductsController.cs
Proyecto_FinalAPI/Models/AuthModels.cs
Proyecto_FinalAPI/Models/ProductApiModels.cs
Proyecto_FinalAPI/Services/AccountApiDbService.cs
Proyecto_FinalAPI/Services/ProductsApiDbService.cs
Proyecto_FinalAPI/Program.cs
docs/api-endpoints.md
docs/api-auth-futura.md
```

---

## Validar endpoints reales

Confirmar que existen exactamente estos endpoints o reportar diferencias:

```text
POST /api/auth/login
POST /api/auth/register
POST /api/auth/forgot-password
POST /api/auth/reset-password

GET /api/products
GET /api/products?categoria=
GET /api/products?buscar=
GET /api/products/{id}
GET /api/products/categories
GET /api/products/featured?take=
```

Para cada endpoint indicar:

```text
Existe: Sí / No
Método:
Ruta:
Controlador:
Acción:
Parámetros:
Respuesta esperada:
Errores esperados:
Observaciones:
```

---

## Validar ProductsController

Revisar si maneja correctamente:

```text
200 OK
400 Bad Request si aplica
404 Not Found en producto inexistente
500 Internal Server Error con mensaje genérico
categoria vacía
buscar vacío
id <= 0
id inexistente
take <= 0
take demasiado alto
errores de base de datos
```

Responder:

- ¿Hay validación de `id <= 0`?
- ¿Hay validación de `take <= 0`?
- ¿Hay límite máximo para `take`?
- ¿Qué pasa si `categoria` viene vacía?
- ¿Qué pasa si `buscar` viene vacío?
- ¿Los errores devuelven mensaje genérico?
- ¿Hay riesgo de exponer detalles técnicos?

---

## Validar ProductsApiDbService

Revisar:

```text
usa stored procedures
no usa SQL inline prohibido
no expone datos sensibles
abre/cierra conexiones correctamente
usa using/await using
maneja DBNull
mapea DTOs correctamente
no depende del proyecto MVC
no duplica lógica crítica
no construye SQL con concatenación
```

Responder explícitamente:

- ¿Usa `CommandType.StoredProcedure`?
- ¿Hay SQL directo?
- ¿Hay riesgo de inyección SQL?
- ¿Hay dependencia circular con MVC?
- ¿Maneja nulls correctamente?
- ¿Maneja imágenes/rutas correctamente?
- ¿Maneja stock de forma coherente con la tienda MVC?

---

## Validar ProductApiModels

Revisar:

```text
DTOs públicos
nullability
propiedades expuestas
nombres claros
datos sensibles
```

Responder:

- ¿Expone solo lo necesario?
- ¿Incluye costo, margen, proveedor o datos internos?
- ¿Incluye stock? Si sí, ¿es consistente con tienda MVC?
- ¿Tiene inicializaciones seguras?
- ¿Hay warnings?

---

## Validar Program.cs API

Revisar:

```text
registro de ProductsApiDbService
Swagger
CORS si existe
HTTPS redirection
routing
controllers
configuración de servicios
auth existente
```

Responder:

- ¿ProductsApiDbService está registrado?
- ¿Se rompió AccountApiDbService?
- ¿Swagger está activo en desarrollo?
- ¿Hay `MapControllers()`?
- ¿Hay cambios innecesarios?
- ¿No se tocó appsettings?
- ¿Se mantiene limpio?

---

## Validar documentación API

Revisar:

```text
docs/api-endpoints.md
docs/api-auth-futura.md
```

Responder:

- ¿Coincide con el código real?
- ¿Documenta endpoints inexistentes?
- ¿Faltan endpoints implementados?
- ¿Incluye ejemplos JSON realistas?
- ¿Incluye códigos HTTP correctos?
- ¿Advierte que no hay JWT todavía?
- ¿Separa endpoints públicos de futuros protegidos?

---

## Pruebas manuales exactas del API

Proponer una tabla con:

```text
Prueba:
URL:
Método:
Resultado esperado:
Código HTTP esperado:
Observaciones:
```

Debe incluir mínimo:

```text
GET /api/products
GET /api/products?categoria=Whisky
GET /api/products?buscar=ron
GET /api/products/{id válido}
GET /api/products/999999
GET /api/products/0
GET /api/products/categories
GET /api/products/featured?take=4
GET /api/products/featured?take=0
GET /api/products/featured?take=-1
POST /api/auth/login con credenciales válidas
POST /api/auth/login con credenciales inválidas
```

---

# Parte C — Qué falta para decir “API al 100%”

Codex debe responder explícitamente:

```text
¿El API está al 100% para el alcance actual?: Sí / No
```

Si la respuesta es No, indicar:

```text
Qué falta:
Prioridad:
Riesgo:
Archivos a tocar:
Bloque recomendado:
```

Si la respuesta es Sí, indicar:

```text
Qué pruebas faltan ejecutar manualmente:
Qué documentación debe actualizarse:
Qué debe quedar como mejora futura:
```

---

# Parte D — Posibles mejoras pequeñas permitidas después del diagnóstico

No implementar todavía.

Solo proponer si aplica.

Posibles mejoras pequeñas aceptables:

```text
validar id <= 0 en ProductsController
validar take <= 0
limitar take máximo, por ejemplo 20 o 50
devolver 400 claro para parámetros inválidos
asegurar 404 para producto inexistente
mejorar mensajes genéricos 500
actualizar docs/api-endpoints.md con validaciones reales
agregar docs/api-pruebas-manuales.md
agregar endpoint GET /api/health si ya existe estructura simple y no afecta nada
```

Posibles mejoras NO recomendadas para este cierre:

```text
implementar JWT completo
exponer pedidos cliente
exponer facturación
exponer inventario admin
exponer clientes/créditos
exponer roles/permisos
crear SQL nuevo grande
refactorizar servicios grandes
cambiar login MVC
cambiar appsettings
```

---

# Parte E — Resultado final esperado del diagnóstico

La respuesta debe venir con estas secciones:

```text
1. Resumen ejecutivo.
2. Estado git y rama.
3. Estado del Bloque 6.
4. Hallazgos de mantenibilidad.
5. Estado del API.
6. Hallazgos del API.
7. API al 100%: Sí / No.
8. Qué debe corregirse antes del PR.
9. Qué puede quedar como mejora futura.
10. Plan recomendado por bloques pequeños.
11. Archivos que se tocarían por bloque.
12. Pruebas manuales exactas.
13. Riesgos restantes.
14. Recomendación final.
```

---

# Importante sobre implementación futura

Después de entregar el diagnóstico, no avances.

Esperá aprobación explícita.

Si se aprueba un bloque de implementación, debe ser pequeño y separado, por ejemplo:

```text
Bloque 6A — Correcciones mínimas de mantenibilidad
Bloque API-A — Validaciones de ProductsController
Bloque API-B — Documentar pruebas manuales del API
Bloque QA-Update — Actualizar QA final
```

Cada bloque debe tener:

```text
archivos permitidos
build
commit separado
reporte final
```

---

# Build final esperado

Al finalizar cualquier implementación futura, el build debe quedar:

```text
0 errores
0 warnings
```

Comando:

```bash
dotnet build Trilogia-Cursos\Proyecto_Final.slnx
```

---

# Veredicto esperado

No quiero cerrar el PR hasta tener claro:

```text
Bloque 6 revisado
API revisado completo
Build 0 errores
Build 0 warnings
git status limpio
docs actualizados
scripts SQL cu091-cu095 existentes
scripts SQL cu091-cu095 ejecutados o claramente pendientes
QA final actualizado
```
