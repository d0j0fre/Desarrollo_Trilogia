# Rollback de 0028

## Qué introduce

- Tabla `dbo.MovilOperaciones`: identificador de cada escritura hecha desde la
  aplicación, para que los reintentos de la cola no dupliquen.
- Tabla `dbo.PedidoPreparacion`: qué pedidos dejó listos bodega, quién y cuándo.
  No toca `Pedidos.Estado`.
- Permiso nuevo `PEDIDOS_PREPARAR`.
- Asignaciones en `dbo.PerfilPermisos`, todas marcadas con
  `UsuarioAsignacionNombre = 'Migración 0028 aplicación móvil por rol'`.
- Procedimientos nuevos `sp_Movil_*`. Ningún procedimiento existente cambia.

## Efecto en el sitio web

Las asignaciones de permisos también habilitan en el sitio web los módulos
equivalentes. Es intencional: siete perfiles de oficina no tenían ningún
permiso y tampoco podían usar sus módulos web.

| Perfil | Permisos agregados |
|---|---|
| Bodeguero | INVENTARIO_VER, INVENTARIO_MOVIMIENTOS, COMPRAS_ORDENES_VER, COMPRAS_ORDENES_RECIBIR, PEDIDOS_VER, PEDIDOS_PREPARAR |
| Gerente | PEDIDOS_VER, INVENTARIO_VER |
| Supervisor | RRHH_JORNADAS_APROBAR |
| Cajero | LIQUIDACION_FINANCIERA |
| Facturador | FACTURACION_VER, PEDIDOS_VER |
| Crédito y Cobro | CREDITOS_VER, CLIENTES_VER |
| Compras | COMPRAS_ORDENES_VER, COMPRAS_SUGERENCIAS_VER, INVENTARIO_VER, PROVEEDORES_VER |
| Soporte | CONSULTAS_VER, CONSULTAS_ATENDER |
| Auditor Interno | AUDITORIA_VER |

Además, `MOVIL_ACCESO` a los siete perfiles de oficina, y
`RRHH_JORNADAS_REGISTRAR` / `RRHH_JORNADAS_VER_PROPIAS` a todo el personal que
no los tenía. Cliente no recibe nada.

## Rollback ordinario: quitar un permiso a un perfil

Desde el sitio web, módulo **Permisos**. No hace falta tocar la base a mano.

## Rollback completo

Solo con aprobación y después de un BACPAC. Borra la historia de preparación de
pedidos y de operaciones móviles.

```sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DELETE pp
FROM dbo.PerfilPermisos pp
WHERE pp.UsuarioAsignacionNombre = N'Migración 0028 aplicación móvil por rol';

DELETE pp
FROM dbo.PerfilPermisos pp
INNER JOIN dbo.Permisos p ON p.PermisoId = pp.PermisoId
WHERE p.Codigo = N'PEDIDOS_PREPARAR';
DELETE dbo.Permisos WHERE Codigo = N'PEDIDOS_PREPARAR';

DROP PROCEDURE IF EXISTS dbo.sp_Movil_Productos_Listar;
DROP PROCEDURE IF EXISTS dbo.sp_Movil_Inventario_MovimientosProducto;
DROP PROCEDURE IF EXISTS dbo.sp_Movil_Inventario_RegistrarMovimiento;
DROP PROCEDURE IF EXISTS dbo.sp_Movil_Producto_CambiarEstado;
DROP PROCEDURE IF EXISTS dbo.sp_Movil_Pedidos_Listar;
DROP PROCEDURE IF EXISTS dbo.sp_Movil_Pedido_Detalle;
DROP PROCEDURE IF EXISTS dbo.sp_Movil_Bodega_PedidosPorPreparar;
DROP PROCEDURE IF EXISTS dbo.sp_Movil_Bodega_MarcarPreparado;
DROP PROCEDURE IF EXISTS dbo.sp_Movil_Rutas_Reasignar;
DROP PROCEDURE IF EXISTS dbo.sp_Movil_Gestion_Operacion;

DROP TABLE IF EXISTS dbo.PedidoPreparacion;
DROP TABLE IF EXISTS dbo.MovilOperaciones;

UPDATE dbo.SchemaMigrationHistory
SET Status = N'RolledBack'
WHERE MigrationId = N'0028_mobile_roles_surface';

COMMIT TRANSACTION;
```

Los movimientos de inventario, recepciones, reasignaciones y cambios de estado
de producto hechos desde la aplicación **no se revierten**: son operaciones de
negocio ya registradas en sus tablas y en `AuditoriaSistema`.

Tras el rollback, la versión de la API debe volver a la anterior: sus endpoints
de bodega, gestión y oficina dependen de estos procedimientos.
