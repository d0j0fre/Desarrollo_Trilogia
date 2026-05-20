# Cambios administrativos incluidos

## Módulos agregados
- Dashboard administrativo
- Gestión de inventario
- Registro de movimientos de inventario
- Gestión de pedidos administrativos
- Facturación y finanzas

## Archivos nuevos principales
- `Proyecto_Final/Filters/AdminAuthorizeAttribute.cs`
- `Proyecto_Final/Services/AdminDbService.cs`
- `Proyecto_Final/Controllers/AdminController.cs`
- `Proyecto_Final/Controllers/InventoryController.cs`
- `Proyecto_Final/Controllers/OrdersAdminController.cs`
- `Proyecto_Final/Controllers/BillingController.cs`
- `Proyecto_Final/Models/Admin/*`
- `Proyecto_Final/Views/Admin/*`
- `Proyecto_Final/Views/Inventory/*`
- `Proyecto_Final/Views/OrdersAdmin/*`
- `Proyecto_Final/Views/Billing/*`
- `Proyecto_Final/wwwroot/css/admin.css`

## Archivos modificados
- `Proyecto_Final/Program.cs`
- `Proyecto_Final/Views/Shared/_Layout.cshtml`
- `database/DistribuidoraJJ_DB.sql`
- `database/password_reset_patch.sql`

## Base de datos
La base de datos sigue siendo `DistribuidoraJJ_DB` en SQL Server.
Se agregaron estas tablas:
- `MovimientosInventario`
- `Facturas`
- `FacturaDetalle`
- `PasswordResetTokens`

## Usuario administrador de prueba
- Correo: `admin@distribuidorajj.com`
- Contraseña: `1234`

## Orden recomendado para levantarlo
1. Abrir la solución en Visual Studio 2022.
2. Ejecutar `database/DistribuidoraJJ_DB.sql` en SQL Server.
3. Confirmar la cadena de conexión en ambos `appsettings.json`.
4. Establecer inicio múltiple para:
   - `Proyecto_Final`
   - `Proyecto_FinalAPI`
5. Correr ambos proyectos.
6. Iniciar sesión con el usuario administrador.

## Nota importante
En este entorno no tenía `dotnet` instalado para compilar y ejecutar una verificación automática aquí mismo. Los cambios quedaron preparados siguiendo la estructura existente del proyecto y manteniendo ASP.NET Core MVC + API + SQL Server.
