# AGENTS.md — Desarrollo_Trilogia / Trilogia-Cursos

## Project identity
This repository contains a university project named `Trilogia-Cursos / Desarrollo_Trilogia`, a .NET MVC + SQL Server web application for a distributor/liquor store called `Licorera La Bodega / DistribuidoraJJ`.

Main projects:
- MVC: `Proyecto_Final`
- API: `Proyecto_FinalAPI`
- Database: `DistribuidoraJJ_DB`
- SQL scripts: `database/`
- Docs: `docs/`

## Current expected state
`main` already includes the work merged from branch `Danny`, including Sprint 3:
- Modern UI improvements for client side, home, shop, cart, footer, and confirmation page.
- Reusable custom modal replacing native browser alerts/confirmations.
- HTML email templates for password recovery and contact notifications.
- Local network testing setup.
- Base security hardening: security headers, safer session cookie, reduced technical error exposure, and login attempt limiter.
- Employee management module with admin views, employee portal, leave requests, tasks, salary/history, demo seed data, and documentation.
- Previous modules: login, register, forgot/reset password, dashboard, inventory, movements, orders, invoicing, roles, permissions, clients, credits, consultations, audit, mobile sales, offline orders CU-072, and Costa Rica demo data.

## Non-negotiable rules
1. Do not modify `appsettings.json` or `appsettings.Development.json` unless explicitly requested.
2. Do not commit local connection strings, real email passwords, API keys, or secrets.
3. Do not add ZIP files, `bin/`, `obj/`, `.vs/`, or full project copies to the repository.
4. Before changing important code, inspect the current repository files. Do not assume previous code is still exact.
5. Keep changes small and separated by purpose.
6. SQL changes must be provided as separate scripts inside `database/`.
7. Do not break existing modules while adding new functionality.
8. Prefer safe additive changes over risky rewrites.
9. Always include testing steps and a Git commit summary/description when proposing changes.
10. If there is a Git conflict, stop and ask for a screenshot or explicit resolution instructions.

## Local environment used by Danny
- SQL Server instance: `LAPTOP-MNV7AL4K\\SQLEXPRESS`
- Database: `DistribuidoraJJ_DB`
- MVC usual URL: `https://localhost:7013`
- API usual URL: `https://localhost:57540/`
- Local network MVC test URL used: `http://0.0.0.0:5013`
- Laptop IP observed during testing: `192.168.0.193`

Important: local connection strings may differ between collaborators. Do not standardize everyone’s machine-specific settings into committed files.

## Authentication and demo users
Admin test user:
- Email: `admin@distribuidorajj.com`
- Password: `1234`

Many demo users use password `1234`.

Employee demo examples:
- `jose.solano@distribuidorajj.com`
- `maria.vargas@distribuidorajj.com`
- `valeria.mora@distribuidorajj.com`
- `gabriela.alpizar@distribuidorajj.com`

## Important database conventions
Use the current permissions structure:
- Correct table: `PerfilPermisos`
- Correct assignment columns: `UsuarioAsignacionId`, `UsuarioAsignacionNombre`

Do not use old names:
- Do not use table `PermisosPerfil`
- Do not use columns `AsignadoPorUsuarioId` or `AsignadoPorNombre`

Offline orders CU-072 use:
- `PedidoOfflineGuid`
- channel: `Venta móvil offline`

## Known technical notes
- Some previous merge issues came from outdated references to `PermisosPerfil`; avoid reintroducing them.
- `SecurityController` is a compatibility bridge to Roles, Permissions, and Audit.
- `Views/Security/Roles.cshtml` should not reference `Proyecto_Final.Models.Perfil`.
- Current `AdminAuthorizeAttribute` may not accept a module string in some versions. Inspect the filter before adding attributes such as `[AdminAuthorize("Empleados")]`.
- Employee module uses `EmployeesDbService` and models in `Models/Admin/EmployeeViewModels.cs`.

## Safe development workflow
1. Work on a feature branch, not directly on `main`.
2. Pull the latest repository state first.
3. Inspect current files before editing.
4. Make one logical change at a time.
5. If SQL is required, add a script under `database/`.
6. Build and test locally.
7. Commit only relevant files.
8. Do not commit appsettings unless explicitly approved.
9. Push the feature branch.
10. Merge to `main` only after successful testing.

## Required testing checklist after changes
Build:
- Clean solution.
- Rebuild solution.
- Confirm 0 errors.

Core tests:
- Login admin: `admin@distribuidorajj.com / 1234`
- Home
- Shop
- Cart
- Custom modal confirmation
- Inventory
- Clients
- Credits
- Roles
- Permissions
- Audit
- Mobile sales
- Offline order flow
- Employees admin module
- Employee portal

Employee tests:
- Admin can list/create/edit employees.
- Admin can assign tasks.
- Admin can approve/reject leave requests.
- Employee can access `Mi portal`.
- Employee can view salary, job position, responsibilities, tasks, and leave requests.
- Employee can request leave with or without salary payment.

## Preferred response format for future implementation tasks
When implementing, provide:
1. Analysis of risk.
2. SQL script path and full SQL if needed.
3. New files to add.
4. Files to replace completely.
5. Files not to touch.
6. Manual test plan.
7. GitHub Desktop Summary.
8. GitHub Desktop Description.

## Suggested commit style
Examples:
- `style(ui): mejorar footer, carrito y espaciados generales`
- `style(storefront): mejorar inicio y tienda del cliente`
- `feat(ui): agregar modal propio reutilizable`
- `feat(email): maquetar correos del sistema`
- `chore(dev): permitir pruebas desde red local`
- `feat(security): reforzar seguridad base del sistema`
- `feat(empleados): implementar gestion base de empleados`
- `style(empleados): pulir interfaz del modulo`
- `chore(seed): agregar datos demo de empleados`

## First instruction for Codex when starting a task
Before editing, inspect the relevant files and summarize what will be changed. Do not modify appsettings or unrelated modules. Keep changes small, provide a test plan, and list the exact files changed.
