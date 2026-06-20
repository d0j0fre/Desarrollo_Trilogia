# Resumen de mejoras de seguridad y arquitectura

## Objetivo

Este documento resume las mejoras aplicadas en la rama `feature/qa-seguridad-arquitectura` para reforzar seguridad, base de datos, permisos, reportes, API y calidad tecnica del proyecto.

## Mejoras aplicadas

### Migracion de consultas SQL directas a procedimientos almacenados

Se migraron consultas directas relacionadas con comprobantes y validacion de facturas hacia procedimientos almacenados dedicados.

Mejoras principales:

- Validacion de factura por pedido mediante procedimiento.
- Obtencion de comprobante cliente por `PedidoId` y `UsuarioId`.
- Obtencion de lineas de comprobante cliente con validacion de propietario.
- Eliminacion de SQL directo en esos flujos.

Script relacionado:

- `database/cu091_migracion_pedidos_facturacion_sp.sql`

### Proteccion de estados de pedidos facturados

Se reforzo el procedimiento de cambio de estado de pedidos admin para evitar inconsistencias entre pedidos y facturas.

Reglas reforzadas:

- No cambiar pedido facturado a `Cancelado`.
- No regresar pedido facturado a estados previos.
- Mantener pedido facturado consistente como `Entregado`.
- Bloquear transiciones invalidas desde `Cancelado`.

Script relacionado:

- `database/cu092_admin_estado_pedido_seguro.sql`

### Correccion de reportes de facturacion

El reporte administrativo de facturacion ahora usa procedimientos agregados reales para:

- Productos mas vendidos.
- Ventas por mes.

Esto evita calculos debiles basados solo en las ultimas facturas consultadas.

Script relacionado:

- `database/cu093_admin_reportes_facturacion_sp.sql`

### Mensajes genericos en errores visibles

Se reemplazo exposicion directa de `ex.Message` en controladores MVC sensibles por mensajes genericos para usuario final.

Beneficio:

- Evita filtrar nombres de procedimientos.
- Evita filtrar errores SQL.
- Evita filtrar rutas internas o detalles tecnicos.

### AdminAuthorize reforzado con UserId

`AdminAuthorizeAttribute` fue reforzado para exigir:

- `UserId` valido en sesion.
- `UserEmail` no vacio.
- `UserRole` valido.

Se mantiene:

- Bypass del rol `Administrador`.
- Validacion por modulo.
- Validacion de permisos existentes.

### Validacion real de firma de imagenes

La carga de imagenes en inventario valida magic bytes basicos para:

- JPG/JPEG.
- PNG.
- WEBP.

Se mantiene:

- Tamano maximo existente.
- Extensiones permitidas.
- Content-types permitidos.
- Nombre final con GUID.

### Antiforgery en vistas legacy

Se agrego `@Html.AntiForgeryToken()` en formularios legacy de `Views/Security` como defensa en profundidad.

Archivos relacionados:

- `Views/Security/CrearRol.cshtml`
- `Views/Security/EditarRol.cshtml`
- `Views/Security/Permisos.cshtml`

### Permisos granulares por accion

Se agrego infraestructura para validar permisos administrativos por codigo exacto.

Componentes:

- Procedimiento `sp_Admin_HasPermissionByCode`.
- Metodo `TienePermisoCodigoPorRolAsync`.
- Soporte opcional en `AdminAuthorizeAttribute`.

Script relacionado:

- `database/cu094_permisos_granulares_acciones.sql`

### Acciones criticas protegidas con permisos exactos

Se aplicaron permisos granulares a acciones criticas:

- Cambiar estado de pedido: `PEDIDOS_CAMBIAR_ESTADO`.
- Crear/editar/activar/inactivar rol: `ROLES_CREAR_EDITAR`.
- Asignar permisos: `PERMISOS_ASIGNAR`.

### Proteccion de generacion de facturas

Se creo el permiso `FACTURACION_GENERAR` y se protegio la accion administrativa de generar factura desde pedido.

Script relacionado:

- `database/cu090_admin_facturar_pedido.sql`
- `database/cu095_facturacion_generar_permiso.sql`

Nota:

- `database/cu090_admin_facturar_pedido.sql` crea `dbo.sp_Admin_GenerateInvoiceFromOrder`; debe ejecutarse antes de probar generacion de facturas desde pedidos.

### Correccion controlada de mojibake en productos

Se agrego un script especifico para corregir nombres y descripciones de productos afectados por texto mal codificado, incluyendo nombres copiados en detalles historicos de factura cuando aplique.

Script relacionado:

- `database/cu096_corregir_mojibake_productos.sql`

### API publica de productos y categorias

Se agregaron endpoints publicos de solo lectura:

- `GET /api/products`
- `GET /api/products?categoria=`
- `GET /api/products?buscar=`
- `GET /api/products/{id}`
- `GET /api/products/categories`
- `GET /api/products/featured?take=`

Los endpoints usan servicio propio del API y procedimientos almacenados existentes.

### Documentacion de autenticacion API futura

Se documento que el API actual:

- No emite JWT.
- No emite token de autorizacion.
- No debe exponer endpoints protegidos todavia.

Recomendacion documentada:

- Mantener productos/categorias como publicos.
- Implementar JWT solo en fase futura aprobada.
- Mantener compatible la respuesta actual del login para no romper MVC.

Documento relacionado:

- `docs/api-auth-futura.md`

### Warnings/nullability en 0

Se corrigieron warnings conocidos de nullability en modelos y vistas MVC mediante inicializaciones seguras y validaciones null-safe minimas.

Resultado esperado:

- Build con 0 errores.
- Build con 0 warnings.

## Archivos sensibles no modificados

No se deben incluir cambios en:

- `appsettings.json`
- `appsettings.Development.json`
- secretos
- cadenas de conexion
- `bin/`
- `obj/`
- `.vs/`
- ZIPs
- archivos generados

## Resultado de seguridad esperado

El proyecto queda mas consistente para revision final:

- Menos SQL directo en flujos criticos.
- Menor exposicion de errores tecnicos.
- Mejor control de permisos administrativos.
- Mejor validacion de archivos subidos.
- Mejor documentacion de riesgos API.
- Build limpio esperado.
