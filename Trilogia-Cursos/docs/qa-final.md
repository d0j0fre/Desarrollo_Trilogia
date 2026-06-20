# QA final del proyecto

## Objetivo

Este documento deja el checklist de pruebas finales para validar la rama `feature/qa-seguridad-arquitectura` antes de abrir Pull Request hacia `main`.

## Estado tecnico esperado

- Build de solucion completa: `dotnet build Trilogia-Cursos\Proyecto_Final.slnx`.
- Resultado esperado: 0 errores.
- Resultado esperado: 0 warnings.
- `appsettings.json` y `appsettings.Development.json` sin cambios.
- Sin `bin/`, `obj/`, `.vs/`, ZIPs ni archivos generados pendientes.
- Scripts SQL nuevos ejecutados en SSMS antes de pruebas funcionales completas.

## Checklist cliente

- [ ] Login cliente.
- [ ] Home.
- [ ] Tienda.
- [ ] Detalle producto.
- [ ] Carrito.
- [ ] Checkout.
- [ ] Confirmacion de compra.
- [ ] Mis pedidos.
- [ ] Detalle pedido.
- [ ] Cancelacion de pedido pendiente, si aplica.
- [ ] Comprobante.
- [ ] Mi Perfil.
- [ ] Logout.

## Checklist admin

- [ ] Login admin.
- [ ] Dashboard.
- [ ] Inventario.
- [ ] Pedidos admin.
- [ ] Cambio de estado de pedido.
- [ ] Generacion de factura desde pedido.
- [ ] Facturacion.
- [ ] Detalle factura.
- [ ] Reporte de productos mas vendidos.
- [ ] Reporte de ventas por mes.
- [ ] Clientes.
- [ ] Creditos.
- [ ] Consultas.
- [ ] Empleados.
- [ ] Roles.
- [ ] Permisos.
- [ ] Auditoria.
- [ ] Logout.

## Checklist empleado y vendedor

- [ ] Login empleado/vendedor si existen credenciales en la base local.
- [ ] Portal empleado.
- [ ] Pedidos vendedor.
- [ ] Accesos permitidos.
- [ ] Accesos bloqueados.
- [ ] Logout.

## Checklist API

- [ ] `POST /api/auth/login`.
- [ ] `POST /api/auth/register`.
- [ ] `POST /api/auth/forgot-password`.
- [ ] `POST /api/auth/reset-password`.
- [ ] `GET /api/products`.
- [ ] `GET /api/products?categoria=`.
- [ ] `GET /api/products?buscar=`.
- [ ] `GET /api/products/{id}` con producto existente.
- [ ] `GET /api/products/{id}` con producto inexistente y respuesta `404`.
- [ ] `GET /api/products/categories`.
- [ ] `GET /api/products/featured?take=`.

## Checklist tecnico

- [ ] Build solucion completa.
- [ ] Confirmar 0 errores.
- [ ] Confirmar 0 warnings.
- [ ] Confirmar `appsettings` sin cambios.
- [ ] Confirmar sin `bin/`, `obj/`, `.vs`, ZIPs ni archivos generados pendientes.
- [ ] Confirmar scripts SQL pendientes/ejecutados.
- [ ] Confirmar que no hay errores de consola principales.
- [ ] Confirmar que no hay 404 de scripts principales.
- [ ] Confirmar que comprobante cliente imprime correctamente.
- [ ] Confirmar que factura admin imprime correctamente.

## Scripts SQL a validar en SSMS

Estos scripts deben ejecutarse en la base `DistribuidoraJJ_DB` antes de pruebas funcionales completas:

- `database/cu091_migracion_pedidos_facturacion_sp.sql`
- `database/cu092_admin_estado_pedido_seguro.sql`
- `database/cu093_admin_reportes_facturacion_sp.sql`
- `database/cu094_permisos_granulares_acciones.sql`
- `database/cu095_facturacion_generar_permiso.sql`

## Pruebas negativas recomendadas

- [ ] Intentar ver comprobante de otro cliente.
- [ ] Intentar cancelar pedido facturado.
- [ ] Intentar facturar pedido cancelado.
- [ ] Intentar duplicar factura de un pedido.
- [ ] Intentar entrar a rutas admin sin sesion.
- [ ] Intentar entrar a rutas admin con rol no autorizado.
- [ ] Intentar subir imagen con extension valida pero contenido invalido.
- [ ] Intentar usar endpoint protegido futuro sin token, si se implementa despues.

## Resultado esperado de cierre

- Build: 0 errores, 0 warnings.
- Rama lista para PR.
- Sin archivos sensibles modificados.
- Sin archivos generados incluidos.
- Scripts SQL documentados.
- Pruebas manuales principales completadas o marcadas como pendientes.
