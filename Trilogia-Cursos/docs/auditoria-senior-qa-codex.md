# Auditoría Senior QA / Seguridad / Arquitectura — Prompt para Codex

## Instrucción corta para usar con Codex

Leé primero:

- `AGENTS.md`
- `docs/auditoria-senior-qa-codex.md`

Actuá como **Senior QA Engineer, Security Reviewer y Arquitecto .NET**.

Trabajá primero en **SOLO LECTURA**.

No edites archivos.  
No apliques cambios.  
No hagas refactor.  
No hagas commit.  
No hagas push.  
No toques `main`.  
No toques `appsettings`.  
No toques SQL.  
No toques `Proyecto_FinalAPI`.  
No toques controladores, servicios, modelos ni vistas.  
No instales paquetes NuGet.  
No borres archivos.  
No muevas archivos.  
No generes scripts todavía.  
No modifiques `bin/`, `obj/`, `.vs/`, ZIPs ni archivos generados.

Entregá únicamente diagnóstico y plan.

---

## Contexto del proyecto

Estoy trabajando en el proyecto universitario .NET / SQL Server llamado:

**Trilogia-Cursos / DistribuidoraJJ / Licorera La Bodega**

Estructura general:

- Proyecto MVC principal: `Proyecto_Final`
- Proyecto API: `Proyecto_FinalAPI`
- Base de datos: `DistribuidoraJJ_DB`
- SQL Server local
- SQL debe ir separado en `database/`
- No se debe trabajar directo en `main`
- `main` debe mantenerse estable

Observación importante del profesor:

> “No se permiten las sentencias de base de datos directamente en el código de .Net.”

Por eso necesito revisar si hay SQL directo dentro del código C#, si hay consultas inline, si hay riesgos de SQL Injection y si conviene migrar gradualmente a procedimientos almacenados o una capa de datos más ordenada.

---

## Objetivo general

Analizar TODO el proyecto como si fueras:

- Senior QA Engineer
- Security Reviewer
- Arquitecto .NET
- Revisor de base de datos
- Revisor de integración MVC/API

Detectar:

1. Vulnerabilidades de seguridad.
2. Problemas de arquitectura.
3. Problemas de flujo funcional.
4. Problemas de integración MVC/API.
5. Problemas de base de datos.
6. Consultas SQL directas dentro del código .NET.
7. Funcionalidades incompletas.
8. Riesgos de permisos, sesiones y roles.
9. Problemas en el flujo de compra, pedidos, facturación y cancelación.
10. Recomendaciones implementables por bloques seguros.

---

# A. Seguridad general

Revisá:

- Login.
- Registro.
- Recuperación de contraseña.
- Reset password.
- Rate-limit.
- Sesiones.
- Roles.
- `AdminAuthorizeAttribute`.
- `SessionAuthorizeAttribute`.
- Acceso cliente a pedidos propios.
- Acceso cliente a comprobantes propios.
- Cancelación de pedidos.
- Formularios POST.
- `AntiForgeryToken`.
- Mensajes de error visibles.
- Validación de imágenes.
- Validaciones de entrada.
- Posibles fugas de información.
- Posibles rutas accesibles sin autorización.
- Posibles acciones administrativas visibles o ejecutables por cliente/empleado.

Para cada hallazgo, reportá:

- Vulnerabilidad encontrada.
- Archivo exacto.
- Riesgo.
- Severidad: Alta / Media / Baja.
- Cómo explotaría un usuario.
- Recomendación concreta.
- Si requiere SQL, indicar script separado en `database/`.
- Si no requiere SQL, indicar archivos exactos a tocar.

---

# B. Consultas SQL directas en código .NET

Buscá en todo el proyecto:

- `SqlCommand` con texto SQL inline.
- `SELECT`, `INSERT`, `UPDATE`, `DELETE` escritos directamente en servicios o controladores.
- Stored procedures usados correctamente.
- Concatenación de strings SQL.
- Riesgo de SQL Injection.
- Queries duplicadas.
- Queries muy grandes dentro de C#.

Clasificá:

1. Consultas directas peligrosas.
2. Consultas directas parametrizadas pero no ideales para la regla del profesor.
3. Consultas ya protegidas con stored procedures.
4. Recomendación para migrarlas gradualmente a stored procedures en `database/`.

No implementes todavía.  
Solo reportá inventario y plan de migración por bloques.

---

# C. Arquitectura MVC/API

Revisá la estructura de:

- `Proyecto_Final`
- `Proyecto_FinalAPI`

Preocupación principal:

El MVC tiene muchos controladores y funcionalidad, pero el API está muy vacío o con poca funcionalidad. Necesito saber si conviene “meterle más carne” al API.

Analizá:

- Qué funcionalidades están solo en MVC.
- Qué funcionalidades deberían tener endpoint API.
- Qué endpoints existen en `Proyecto_FinalAPI`.
- Qué endpoints faltan.
- Si el API realmente aporta algo o está incompleto.
- Qué módulos serían buenos candidatos para API:
  - productos,
  - inventario,
  - pedidos,
  - facturación,
  - clientes,
  - empleados,
  - autenticación,
  - reportes,
  - portal cliente,
  - vendedor/empleado.

Proponé:

- API actual: diagnóstico.
- Endpoints faltantes recomendados.
- Prioridad Alta / Media / Baja.
- Riesgos.
- Qué no conviene mover al API todavía.
- Plan por bloques para fortalecer `Proyecto_FinalAPI` sin romper MVC.

---

# D. Flujo compra → pedido → admin → factura → cliente

Analizá especialmente este caso:

Como cliente:

1. Inicio sesión.
2. Agrego productos al carrito.
3. Finalizo compra.
4. Se genera un pedido o factura por ₡100.000, por ejemplo.

Como administrador:

1. Esa compra debería aparecer en el módulo de pedidos o facturación.
2. El admin debería poder ver el detalle de esa compra.
3. El admin debería aprobar/procesar/finalizar el pedido.
4. El admin debería generar o cerrar la factura/comprobante.
5. El cliente debería poder ver el estado actualizado y el comprobante cuando aplique.

Problema observado:

Hice una compra como cliente, pero no vi claramente esa compra reflejada del lado de administrador, o no encontré la parte para finalizar/aprobar/facturar esa compra.

Revisá:

- Qué pasa realmente cuando se finaliza una compra.
- Qué tablas se afectan.
- Qué procedimientos se llaman.
- Si se crea `Pedido`.
- Si se crea `Factura` automáticamente o no.
- Si el pedido queda en estado `Pendiente`.
- Si el admin tiene pantalla para ver ese pedido.
- Si el admin tiene acción para aprobar/procesar/finalizar.
- Si existe acción para generar factura desde pedido.
- Si el cliente puede ver comprobante solo cuando existe factura.
- Si hay inconsistencia entre `Pedido` y `Factura`.
- Si el flujo está incompleto.

Necesito un diagnóstico claro:

- Flujo actual real.
- Qué está completo.
- Qué está incompleto.
- Qué parte no está conectada.
- Qué archivos participan.
- Qué tablas/procedimientos participan.
- Qué deberíamos implementar para que el flujo quede profesional.

---

# E. Módulos funcionales incompletos

Revisá si faltan piezas en:

- Tienda.
- Carrito.
- Checkout.
- Confirmación.
- Portal cliente.
- Cancelación de pedidos.
- Comprobante cliente.
- Pedidos admin.
- Facturación admin.
- Inventario.
- Clientes.
- Créditos.
- Consultas.
- Empleados.
- Portal empleado.
- Roles.
- Permisos.
- Auditoría.
- API.
- Reportes.

Para cada módulo, reportá:

- Estado actual.
- Qué falta.
- Qué riesgo hay.
- Qué se recomienda implementar.
- Prioridad.

---

# F. Optimización y mantenibilidad

Analizá:

- Código repetido.
- Servicios demasiado grandes.
- Controladores con demasiada lógica.
- Consultas duplicadas.
- Vistas demasiado cargadas.
- CSS acumulado.
- JS faltante o frágil.
- Uso de `ViewBag` / `TempData` si está desordenado.
- Posibles problemas de rendimiento.
- Cargas de listas sin paginación.
- Tablas admin muy grandes.
- Falta de logs o auditoría en acciones importantes.

---

# G. QA final

Prepará una matriz de pruebas final con:

- Pruebas cliente.
- Pruebas admin.
- Pruebas empleado/vendedor.
- Pruebas de seguridad.
- Pruebas de API.
- Pruebas de base de datos.
- Pruebas responsive.
- Pruebas de impresión.
- Pruebas negativas.

---

# Formato de respuesta requerido

Respondé con esta estructura:

1. Resumen ejecutivo.
2. Hallazgos críticos.
3. Hallazgos medios.
4. Hallazgos bajos.
5. Diagnóstico del API.
6. Diagnóstico de SQL directo en C#.
7. Diagnóstico del flujo compra → pedido → admin → factura → cliente.
8. Funcionalidades faltantes.
9. Recomendaciones por prioridad.
10. Plan de implementación por bloques.
11. Archivos que probablemente habría que tocar por bloque.
12. Scripts SQL que probablemente habría que crear por bloque.
13. Riesgos.
14. Pruebas necesarias.
15. Qué NO se debe tocar.
16. Recomendación final: qué implementar primero.

---

# Reglas para el plan de implementación

Dividir en bloques pequeños.

Prioridad sugerida:

1. Seguridad y autorización.
2. Flujo compra → pedido admin → facturación.
3. SQL directo en C# / procedimientos almacenados.
4. Fortalecer API.
5. Optimización y mantenibilidad.
6. QA final.
7. Documentación.

Cada bloque debe tener:

- Objetivo.
- Archivos permitidos.
- Archivos prohibidos.
- Si requiere SQL.
- Pruebas.
- Summary sugerido para GitHub Desktop.
- Description sugerida para GitHub Desktop.

---

# Reglas de parada

Si encontrás cualquiera de estos casos, detenete y reportá antes de implementar:

- Build roto.
- Login roto.
- Checkout roto.
- Permisos rotos.
- Impresión rota.
- Archivo sensible modificado.
- Duda sobre lógica.
- SQL riesgoso.
- Cambio que requiere base de datos.
- Cambio que afecta `main`.

---

# Instrucción final

No implementes nada todavía.  
No edites archivos.  
No generes scripts todavía.  
No hagas commits.  
No hagas push.  
No hagas merge.

Solo entregá diagnóstico y plan.
