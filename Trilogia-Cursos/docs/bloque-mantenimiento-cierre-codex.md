# BLOQUE MANTENIMIENTO — Optimización, limpieza visible y cierre técnico

**Proyecto:** Trilogia-Cursos / DistribuidoraJJ / Licorera La Bodega  
**Rama:** `feature/qa-seguridad-arquitectura`  
**Objetivo:** cerrar mantenimiento sin hacer refactors grandes, corrigiendo únicamente detalles seguros antes del PR.

---

## 1. Contexto

Diagnóstico previo del Bloque 6:

```text
No hay hallazgos críticos que obliguen a refactor grande antes del PR.
Sí hay deuda de mantenimiento.
Lo más recomendable antes del PR es corregir detalles visibles y documentar deuda futura.
```

Hallazgos principales:

```text
B6-01 AdminDbService demasiado grande -> futuro
B6-02 SQL inline residual -> opcional/futuro
B6-03 controladores con lógica moderada -> futuro
B6-04 ViewBag/TempData abundante -> futuro
B6-05 CSS acumulado -> futuro
B6-06 mojibake visible residual -> corregir si es seguro
B6-07 listados sin paginación real -> futuro
B6-08 vistas legacy Security -> documentar/futuro
```

---

## 2. Reglas obligatorias

```text
No hacer refactor grande.
No dividir AdminDbService.
No migrar SQL inline residual en este bloque.
No tocar appsettings.
No tocar cadenas de conexión.
No tocar SQL.
No cambiar estructura de base de datos.
No cambiar lógica de negocio.
No cambiar seguridad.
No cambiar permisos.
No cambiar rutas.
No cambiar formularios.
No cambiar CSS salvo que sea un texto visible en archivo permitido.
No tocar bin, obj, .vs, ZIPs ni archivos generados.
```

Este bloque debe ser seguro, pequeño y defendible.

---

## 3. Archivos permitidos

### Documentación permitida

```text
docs/plan-continuacion-api-qa-codex.md
docs/diagnostico-bloque6-api-cierre-codex.md
docs/qa-final.md
docs/resumen-mejoras-seguridad.md
docs/resumen-final-proyecto.md
```

### Corrección de mojibake visible

Solo tocar archivos donde aparezcan textos visibles dañados para el usuario, previa búsqueda controlada.

Archivos candidatos según diagnóstico:

```text
Proyecto_Final/Controllers/CartController.cs
Proyecto_Final/wwwroot/js/checkout.js
Proyecto_Final/Services/AdminDbService.cs
docs/*
```

También podés reportar otros archivos si encontrás mojibake visible, pero no los modifiques sin justificar en el reporte.

---

## 4. Tarea A — Resolver documentos pendientes sin trackear

Verificar:

```bash
git status --short --branch
```

Si están no trackeados:

```text
docs/plan-continuacion-api-qa-codex.md
docs/diagnostico-bloque6-api-cierre-codex.md
```

Incluirlos en este bloque.

No modificar su contenido salvo que haya errores evidentes de ruta o título.

---

## 5. Tarea B — Corrección de mojibake visible residual

Buscar textos dañados típicos:

```text
á
é
í
ó
ú
ñ
Ã
¿
¡
opción
está
edición
contraseña
categoría
facturación
permisos
```

Comando sugerido:

```bash
git grep -n "Ã\|Â" -- Proyecto_Final docs database
```

### Qué sí corregir

Corregir únicamente texto visible para usuario o documentación, por ejemplo:

```text
está -> está
opción -> opción
edición -> edición
contraseña -> contraseña
categoría -> categoría
facturación -> facturación
```

Aplica para:

```text
mensajes TempData visibles
mensajes ModelState visibles
textos en JavaScript visibles
textos de documentación
comentarios si son visibles o importantes
```

### Qué NO corregir

No tocar:

```text
nombres de variables
nombres de métodos
claves de permisos
códigos SQL
nombres de procedimientos
rutas
keys de JSON
identificadores internos
clases CSS
IDs HTML
datos que puedan depender de BD
```

Si hay duda, no corregir y reportar.

---

## 6. Tarea C — Documentar deuda de mantenimiento futura

Actualizar:

```text
docs/resumen-final-proyecto.md
```

Agregar sección:

```text
## Mejoras futuras de mantenibilidad
```

Debe incluir:

```text
- Separar AdminDbService en servicios especializados.
- Migrar SQL inline residual de AccountDbService y algunos métodos administrativos a procedimientos almacenados.
- Migrar gradualmente ViewBag/TempData a ViewModels.
- Agregar paginación real en listados grandes.
- Consolidar CSS acumulado por sprints.
- Mantener vistas legacy de Security solo como compatibilidad o retirarlas en una fase futura.
- Implementar JWT antes de exponer endpoints protegidos del API.
```

Actualizar también:

```text
docs/qa-final.md
```

Agregar validación visual:

```text
- Revisar que no haya textos con caracteres dañados tipo Ã, Â, está, opción o contraseña en pantallas principales.
```

---

## 7. Tarea D — Confirmar que no se hará refactor grande

En el reporte final, indicar explícitamente:

```text
AdminDbService no se dividió por riesgo de regresión antes del PR.
SQL inline residual queda documentado como mejora futura.
ViewBag/TempData quedan documentados como mejora futura.
Paginación queda documentada como mejora futura.
CSS acumulado queda documentado como mejora futura.
```

---

## 8. Build obligatorio

Ejecutar:

```bash
dotnet build Trilogia-Cursos\Proyecto_Final.slnx
```

Resultado requerido:

```text
0 errores
0 warnings
```

---

## 9. Estado git obligatorio

Ejecutar:

```bash
git status --short --branch
```

Reportar:

```text
rama actual
archivos modificados
archivos no trackeados
si el árbol quedó limpio después del commit
```

---

## 10. Reporte final esperado

Reportá:

1. Archivos modificados.
2. Documentos no trackeados incorporados.
3. Textos mojibake corregidos.
4. Archivos donde se corrigió mojibake.
5. Deuda futura documentada.
6. Resultado del build.
7. Warnings.
8. Estado git.
9. Confirmación de que no tocaste:
   - SQL,
   - appsettings,
   - permisos,
   - rutas,
   - lógica de negocio,
   - bin/obj/.vs/ZIPs.
10. Recomendación final para PR o pendientes restantes.

---

## 11. Commit sugerido

**Summary**

```text
chore: cerrar mantenimiento y documentar deuda futura
```

**Description**

```text
Incorpora documentos pendientes de cierre, corrige textos visibles con caracteres dañados cuando aplica y documenta mejoras futuras de mantenibilidad sin cambiar lógica funcional, SQL, configuración ni seguridad.
```
