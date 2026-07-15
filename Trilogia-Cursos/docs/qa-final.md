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
- [ ] Login visualmente limpio en escritorio, pantalla mediana y movil.
- [ ] Login invalido muestra error sin romper el diseño.
- [ ] Link de recuperar contrasena visible.
- [ ] Home.
- [ ] Tienda.
- [ ] Detalle producto.
- [ ] Carrito.
- [ ] Checkout.
- [ ] Checkout con pago simulado academico.
- [ ] Confirmacion de compra.
- [ ] Mis pedidos.
- [ ] Detalle pedido.
- [ ] Cancelacion de pedido pendiente, si aplica.
- [ ] Comprobante.
- [ ] Mi Perfil.
- [ ] Logout.

## Checklist admin

- [ ] Login admin.
- [ ] Login admin correcto desde la pantalla rediseñada.
- [ ] Dashboard.
- [ ] Inventario.
- [ ] Pedidos admin.
- [ ] Cambio de estado de pedido.
- [ ] Generacion de factura desde pedido.
- [ ] Facturacion.
- [ ] Detalle factura.
- [ ] Tabla de facturas: boton `Ver` visible completo en escritorio y pantalla mediana.
- [ ] Reporte de productos mas vendidos.
- [ ] Reporte de ventas por mes.
- [ ] Inventario descuenta stock al crear pedido desde checkout.
- [ ] Inventario no vuelve a descontar stock al generar factura.
- [ ] Clientes.
- [ ] Creditos.
- [ ] Consultas.
- [ ] Empleados.
- [ ] Roles.
- [ ] Permisos.
- [ ] Auditoria.
- [ ] Logout.

## Checklist registro

- [ ] Abrir Registro.
- [ ] Registro visualmente consistente con Login.
- [ ] Registro vacio muestra validaciones.
- [ ] Checkbox de terminos sigue funcionando.
- [ ] Registro con datos validos funciona.
- [ ] Link hacia Login funciona.
- [ ] Responsive en pantalla mediana y movil.

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
- [ ] `GET /api/products/0` debe devolver `400`.
- [ ] `GET /api/products/-1` debe devolver `400`.
- [ ] `GET /api/products/categories`.
- [ ] `GET /api/products/featured?take=`.
- [ ] `GET /api/products/featured?take=0` debe devolver `400`.
- [ ] `GET /api/products/featured?take=-1` debe devolver `400`.
- [ ] `GET /api/products/featured?take=100` debe devolver `200` con maximo 24 resultados.

## Checklist datos y mojibake

- [ ] Confirmar que Tienda muestra `Ron Añejo`.
- [ ] Confirmar que Checkout muestra `Ron Añejo`.
- [ ] Confirmar que API `/api/products` muestra `Ron Añejo`.
- [ ] Confirmar que Billing/Factura muestra `Ron Añejo` si aplica.

## Checklist pago simulado e inventario

- [ ] Producto con stock 15: comprar 10 unidades y confirmar stock final 5.
- [ ] Intentar comprar mas unidades que el stock disponible y confirmar rechazo con mensaje claro.
- [ ] Confirmar que el stock no cambia cuando el pedido falla por stock insuficiente.
- [ ] Crear pedido con `Efectivo contra entrega`.
- [ ] Crear pedido con `SINPE Movil simulado` y referencia opcional.
- [ ] Crear pedido con `Tarjeta demo` sin ingresar numero real de tarjeta ni CVV.
- [ ] Crear pedido con `Transferencia simulada` y referencia opcional.
- [ ] Confirmar que el admin puede ver metodo, estado, referencia y fecha de pago en detalle de pedido.
- [ ] Crear pedido pendiente con inventario descontado, cancelarlo como cliente y confirmar que el stock se restaura.
- [ ] Crear pedido pendiente con inventario descontado, cancelarlo como admin y confirmar que el stock se restaura.
- [ ] Generar factura de un pedido con inventario descontado y confirmar que el stock no se descuenta otra vez.
- [ ] Confirmar que la factura aparece en reportes de facturacion.

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
- [ ] Revisar que no haya textos con caracteres dañados tipo `Ã`, `Â`, `estÃ¡`, `opciÃ³n` o `contraseÃ±a` en pantallas principales.

## QA Azure publicado

Detalle completo: `docs/azure-despliegue-final-qa.md`.

URLs:

- API: `https://api-trilogia-cursos-dev-cr01-haefhbgrd8dvcvc5.centralus-01.azurewebsites.net/`
- MVC: `https://web-trilogia-cursos-dev-cr01-bbeyeedbfjejcgau.centralus-01.azurewebsites.net/`
- Swagger: `https://api-trilogia-cursos-dev-cr01-haefhbgrd8dvcvc5.centralus-01.azurewebsites.net/swagger`
- Healthcheck: `https://api-trilogia-cursos-dev-cr01-haefhbgrd8dvcvc5.centralus-01.azurewebsites.net/health`
- Productos API: `https://api-trilogia-cursos-dev-cr01-haefhbgrd8dvcvc5.centralus-01.azurewebsites.net/api/productos`

Pruebas aprobadas:

- [x] Azure SQL DEV online.
- [x] SSMS conecta a `DistribuidoraJJ_DB_DEV`.
- [x] Stored Procedures principales validados.
- [x] API local contra Azure SQL DEV.
- [x] MVC local contra Azure SQL DEV.
- [x] API Azure `/health`.
- [x] API Azure `/swagger`.
- [x] API Azure `/api/productos`.
- [x] MVC Azure Home.
- [x] MVC Azure Login.
- [x] Login admin.
- [x] Login cliente.
- [x] Login vendedor/empleado.
- [x] Admin.
- [x] Shop.

Pruebas recomendadas pendientes:

- [ ] Checkout completo en Azure.
- [ ] Carrito completo en Azure.
- [ ] Facturacion completa en Azure.
- [ ] Inventario completo en Azure.
- [ ] Subida de imagenes en Azure.
- [ ] Recuperacion de contrasena / SMTP real.
- [ ] Pruebas con companeros desde otras IPs.

## Scripts SQL a validar en SSMS

Estos scripts deben ejecutarse en la base `DistribuidoraJJ_DB` antes de pruebas funcionales completas:

- `database/cu090_admin_facturar_pedido.sql`
- `database/cu091_migracion_pedidos_facturacion_sp.sql`
- `database/cu092_admin_estado_pedido_seguro.sql`
- `database/cu093_admin_reportes_facturacion_sp.sql`
- `database/cu094_permisos_granulares_acciones.sql`
- `database/cu095_facturacion_generar_permiso.sql`
- `database/cu096_corregir_mojibake_productos.sql`
- `database/cu097_pago_simulado_inventario_checkout.sql`

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
