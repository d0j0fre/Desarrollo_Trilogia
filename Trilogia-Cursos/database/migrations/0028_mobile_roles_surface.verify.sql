SET NOCOUNT ON;

/* Verificacion de 0028. Solo lectura. */
IF OBJECT_ID(N'dbo.MovilOperaciones', N'U') IS NULL
    THROW 55850, N'Falta dbo.MovilOperaciones.', 1;
IF OBJECT_ID(N'dbo.PedidoPreparacion', N'U') IS NULL
    THROW 55851, N'Falta dbo.PedidoPreparacion.', 1;

IF EXISTS (
    SELECT name FROM (VALUES
        (N'sp_Movil_Productos_Listar'), (N'sp_Movil_Inventario_MovimientosProducto'),
        (N'sp_Movil_Inventario_RegistrarMovimiento'), (N'sp_Movil_Producto_CambiarEstado'),
        (N'sp_Movil_Pedidos_Listar'), (N'sp_Movil_Pedido_Detalle'),
        (N'sp_Movil_Bodega_PedidosPorPreparar'), (N'sp_Movil_Bodega_MarcarPreparado'),
        (N'sp_Movil_Rutas_Reasignar'), (N'sp_Movil_Gestion_Operacion')
    ) expected(name)
    WHERE OBJECT_ID(N'dbo.' + expected.name, N'P') IS NULL)
    THROW 55852, N'Falta al menos un procedimiento sp_Movil_* de 0028.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Codigo = N'PEDIDOS_PREPARAR' AND Activo = 1)
    THROW 55853, N'Falta el permiso PEDIDOS_PREPARAR.', 1;

/* Todo perfil del personal puede entrar a la aplicacion; Cliente no. */
IF EXISTS (
    SELECT 1 FROM dbo.Perfiles pf
    WHERE pf.Nombre <> N'Cliente'
      AND NOT EXISTS (SELECT 1 FROM dbo.PerfilPermisos pp INNER JOIN dbo.Permisos p ON p.PermisoId = pp.PermisoId
                      WHERE pp.PerfilId = pf.PerfilId AND p.Codigo = N'MOVIL_ACCESO'))
    THROW 55854, N'Hay un perfil del personal sin MOVIL_ACCESO.', 1;
IF EXISTS (
    SELECT 1 FROM dbo.PerfilPermisos pp
    INNER JOIN dbo.Perfiles pf ON pf.PerfilId = pp.PerfilId
    INNER JOIN dbo.Permisos p ON p.PermisoId = pp.PermisoId
    WHERE pf.Nombre = N'Cliente' AND p.Codigo = N'MOVIL_ACCESO')
    THROW 55855, N'El perfil Cliente no debe tener MOVIL_ACCESO.', 1;

/* Los procedimientos web que la app reutiliza siguen existiendo. */
IF OBJECT_ID(N'dbo.sp_Admin_UpdateOrderStatus', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_Compras_RecibirDetalle', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_Manager_ApproveOrder', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_Seller_CreateOrder', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_RRHH_GuardarMiJornada', N'P') IS NULL
    THROW 55856, N'Falta un procedimiento web que la aplicación reutiliza.', 1;

SELECT pf.Nombre AS Perfil, COUNT(*) AS Permisos,
       STRING_AGG(p.Codigo, N', ') WITHIN GROUP (ORDER BY p.Codigo) AS Codigos
FROM dbo.PerfilPermisos pp
INNER JOIN dbo.Perfiles pf ON pf.PerfilId = pp.PerfilId
INNER JOIN dbo.Permisos p ON p.PermisoId = pp.PermisoId
WHERE pf.Nombre <> N'Administrador'
GROUP BY pf.Nombre
ORDER BY pf.Nombre;

PRINT N'0028 verificada.';
