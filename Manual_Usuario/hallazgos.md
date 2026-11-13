## 4. Hallazgos que afectan al manual (para el gerente)

Estado al 02/11/2026, verificado con el código de la rama `correcciones-esteban-manual` (`8f8ffcd`) y una base v2 con las migraciones 0251 a 0257 aplicadas en orden (`(localdb)\Trilogia_manualfinal2`). Los ocho hallazgos de la primera ronda están resueltos en esa rama. Los hallazgos de cada grupo, con su estado, están en `_hallazgos_grupo1.md`, `hallazgos_grupo2.md` y `_hallazgos_grupo3.md`.

| N.º | Hallazgo | Estado |
|---|---|---|
| 1 | Perfiles de oficina (7–14) sin permisos en la base demo | Resuelto por la migración 0251. **Producción debe tener aplicadas las migraciones 0251 a 0257**: el manual documenta esos permisos y las pantallas que cambian con ellas. |
| 2 | Módulos permitidos sin botón en el menú web | Resuelto: un solo panel «Administración» que muestra a cada puesto las áreas y opciones de su permiso. |
| 3 | Botones del menú que llevaban a «Acceso denegado» (Finanzas, Planilla, Histórico de precios) | Resuelto: el panel solo muestra lo que el puesto puede abrir. |
| 4 | Presupuestos y Gastos caían (503) con Windows en español | Resuelto (PR #131): las listas y los formularios de creación abren con cultura es-CR. |
| 5 | Mis jornadas (web): borrador o rechazada no se podían completar | Resuelto: botón «Completar». El capítulo de ejemplo ya lo documenta. |
| 6 | App: «Marcar leídas» casi invisible | Resuelto: ahora en color vino. |
| 7 | El logo se encimaba con «Inicio/Tienda» en pantallas medianas | Resuelto: la barra pasa a dos líneas. |
| 8 | Datos del PR #131 aún no en `main` | Resuelto: #131 está en `main` y en la base v2. Sondeos rehechos con 8f8ffcd. |

### Cambio de acceso con 8f8ffcd

- **Registrar movimiento** exige ahora `INVENTARIO_MOVIMIENTOS` (G2-40): solo lo abren Administrador, Empleado y Bodeguero. Gerente y Compras ven la lista de Movimientos, pero sin el botón, y por dirección reciben «Acceso denegado». El sondeo web lo prueba desde esta versión.
- El menú visible y el resto del acceso por perfil (web y app) no cambiaron respecto de 0e4fb19.

### Permisos reales por perfil (base v2 + 0251–0257, 02/11/2026)

Sustituyen a los conteos viejos que circulan en el equipo (95, 18, 12…) y a la tabla anterior «con matriz 0028». Es lo que documenta el manual. Las migraciones 0252 a 0257 no cambian ningún conteo.

| Perfil | Permisos |
|---|---|
| Administrador | 100 |
| Gerente | 28 |
| Empleado | 13 |
| Bodeguero | 9 |
| Supervisor | 8 |
| Chofer | 7 |
| Compras | 7 |
| Cajero | 5 |
| Facturador | 5 |
| Crédito y Cobro | 5 |
| Soporte | 5 |
| Auditor Interno | 5 |
| Vendedor | 5 |
| Cliente | 0 |
