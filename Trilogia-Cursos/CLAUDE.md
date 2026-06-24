# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

University project for `DistribuidoraJJ / Licorera La Bodega` — a distributor/liquor store system.

- `Proyecto_Final/` — ASP.NET Core MVC web application (.NET 9)
- `Proyecto_FinalAPI/` — ASP.NET Core REST API (.NET 9)
- `database/` — All SQL scripts (schema, stored procedures, seed data, patches)
- `docs/` — Project documentation
- `Proyecto_Final.slnx` — Solution file

## Build and run

```powershell
# Build
dotnet build Proyecto_Final.slnx

# Run MVC (default https://localhost:7013)
dotnet run --project Proyecto_Final

# Run API (default https://localhost:57540)
dotnet run --project Proyecto_FinalAPI
```

Both projects should run simultaneously during development. No test project exists.

**Local network testing (MVC):** `http://0.0.0.0:5013`

## Database setup

Execute SQL scripts in `database/` in this order via SSMS or Azure Data Studio:

1. `DistribuidoraJJ_DB.sql` — full schema
2. Patch scripts in numeric order: `password_reset_patch.sql`, `categorias_migration.sql`, `cu033_*` through `cu097_*`
3. Stored procedures: `sp_admin_modulo.sql`, `sp_auth_api.sql`, `sp_Store_GetProducts.sql`
4. Seed data: `seed_*.sql` scripts

**Database:** `DistribuidoraJJ_DB` on `(localdb)\MSSQLLocalDB` (development default). Individual collaborators may use different SQL Server instances — do not standardize machine-specific connection strings in committed files.

## Architecture

### Data access

No Entity Framework. All database calls use raw ADO.NET (`SqlConnection`, `SqlCommand`, `SqlDataReader`) with stored procedures (`CommandType.StoredProcedure`). All calls are async. Null safety is always checked with `reader.IsDBNull()`.

```csharp
// Typical pattern
await using var conn = new SqlConnection(_connectionString);
await conn.OpenAsync();
using var cmd = new SqlCommand("dbo.sp_Admin_GetProducts", conn);
cmd.CommandType = CommandType.StoredProcedure;
cmd.Parameters.Add("@Filtro", SqlDbType.NVarChar, 100).Value = filtro;
```

### Authentication

Session-based (no JWT). Session cookie: `.DistribuidoraJJ.Session`, 45-minute idle timeout.

Session variables: `UserId` (int), `UserEmail`, `UserRole`, `UserFullName`.

Authorization levels:
- `[SessionAuthorize]` — any logged-in user
- `[AdminAuthorize]` — role must be `"Administrador"`
- `[AdminAuthorize("CODE")]` — requires specific permission code (e.g., `FACTURACION_GENERAR`, `ROLES_CREAR_EDITAR`, `PERMISOS_ASIGNAR`, `PEDIDOS_CAMBIAR_ESTADO`)

> Inspect `AdminAuthorizeAttribute.cs` before adding `[AdminAuthorize("...")]` — some versions do not accept a module string parameter.

### MVC project structure

**Services** (all receive `IConfiguration` for connection string):
- `AdminDbService.cs` (~1800 lines) — dashboard, products, inventory, orders, invoicing, clients, credits, roles, permissions, audit, employees
- `EmployeesDbService.cs` — employee CRUD, leave requests, tasks, salary/history
- `AccountDbService.cs` — user validation, password updates
- `StoreDbService.cs` — client-facing product catalog, cart, orders
- `AccountApiService.cs` — HttpClient calls to the API

**Controllers → Feature areas:**
- `AccountController` — login, register, password reset
- `AdminController` — admin dashboard
- `InventoryController`, `OrdersAdminController`, `BillingController` — inventory/orders/invoicing
- `ClientsController`, `CreditsController`, `ConsultationsController` — client management
- `EmployeesController`, `EmployeePortalController` — employee admin and self-service
- `RolesController`, `PermissionsController`, `AuditController` — RBAC and audit
- `CartController`, `ClientPortalController` — customer storefront
- `SellerOrdersController` — mobile sales / offline orders
- `SecurityController` — compatibility bridge to Roles, Permissions, Audit views (do not rename)

**Models** are ViewModels: `Models/Admin/*.cs` and `Models/Store/*.cs`.

### API project structure

Two controllers:
- `AuthController` — `POST /api/auth/login|register|forgot-password|reset-password`
- `ProductsController` — `GET /api/products` (with `?categoria=`, `?buscar=`, `?take=`), `/api/products/{id}`, `/api/products/categories`, `/api/products/featured`

Services: `AccountApiDbService`, `ProductsApiDbService`, `EmailService`, `EmailTemplateBuilder`, `LoginAttemptLimiter`, `PasswordRecoveryAttemptLimiter`.

## Database conventions

| Use | Do NOT use |
|---|---|
| `PerfilPermisos` | `PermisosPerfil` |
| `UsuarioAsignacionId`, `UsuarioAsignacionNombre` | `AsignadoPorUsuarioId`, `AsignadoPorNombre` |
| `PedidoOfflineGuid`, channel `"Venta móvil offline"` | any other offline order naming |

Inventory is decremented when an order is created (checkout), restored on cancellation (if pending, not yet billed). It is NOT decremented again on invoice generation.

New SQL changes must go in a new numbered script under `database/` (e.g., `cu098_descripcion.sql`).

## Non-negotiable rules

1. Do not modify `appsettings.json` or `appsettings.Development.json` unless explicitly requested.
2. Do not commit connection strings, email passwords, or any secrets.
3. Do not commit `bin/`, `obj/`, `.vs/`, ZIP files, or full project copies.
4. Always inspect the current file before editing — do not assume prior code is still exact.
5. SQL changes go in a separate script under `database/`, never inlined in C# services.
6. Do not break existing modules when adding new functionality.
7. Work on a feature branch, not directly on `main`.

## Commit style

Use conventional commits in Spanish:

```
feat(módulo): descripción breve
fix(módulo): descripción breve
style(módulo): descripción breve
chore(módulo): descripción breve
```

Examples: `feat(empleados): agregar solicitud de licencias`, `fix(checkout): descontar inventario al confirmar pedido`

## Demo credentials

| Role | Email | Password |
|---|---|---|
| Admin | `admin@distribuidorajj.com` | `1234` |
| Employees | `jose.solano@distribuidorajj.com`, `maria.vargas@...`, etc. | `1234` |

## Testing checklist after changes

1. `dotnet build` → 0 errors, 0 warnings
2. Login as admin → dashboard, inventory, orders, billing, clients, credits, roles, permissions, audit, employees
3. Login as employee → employee portal (salary, tasks, leave requests)
4. Client flow: shop → cart → checkout (simulated payment) → order confirmation → cancellation if pending
5. Verify no regression in unrelated modules
