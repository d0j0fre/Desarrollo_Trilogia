# Integración de la reestructuración

Un compañero reestructuró el sistema: nuevo diseño visual, módulos migrados a otra interfaz y cambios funcionales. Esa versión **no tiene pruebas QA documentadas**, así que no se adopta tal cual. Estrategia acordada:

1. **Primero** se corrige el sistema base (`main`), que sí está probado (502 casos, 91 bugs).
2. **Después** se integran de forma selectiva las mejoras de la reestructuración sobre la base corregida, módulo por módulo, re-ejecutando los casos QA de cada módulo como prueba de regresión.

Así se aprovecha lo mejor de las dos versiones sin heredar errores que nadie ha documentado, y no se gasta uso de IA depurando una versión sin mapa de fallos.

## Versión oficial a integrar

**`origin/restructuracion-2.0`** (commit `9aa7045`, 29/10/2026), con el informe del autor en `fuentes/INFORME-STAGE2-INTEGRACION.pdf`. Contiene todo lo de las ramas de abajo más el trabajo de septiembre y octubre: 64 commits, 719 archivos y +40k/−16k líneas contra `main`. El análisis completo, los riesgos y las decisiones están en [`MATRIZ_INTEGRACION.md`](MATRIZ_INTEGRACION.md).

## Antecedentes en GitHub (versiones anteriores de la misma reestructuración)

| Rama | Autor | Fecha | Contenido |
|---|---|---|---|
| `codex/saneamiento-etapa1` (PR #127, abierto y en conflicto) | d0j0fre | 19–21/08 | Stage 1: seguridad, capacidades/permisos efectivos, revocación de sesiones, checkout y stock cero, órdenes de compra, kilometraje, datos offline. 137 archivos. |
| `codex/stage2-design-system` (incluye todo Stage 1) | d0j0fre | 21–31/08 | Stage 2: identidad visual Distribuidora JJ, sistema de diseño y layouts, migración de storefront, portal, admin, inventario, compras, finanzas, crédito, presupuestos, RRHH, planilla y entregas del chofer. 243 archivos (+7 543 / −9 456 líneas), 95 nuevos. Incluye pruebas de contrato y un smoke E2E (Playwright). |

- Parte de `main` del 12/08 (`6e36ae1`). Desde ese punto `main` solo agregó la app móvil y la API JWT (`595b7b2`). La reestructuración no toca `Proyecto_Movil`.
- **83 archivos fueron modificados en ambos lados**: `Program.cs`, `AdminDbService.cs`, `_Layout.cshtml`, `appsettings.json` y ~70 vistas (Account, Budgets, Cart, ClientPortal, Clients, Employees, Expenses, Inventory, OrdersAdmin, Payroll, etc.). Ahí habrá conflictos de merge.
- Varios bugs del backlog parecen tocados por Stage 1 (dato de referencia para la integración, **no se usa durante las correcciones**):

| Bug | Commit de Stage 1 relacionado |
|---|---|
| BUG-04-03, BUG-11-06 (acceso tras inactivar rol o empleado) | `fix: revocar sesiones tras cambios de seguridad` |
| BUG-09-02, BUG-16-07, BUG-22-02 (Gerente/Auditor sin acceso) | `fix: reemplazar autorizacion por capacidades efectivas`, `fix: separar permisos de lectura y mutacion` |
| BUG-12-03 (checkout acepta más que el stock) | `fix: conservar intencion de login y bloquear stock cero`, `fix: sanear checkout...` |
| BUG-07-01, BUG-07-02 (offline del vendedor) | `fix: aislar y expirar datos offline por usuario` (solo la parte web) |

## Relación con las correcciones

La reestructuración va **completamente por aparte**. Durante las correcciones no se consulta ni se copia nada de `restructuracion-2.0`: se corrige `main` según el reporte de errores (ver `PLAN_CORRECCIONES.md`). Al integrar, el historial de commits de cada corrección (`fix(BUG-xx-yy): ...`) sirve para saber qué conservar.

## Integración selectiva (después de corregir y re-probar `main`)

Cuánto se integra depende del tiempo que quede antes de la presentación (ver `MATRIZ_INTEGRACION.md` §6). Lo mínimo es la capa 1.

Se hace en una rama `integracion/reestructuracion` desde `main` ya corregido y por capas, de menor a mayor riesgo. Cada capa se mergea solo si pasa build, tests y los casos QA de los módulos tocados.

| Capa | Qué entra | Riesgo | Verificación |
|---|---|---|---|
| 1. Fundamentos visuales | Identidad, tokens CSS, layouts, componentes, `_Layout` | Bajo | Recorrido visual por rol y responsive |
| 2. Seguridad Stage 1 | Revocación de sesiones, capacidades y permisos (comparar con lo ya corregido en `main`) | Medio | `dotnet test` + matriz de autorización + casos de módulos 1, 4, 9 y 22 |
| 3. Vistas por módulo | Storefront → portal → admin/inventario → compras → finanzas/crédito → RRHH/planilla → entregas | Medio | Casos QA del módulo + conservar las correcciones (`fix(BUG-xx-yy)`) que tocaron esas vistas |
| 4. Funcionalidades nuevas | Lo que no existía en `main` | Alto | Casos nuevos escritos para esa funcionalidad |

Para cada cambio de la reestructuración se decide en `MATRIZ_INTEGRACION.md`: **Adoptar** · **Adoptar con ajustes** · **Mantener `main`** · **Descartar**. Criterio: gana la versión que cumple la HU y el caso QA. Si ambas cumplen, gana la del nuevo diseño.

Se descarta o pospone lo que cambie contratos de BD o API que la app móvil usa, renombre rutas que los casos QA referencian sin dar redirección, o no se pueda verificar con un caso.

## Regresión final de la integración

Se re-ejecutan los casos de `G8_SC803_J_CasosQA.xlsx` de los módulos tocados por la integración. Si una capa no pasa, se revierte y `main` corregido sigue siendo la entrega. Los bugs nuevos se registran con el mismo formato (`BUG-MM-NN`) en un backlog de la ronda 2.

## Insumos del compañero

1. ✅ Informe de trabajo: `fuentes/INFORME-STAGE2-INTEGRACION.pdf`.
2. ✅ Versión en GitHub: rama `restructuracion-2.0`.
3. Pendiente: confirmar con el autor los riesgos R1–R4 de la matriz (migraciones, API de la app móvil, Chat V2 y modelo comercial).
