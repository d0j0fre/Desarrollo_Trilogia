# BLOQUE API — Cierre al 100% del API público

**Proyecto:** Trilogia-Cursos / DistribuidoraJJ / Licorera La Bodega  
**Rama:** `feature/qa-seguridad-arquitectura`  
**Objetivo:** dejar el API público completamente consistente, defendible, documentado y probado dentro del alcance actual.

> Alcance actual del API: autenticación básica existente + productos/categorías públicos.  
> No incluye JWT implementado ni endpoints protegidos de pedidos, facturación, inventario, clientes, créditos, roles o permisos.

---

## 1. Contexto obligatorio

Ya existen estos endpoints:

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

Diagnóstico previo:
- `ProductsApiDbService` usa stored procedures.
- No depende de MVC.
- No expone costo, margen, proveedor ni datos internos.
- Sí expone `Stock`, consistente con la tienda MVC.
- El API todavía no tiene JWT. Eso es correcto para este cierre.
- No se deben exponer endpoints protegidos sin token.

---

## 2. Reglas obligatorias

```text
No implementar JWT.
No crear API Key.
No crear cookies compartidas.
No exponer pedidos.
No exponer facturación.
No exponer inventario admin.
No exponer clientes/créditos.
No exponer roles/permisos.
No tocar appsettings.
No tocar SQL.
No tocar MVC salvo documentación QA permitida.
No cambiar login MVC.
No cambiar shape actual del login API.
No tocar bin, obj, .vs, ZIPs ni archivos generados.
```

El build final debe quedar:

```text
0 errores
0 warnings
```

Comando:

```bash
dotnet build Trilogia-Cursos\Proyecto_Final.slnx
```

---

## 3. Archivos permitidos

Para este bloque solo podés tocar:

```text
Proyecto_FinalAPI/Controllers/ProductsController.cs
docs/api-endpoints.md
docs/api-pruebas-manuales.md
docs/qa-final.md
docs/resumen-final-proyecto.md
```

No tocar `ProductsApiDbService.cs` salvo que el build obligue y se justifique antes de hacerlo.  
No tocar `Program.cs` salvo que el build obligue y se justifique antes de hacerlo.  
No tocar `AuthController.cs` en este bloque.

---

## 4. Cambios requeridos en ProductsController

### 4.1 Validar `id <= 0`

Endpoint:

```text
GET /api/products/{id}
```

Requerimiento:

```text
Si id <= 0, devolver 400 Bad Request.
No llamar al servicio.
No llegar a base de datos.
No devolver 404 para id inválido.
```

Respuesta sugerida:

```json
{
  "message": "El identificador del producto debe ser mayor a cero."
}
```

Comportamiento esperado:

```text
/api/products/0  -> 400
/api/products/-1 -> 400
/api/products/999999 -> 404 si no existe
```

---

### 4.2 Validar `take <= 0`

Endpoint:

```text
GET /api/products/featured?take=
```

Requerimiento:

```text
Si take <= 0, devolver 400 Bad Request.
No corregir silenciosamente a 1.
No llamar al servicio.
```

Respuesta sugerida:

```json
{
  "message": "La cantidad solicitada debe ser mayor a cero."
}
```

---

### 4.3 Limitar `take` máximo

Endpoint:

```text
GET /api/products/featured?take=
```

Requerimiento:

```text
Si take > 24, limitar a 24.
Este comportamiento debe documentarse.
```

Ejemplos:

```text
/api/products/featured?take=4  -> hasta 4 productos
/api/products/featured?take=100 -> máximo 24 productos
/api/products/featured?take=0 -> 400
/api/products/featured?take=-1 -> 400
```

---

### 4.4 Mantener errores 500 genéricos

Si ocurre una excepción interna:

```text
No exponer ex.Message.
No exponer stack trace.
No exponer detalles SQL.
```

Respuesta genérica sugerida:

```json
{
  "message": "Ocurrió un error al consultar los productos."
}
```

o una variante equivalente ya existente.

---

## 5. Cambios requeridos en documentación API

Actualizar:

```text
docs/api-endpoints.md
```

Debe reflejar exactamente:

### Para `GET /api/products/{id}`

Documentar:

```text
200: producto encontrado
400: id menor o igual a cero
404: producto no encontrado
500: error interno genérico
```

Incluir ejemplo de `400`:

```json
{
  "message": "El identificador del producto debe ser mayor a cero."
}
```

### Para `GET /api/products/featured?take=`

Documentar:

```text
200: productos destacados
400: take menor o igual a cero
500: error interno genérico
```

Documentar también:

```text
Si take es mayor a 24, se limita a 24.
```

Incluir ejemplo de `400`:

```json
{
  "message": "La cantidad solicitada debe ser mayor a cero."
}
```

---

## 6. Crear documentación de pruebas manuales del API

Crear:

```text
docs/api-pruebas-manuales.md
```

Debe incluir una tabla con:

```text
ID
Módulo
Método
URL
Datos de prueba
Resultado esperado
Código HTTP esperado
Estado
Observaciones
```

### Pruebas mínimas obligatorias

```text
API-001 GET /api/products -> 200
API-002 GET /api/products?categoria=Whisky -> 200
API-003 GET /api/products?buscar=ron -> 200
API-004 GET /api/products/{id válido} -> 200
API-005 GET /api/products/999999 -> 404
API-006 GET /api/products/0 -> 400
API-007 GET /api/products/-1 -> 400
API-008 GET /api/products/categories -> 200
API-009 GET /api/products/featured?take=4 -> 200
API-010 GET /api/products/featured?take=0 -> 400
API-011 GET /api/products/featured?take=-1 -> 400
API-012 GET /api/products/featured?take=100 -> 200, máximo 24 resultados
API-013 POST /api/auth/login con credenciales válidas -> 200
API-014 POST /api/auth/login con credenciales inválidas -> 401
API-015 POST /api/auth/register con datos válidos -> código esperado según implementación actual
API-016 POST /api/auth/forgot-password -> código esperado según implementación actual
API-017 POST /api/auth/reset-password -> código esperado según implementación actual
```

Agregar una nota:

```text
El API no emite JWT actualmente. Por eso no existen endpoints protegidos de cliente/admin en esta fase.
```

---

## 7. Actualizar QA final

Actualizar:

```text
docs/qa-final.md
```

Agregar o ajustar la sección API para incluir pruebas negativas:

```text
GET /api/products/0 debe devolver 400.
GET /api/products/-1 debe devolver 400.
GET /api/products/999999 debe devolver 404.
GET /api/products/featured?take=0 debe devolver 400.
GET /api/products/featured?take=-1 debe devolver 400.
GET /api/products/featured?take=100 debe devolver 200 con máximo 24 resultados.
```

---

## 8. Actualizar resumen final del proyecto

Actualizar:

```text
docs/resumen-final-proyecto.md
```

Debe indicar:

```text
El API público queda cerrado para el alcance actual:
- autenticación básica existente,
- productos públicos,
- categorías públicas,
- destacados públicos,
- validaciones de parámetros,
- documentación de endpoints,
- pruebas manuales API documentadas.

JWT y endpoints protegidos quedan documentados como mejora futura.
```

---

## 9. Pruebas técnicas obligatorias

Ejecutar:

```bash
dotnet build Trilogia-Cursos\Proyecto_Final.slnx
```

Resultado requerido:

```text
0 errores
0 warnings
```

Revisar:

```bash
git status --short --branch
```

---

## 10. Reporte final esperado

Reportá:

1. Archivos modificados.
2. Cambios aplicados en `ProductsController`.
3. Documentación actualizada.
4. Archivo de pruebas manuales creado.
5. Resultado del build.
6. Warnings.
7. Estado de git.
8. Confirmación de que no tocaste:
   - SQL,
   - appsettings,
   - MVC funcional,
   - AuthController,
   - ProductsApiDbService,
   - Program.cs,
   - bin/obj/.vs/ZIPs.
9. Pruebas manuales pendientes de ejecutar.
10. Si el API ya puede considerarse al 100% dentro del alcance actual.

---

## 11. Commit sugerido

**Summary**

```text
fix(api): cerrar validaciones y pruebas del api publico
```

**Description**

```text
Agrega validaciones explícitas para parámetros inválidos en endpoints públicos de productos, actualiza la documentación de endpoints y agrega pruebas manuales del API.

Mantiene el alcance público del API sin implementar JWT ni exponer endpoints protegidos.
```
