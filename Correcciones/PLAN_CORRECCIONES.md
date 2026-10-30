# Plan de correcciones — Distribuidora JJ / Licorera La Bodega

Fuente oficial: `Entregas/G8_SC803_J_BugReport.pdf` (91 bugs en 25 módulos), texto completo en
[`fuentes/BugReport_G8_SC803_J.txt`](fuentes/BugReport_G8_SC803_J.txt). Estado por bug en [`BACKLOG.md`](BACKLOG.md).

| Prioridad | Bugs |
|---|---:|
| Crítico (P1) | 7 |
| Alto (P2) | 19 |
| Medio (P3) | 65 |
| **Total** | **91** |

Meta: todas las correcciones integradas en `main` en **2 semanas**. Después se hace la ronda completa de re-pruebas.

### Etapas del proyecto

| Etapa | Qué | Detalle |
|---|---|---|
| **0. Preparación** | Acordar el plan con el equipo y aprobar las decisiones D1–D5 de [`MATRIZ_INTEGRACION.md`](MATRIZ_INTEGRACION.md) | Las sesiones de corrección arrancan cuando el equipo aprueba |
| **1. Correcciones** | Los 91 bugs sobre `main`, **todos los lotes (E1–E9, D, N, G)** | ~2 semanas |
| **2. Re-prueba de `main`** | Casos QA de los bugs corregidos más un recorrido de regresión por módulo | `main` queda **listo**: es la entrega asegurada |
| **3. Integración según tiempo** | Mejoras de `restructuracion-2.0` por capas. Lo mínimo es la capa visual; el resto se decide por el tiempo que quede antes de la presentación | [`REESTRUCTURACION.md`](REESTRUCTURACION.md) |
| **4. Regresión final** | Casos QA de los módulos tocados por la integración | Si una capa no pasa, no se integra y `main` sigue siendo la entrega |

**Regla que une las etapas 1 y 3:** cada sesión, **antes de corregir un bug, revisa si `restructuracion-2.0` ya lo resolvió**. Si es así, adapta esa solución a `main` en vez de inventar otra: es más rápido y deja el código más parecido, lo que reduce conflictos al integrar. Se corrige en la capa de lógica y en las vistas solo lo mínimo. Cada PR lista las vistas que hay que **re-aplicar en el nuevo diseño**. El detalle está en `REESTRUCTURACION.md`.

---

## 1. Entorno de trabajo (obligatorio para todos)

| Qué | Dónde |
|---|---|
| Repositorio de trabajo | `C:\dev\Desarrollo_Trilogia` (clon limpio, **fuera de OneDrive**) |
| Repositorio remoto | https://github.com/d0j0fre/Desarrollo_Trilogia — rama base `main` |
| Documentación QA y entregas | `OneDrive\...\Implantacion\Proyecto3\Desarrollo_Trilogia\Entregas` y `Trilogia-Cursos\QA-Ejecucion*` (no se mueven al repo) |
| Evidencias de cada bug | `...\Trilogia-Cursos\QA-Ejecucion*\evidencias\` y JSON en `...\QA-Ejecucion*\bugs*.json` |
| Credenciales y scripts de BD | `OneDrive\...\Proyecto3\_local_setup` (fuera de git a propósito) |

> La copia antigua en OneDrive tenía el `.git` corrupto. **No usar esa carpeta para programar ni hacer commits.**

Compañeros (David, Danny, Gerald): clonar con `git clone https://github.com/d0j0fre/Desarrollo_Trilogia C:\dev\Desarrollo_Trilogia`, nunca dentro de OneDrive.

### Base de datos

- Desarrollo y pruebas locales: **LocalDB** (`_local_setup/Build-LocalDb.ps1`).
- **Azure SQL compartido**: plan gratuito con ~55 h de cómputo al mes. No dejar apps apuntando a Azure mientras se programa. Solo se usa para la validación final.
- Cambios de esquema/SP: script nuevo en `Trilogia-Cursos/database/migrations/` siguiendo su `README.md` (idempotente, con `.verify.sql` y rollback). **Ninguna sesión aplica scripts en Azure**; Esteban los aplica después del merge.
- Para que los lotes paralelos no choquen, cada lote usa su bloque de numeración:

  La reestructuración ya ocupa `0023–0061`, así que las correcciones usan la serie **0200**:

| Lote | Bloque | Lote | Bloque |
|---|---|---|---|
| E1 | 0200–0209 | E7 | 0260–0269 |
| E2 | 0210–0219 | E8 | 0270–0279 |
| E3 | 0220–0229 | E9 | 0280–0289 |
| E4 | 0230–0239 | D (David) | 0290–0299 |
| E5 | 0240–0249 | N (Danny) | 0300–0309 |
| E6 | 0250–0259 | G (Gerald) | 0310–0319 |

## 2. Flujo Git

1. Una rama por lote desde `main` actualizado: `fix/<lote>-<tema>` (ej. `fix/e1-errores-criticos`, `fix/david-reportes`).
2. Commits pequeños, uno por bug o grupo de bugs con la misma causa: `fix(BUG-05-01): guardar producto responde 405`.
3. Antes del PR: `dotnet build Trilogia-Cursos/Proyecto_Final.slnx` sin errores y `dotnet test` en verde. Móvil: `flutter analyze` sin errores.
4. PR hacia `main` con la lista de bugs cerrados, cómo se verificó, los scripts SQL nuevos y la sección **"Re-aplicar en el nuevo diseño"** (vistas tocadas y qué conservar). Actualizar las filas del lote en `BACKLOG.md` en el mismo PR.
5. Merge de uno en uno (Esteban revisa). Si `main` avanzó, actualizar la rama antes de mergear.
6. No mergear `restructuracion-2.0` (ni sus versiones anteriores: PR #127, `codex/stage2-design-system`) durante la Etapa 1. Si un bug ya está resuelto ahí, se adapta solo ese fragmento y se anota en el backlog: `adaptado de restructuracion-2.0 <commit>`. Consulta rápida: `git log origin/restructuracion-2.0 -- <archivo>` y `git show origin/restructuracion-2.0:<ruta>`. La integración completa es la Etapa 3.

Archivos de alto riesgo de conflicto (tocar solo lo necesario, sin reformatear): `Services/AdminDbService.cs`, `Program.cs`, `Views/Shared/_Layout*.cshtml`, menús por rol, `database/00_todo_en_uno.sql`.

## 3. Definición de terminado (por bug)

- Revisado contra `restructuracion-2.0`: si ahí estaba resuelto, se adaptó esa solución.
- Reproducido antes del cambio, o causa explicada si no se reproduce en LocalDB.
- Cumple el **resultado esperado** del bug report (que cita la HU/caso CU-xxx-yy).
- Build y tests en verde; prueba unitaria nueva cuando la lógica lo permite.
- Fila del bug en `BACKLOG.md` con estado `Corregido`, rama/PR y nota corta.

## 4. Reparto

Esteban trabaja los lotes técnicos con sesiones de Claude Code (E1–E9, 62 bugs). Los compañeros toman lotes acotados, de patrón repetible y sin dependencia fuerte de IA (29 bugs).
BUG-09-05 aparece dos veces en el PDF (P1 y P2); se trata como un solo bug P1.

| Lote | Responsable | Tema | Bugs | Fase |
|---|---|---|---:|:---:|
| E1 | Esteban (IA) | Errores críticos de servidor (405/503/"No podemos completar") | 7 | 1 |
| E2 | Esteban (IA) | Seguridad, roles, permisos y sesiones | 7 | 1 |
| E5 | Esteban (IA) | Correos, mensajería y documentos PDF | 6 | 1 |
| E3 | Esteban (IA) | App móvil, sincronización y notificaciones | 7 | 2 |
| E4 | Esteban (IA) | Rutas, entregas y liquidaciones | 10 | 2 |
| E6 | Esteban (IA) | Crédito, clientes, portal y checkout | 6 | 2 |
| E7 | Esteban (IA) | Planilla y RRHH | 4 | 3 |
| E8 | Esteban (IA) | Analítica: sugerencias, tendencias y vencimientos | 10 | 3 |
| E9 | Esteban (IA) | Asistente virtual y venta cruzada | 6 | 3 |
| D | David | Reportes, devoluciones, metas/KPIs y exportaciones | 10 | 1–3 |
| N | Danny | Activos, comodatos, flota, promociones y combos | 10 | 1–3 |
| G | Gerald | Cuenta, perfil, chat y búsqueda de inventario | 9 | 1–3 |

Fases de la Etapa 1 (se cuentan desde que el equipo aprueba el plan): **1** = días 1–4 · **2** = días 4–8 · **3** = días 8–11 · **4** = días 11–14 (cierre, smoke test en Azure y documentación con la sesión "Documentacion").

Máximo 3 sesiones de IA en paralelo para cuidar el uso semanal y evitar conflictos de merge. Al cerrar un lote se lanza el siguiente.

### Dependencias entre lotes

- **E1 primero**: desbloquea detalle de pedidos, presupuestos, ingresos/egresos y planilla, que otros lotes necesitan para verificar.
- David: BUG-22-03 y BUG-22-04 (presupuestos) después del merge de E1 (BUG-22-01) y E2 (BUG-22-02).
- Danny: BUG-16-02 usa el saldo/deuda del cliente; si E6 cambia esa consulta, usar la de `main` más reciente.
- E3 y E4 comparten estados de pedido/ruta: E4 se mergea después de E3 o se coordina el cambio de estados en un solo SP.
- E5 (contrato de comodato PDF, BUG-16-01) y Danny (comodatos) tocan el mismo módulo: E5 solo agrega la generación del documento.

## 5. Detalle de lotes

Los IDs completos con título y prioridad están en `BACKLOG.md`. Para cada bug, leer su bloque en `fuentes/BugReport_G8_SC803_J.txt` (pasos, resultado actual y esperado).

### E1 — Errores críticos de servidor (Fase 1)
BUG-05-01, BUG-09-03, BUG-09-04, BUG-09-05, BUG-11-02, BUG-14-03, BUG-22-01.
Todos son pantallas que caen con 405/503 o "No podemos completar esta solicitud". Buscar primero una causa común (contrato de SP distinto entre código y BD, columnas faltantes en Azure frente a scripts, antiforgery/rutas). Comparar con lo que corrige el PR #127 antes de reinventar.

### E2 — Seguridad, roles, permisos y sesiones (Fase 1)
BUG-04-01, BUG-04-02, BUG-04-03, BUG-09-02, BUG-11-06, BUG-16-07, BUG-22-02.
Rol con usuarios no se inactiva; Administrador con todos los permisos; usuarios de rol inactivo o empleado inactivo pierden acceso de inmediato (validar en cada request, no solo al iniciar sesión); Gerente y Auditor con los accesos que su HU indica.

### E3 — App móvil, sincronización y notificaciones (Fase 2)
BUG-07-01, BUG-07-02, BUG-08-01, BUG-10-06, BUG-10-08, BUG-23-05, BUG-25-04.
Pedido sin conexión y sincronización; estados En ruta / No entregado desde la app; notificaciones (bandeja web + push) para asignación y reasignación de ruta y mensajes directos; mapa con la secuencia de visitas en la app del chofer.

### E4 — Rutas, entregas y liquidaciones (Fase 2)
BUG-06-04, BUG-10-01, BUG-10-02, BUG-10-03, BUG-10-04, BUG-10-05, BUG-10-07, BUG-25-01, BUG-25-02, BUG-25-03.

### E5 — Correos, mensajería y documentos PDF (Fase 1)
BUG-01-01, BUG-03-01, BUG-09-01, BUG-11-05, BUG-12-02, BUG-16-01.
Revisar primero la configuración SMTP y el servicio de correo (probablemente una sola causa para 01-01 y 03-01). Generación de PDF reutilizable para boleta, estado de cuenta/factura y contrato de comodato.

### E6 — Crédito, clientes, portal y checkout (Fase 2)
BUG-05-04, BUG-06-01, BUG-06-02, BUG-06-03, BUG-12-01, BUG-12-03.

### E7 — Planilla y RRHH (Fase 3)
BUG-11-01, BUG-11-03, BUG-11-04, BUG-11-07.

### E8 — Analítica, sugerencias, tendencias y vencimientos (Fase 3)
BUG-05-02, BUG-20-01, BUG-24-01, BUG-24-02, BUG-24-03, BUG-24-04, BUG-24-05, BUG-24-06, BUG-24-07.
(Incluye exportación a Excel de Tendencia estacional; usar el mismo helper de exportación que defina David.)

### E9 — Asistente virtual y venta cruzada (Fase 3)
BUG-26-01, BUG-26-02, BUG-26-03, BUG-26-04, BUG-26-05, BUG-26-06.

### D — David: reportes, devoluciones, metas y exportaciones
BUG-13-01, BUG-13-02, BUG-13-03, BUG-14-01, BUG-14-02, BUG-21-01, BUG-21-02, BUG-21-03, BUG-22-03, BUG-22-04.
Orden sugerido: 13-03, 21-01, 14-01 (validaciones y campos) → 13-01, 14-02, 21-02, 22-04 (exportaciones .xlsx con un helper común, p. ej. ClosedXML) → 21-03, 13-02 → 22-03 (después de E1/E2).

### N — Danny: activos, comodatos, flota, promociones y combos
BUG-15-01, BUG-15-02, BUG-16-02, BUG-16-03, BUG-16-04, BUG-16-05, BUG-16-06, BUG-17-01, BUG-17-02, BUG-18-01.
Orden sugerido: 18-01, 17-01, 15-02, 16-05, 16-06 (validaciones y columnas) → 15-01, 16-03, 17-02 → 16-02, 16-04.

### G — Gerald: cuenta, perfil, chat y búsqueda de inventario
BUG-01-02, BUG-01-03, BUG-02-01, BUG-02-02, BUG-05-03, BUG-23-01, BUG-23-02, BUG-23-03, BUG-23-04.
Orden sugerido: 02-02, 23-02, 23-03, 05-03 → 01-02, 01-03, 02-01 → 23-04, 23-01.
Nota BUG-01-02: el caso CU-012-02 pide indicar que el correo no está registrado. Implementarlo como lo pide la HU (decisión del equipo).

## 6. Organización de archivos (Fase 4, sesión "Documentacion")

Estructura objetivo en OneDrive (solo documentos; el código vive en `C:\dev`):

```
Implantacion/
├── 01_Plantillas/
├── 02_Entregas_Oficiales/        ← PlanQA, CasosQA, BugReport, InformeQA (QA03) + versiones ENTREGABLE1
├── 03_Ejecucion_QA/              ← QA-Ejecucion, -03-13, -14-18, -Companeros (bitácoras, JSON, evidencias)
├── 04_Pruebas_de_compañeros/     ← DeCompañeros + Entregas/Pruebas compañeros
├── 05_Pruebas_a_Grupo1/          ← Pruebas-Grupo1
├── 06_Correcciones/              ← re-pruebas y evidencias de cada bug corregido
└── _local_setup/                 ← credenciales (nunca a git)
```

`cuentas_prueba.md` y cualquier archivo con contraseñas no se suben a GitHub.
