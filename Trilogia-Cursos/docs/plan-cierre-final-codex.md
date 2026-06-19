Estoy continuando el proyecto universitario .NET / SQL Server llamado Trilogia-Cursos / DistribuidoraJJ / Licorera La Bodega.

Contexto general:

* Proyecto en Visual Studio con MVC + API.
* Repositorio local:
  C:\Users\danny\Desktop\DISEÑO Y DESARROLLO\Desarrollo_Trilogia\Trilogia-Cursos
* Base de datos:
  DistribuidoraJJ_DB
* SQL Server local.
* Rama main ya contiene los últimos cambios integrados desde Danny.
* Rama Danny también debe mantenerse alineada, pero NO trabajés directo sobre main.
* Usuario admin demo:
  [admin@distribuidorajj.com](mailto:admin@distribuidorajj.com) / 1234
* Usuario cliente demo probable:
  [cliente@distribuidorajj.com](mailto:cliente@distribuidorajj.com) / 1234

Objetivo general:
Cerrar el proyecto con las mejoras visuales, QA, limpieza, documentación y verificación final que todavía faltan, sin romper lógica, seguridad, base de datos ni ramas principales.

IMPORTANTE:
No trabajés directo en main.
No trabajés directo en Danny para este cierre.
Creá una rama nueva desde main llamada:

final/cierre-visual-qa

Flujo obligatorio:

1. git switch main
2. git pull origin main
3. git switch -c final/cierre-visual-qa
4. Trabajar únicamente en esa rama.
5. Hacer cambios por bloques internos.
6. Ejecutar build después de cada bloque importante.
7. Hacer commits separados por bloque.
8. No hacer push a main.
9. No hacer merge a main.
10. Al final, reportar estado y recomendar PR: final/cierre-visual-qa -> main.

Reglas estrictas:

* No tocar appsettings.json ni appsettings.Development.json.
* No tocar cadenas de conexión.
* No tocar secretos.
* No subir bin/, obj/, .vs/, ZIPs ni archivos generados.
* No instalar paquetes NuGet.
* No usar CDN.
* No cambiar estructura de base de datos salvo que explícitamente se indique.
* No tocar Proyecto_FinalAPI salvo que sea documentación o build y no haya alternativa.
* No cambiar lógica de negocio.
* No cambiar controladores, servicios ni modelos salvo que sea estrictamente necesario para corregir build o una advertencia menor aprobada por el propio análisis.
* No romper login, registro, recuperación, carrito, checkout, portal cliente, facturación, empleados, roles ni permisos.
* No cambiar rutas.
* No cambiar asp-action, asp-controller, method, asp-for, name/id de inputs.
* No eliminar AntiForgeryToken.
* No tocar scripts de validación salvo que se detecte error real.
* No eliminar ni debilitar reglas de impresión de comprobantes/facturas.
* No hacer cambios masivos sin build.
* Si aparece conflicto, error crítico o duda de lógica, detenerte y reportar antes de seguir.

Estado ya completado:

* Seguridad base reforzada.
* Rate-limit para recuperación de contraseña.
* Mensajes genéricos.
* Validación de imágenes.
* SessionAuthorizeAttribute aplicado.
* Portal cliente.
* Historial y detalle de pedidos.
* Cancelación segura de pedidos pendientes.
* Comprobante/factura visible para cliente.
* Mi Perfil con nombre, correo, teléfono y dirección.
* Checkout modernizado visualmente.
* Scripts locales de checkout restaurados.
* Provincia/cantón/distrito en checkout.
* Detalle de producto modernizado.
* Portal cliente Mis pedidos y Detalle modernizados.
* Comprobante cliente modernizado visualmente.
* Login y Registro modernizados.
* Merge Danny/main ya integrado.
* Micro-fix EmployeesController con [AdminAuthorize("Empleados")] aplicado en integración.

Lo que falta cerrar ahora:

BLOQUE A — Recuperación de contraseña visual
Archivos permitidos:

* Proyecto_Final/Views/Account/ForgotPassword.cshtml
* Proyecto_Final/Views/Account/ResetPassword.cshtml
* Proyecto_Final/wwwroot/css/sprint4-frontend-polish.css

Objetivo:
Modernizar visualmente las pantallas de recuperación y restablecimiento de contraseña, sin tocar lógica, tokens, rate-limit, controladores, servicios ni mensajes sensibles.

Reglas específicas:

* Mantener AntiForgeryToken.
* Mantener Token hidden en ResetPassword.
* Mantener asp-for y validaciones.
* Mantener mensajes genéricos.
* No tocar AccountController.
* No tocar PasswordRecoveryAttemptLimiter.
* No cambiar textos de seguridad sensibles salvo mejora visual mínima.

Pruebas:

* Abrir ForgotPassword.
* Enviar correo vacío y validar mensajes.
* Enviar correo real/demo y confirmar que no rompe.
* Abrir ResetPassword si existe flujo disponible.
* Confirmar que no hay errores de consola.
* Build 0 errores.

Commit sugerido:
Summary:
style(auth): modernizar recuperacion de contraseña

Description:
Moderniza visualmente las pantallas de recuperacion y restablecimiento de contraseña con estilos s4- scoped, manteniendo tokens, validaciones, mensajes genericos y logica existente.

BLOQUE B — Mi Perfil visual
Archivos permitidos:

* Proyecto_Final/Views/Profile/Edit.cshtml
* Proyecto_Final/wwwroot/css/sprint4-frontend-polish.css

Objetivo:
Modernizar Mi Perfil para que parezca pantalla de “Mi cuenta”, no módulo administrativo.

Reglas específicas:

* No tocar ProfileController.
* No tocar AccountDbService.
* No tocar ProfileEditViewModel.
* Mantener asp-for de nombre, correo, teléfono y dirección.
* Mantener AntiForgeryToken.
* Mantener validaciones.
* Mantener mensajes de éxito/error.

Pruebas:

* Login admin.
* Abrir Mi Perfil.
* Editar teléfono/dirección.
* Guardar.
* Refrescar y confirmar persistencia.
* Confirmar que login y portal cliente siguen funcionando.
* Build 0 errores.

Commit sugerido:
Summary:
style(perfil): modernizar mi cuenta

Description:
Moderniza visualmente la pantalla Mi Perfil con estilos s4- scoped, manteniendo formulario, validaciones, AntiForgeryToken y logica existente.

BLOQUE C — Dashboard admin visual
Archivos permitidos:

* Proyecto_Final/Views/Admin/Index.cshtml
* Proyecto_Final/wwwroot/css/sprint4-frontend-polish.css
* Proyecto_Final/wwwroot/css/admin.css solo si ya se usa ahí y el cambio es pequeño.

Objetivo:
Mejorar el dashboard admin para que se vea profesional: métricas claras, accesos rápidos, módulos ordenados, mejor jerarquía visual.

Reglas específicas:

* No tocar AdminController.
* No tocar AdminDbService.
* No cambiar lógica de datos.
* No cambiar rutas.
* No eliminar acciones existentes.
* No romper permisos.

Pruebas:

* Login admin.
* Abrir dashboard.
* Probar accesos principales.
* Confirmar que empleados, inventario, pedidos y facturación siguen accesibles.
* Build 0 errores.

Commit sugerido:
Summary:
style(admin): modernizar dashboard principal

Description:
Moderniza visualmente el dashboard administrativo con mejor jerarquia, tarjetas de resumen y accesos claros, sin modificar logica, rutas ni permisos.

BLOQUE D — Inventario visual
Archivos permitidos:

* Proyecto_Final/Views/Inventory/Index.cshtml
* Proyecto_Final/Views/Inventory/Create.cshtml
* Proyecto_Final/Views/Inventory/Edit.cshtml
* Proyecto_Final/Views/Inventory/Movements.cshtml si existe.
* Proyecto_Final/wwwroot/css/sprint4-frontend-polish.css
* Proyecto_Final/wwwroot/css/admin.css solo si es necesario y limitado.

Objetivo:
Mejorar tablas, formularios y acciones de inventario. Nada escondido, botones claros, responsive decente.

Reglas específicas:

* No tocar InventoryController.
* No tocar servicios.
* No tocar modelos.
* No tocar subida/validación de imágenes.
* Mantener formularios, asp-for, enctype si existe, AntiForgeryToken.
* Mantener acciones crear, editar, eliminar/movimientos si existen.

Pruebas:

* Abrir inventario.
* Crear producto si se puede.
* Editar producto si se puede.
* Revisar imagen/producto sin romper.
* Revisar móvil.
* Build 0 errores.

Commit sugerido:
Summary:
style(inventario): mejorar vistas administrativas

Description:
Mejora visualmente las vistas de inventario, tablas, formularios y acciones administrativas, preservando rutas, formularios, validaciones y logica existente.

BLOQUE E — Pedidos admin y facturación admin visual
Archivos permitidos:

* Proyecto_Final/Views/OrdersAdmin/Index.cshtml
* Proyecto_Final/Views/Billing/Index.cshtml
* Proyecto_Final/Views/Billing/Detail.cshtml
* Proyecto_Final/wwwroot/css/sprint4-frontend-polish.css
* Proyecto_Final/wwwroot/css/admin.css solo si es necesario y limitado.

Objetivo:
Mejorar visualmente pedidos admin y facturación admin. Factura admin debe conservar impresión limpia.

Reglas específicas:

* No tocar OrdersAdminController.
* No tocar BillingController.
* No tocar AdminDbService.
* No cambiar estados ni acciones.
* No cambiar rutas.
* No romper factura imprimible.
* Mantener @media print si existe.
* Mantener botones/acciones admin existentes.

Pruebas:

* Login admin.
* Abrir pedidos admin.
* Abrir facturación.
* Abrir detalle/factura admin.
* Probar impresión o vista previa si aplica.
* Confirmar que cliente/comprobante no cambió.
* Build 0 errores.

Commit sugerido:
Summary:
style(admin): mejorar pedidos y facturacion

Description:
Moderniza visualmente pedidos administrativos y facturacion, manteniendo acciones, rutas, datos e impresion de factura sin cambios funcionales.

BLOQUE F — Clientes, créditos y consultas visual
Archivos permitidos:

* Proyecto_Final/Views/Clients/*
* Proyecto_Final/Views/Credits/*
* Proyecto_Final/Views/Consultations/*
* Proyecto_Final/wwwroot/css/sprint4-frontend-polish.css
* Proyecto_Final/wwwroot/css/admin.css solo si es necesario y limitado.

Objetivo:
Mejorar tablas, estados, formularios y acciones de clientes, créditos y consultas.

Reglas específicas:

* No tocar controladores.
* No tocar servicios.
* No tocar modelos.
* No cambiar cálculos de crédito.
* No cambiar lógica de consultas.
* Mantener formularios y validaciones.

Pruebas:

* Login admin.
* Abrir clientes.
* Abrir créditos.
* Abrir consultas.
* Probar acciones básicas si existen.
* Build 0 errores.

Commit sugerido:
Summary:
style(admin): mejorar clientes creditos y consultas

Description:
Mejora visualmente los modulos administrativos de clientes, creditos y consultas, manteniendo logica, rutas, formularios y validaciones existentes.

BLOQUE G — Empleados, portal empleado, roles y permisos visual
Archivos permitidos:

* Proyecto_Final/Views/Employees/*
* Proyecto_Final/Views/EmployeePortal/*
* Proyecto_Final/Views/SellerOrders/*
* Proyecto_Final/Views/Roles/*
* Proyecto_Final/Views/Permissions/*
* Proyecto_Final/Views/Security/*
* Proyecto_Final/wwwroot/css/sprint4-frontend-polish.css
* Proyecto_Final/wwwroot/css/admin.css solo si es necesario y limitado.

Objetivo:
Mejorar visualmente empleados, portal empleado/vendedor, roles, permisos y seguridad administrativa.

Reglas específicas:

* No tocar EmployeesController.
* No tocar EmployeePortalController.
* No tocar SellerOrdersController.
* No tocar AdminAuthorizeAttribute.
* No tocar SessionAuthorizeAttribute.
* No cambiar permisos ni lógica de autorización.
* No cambiar módulos.
* No cambiar roles.
* No cambiar acciones.
* Mantener [AdminAuthorize("Empleados")] intacto.
* Mantener formularios y AntiForgeryToken.

Pruebas:

* Login admin.
* Abrir empleados.
* Abrir roles/permisos/seguridad si existen.
* Login empleado/vendedor si hay credencial.
* Abrir portal empleado.
* Abrir pedidos vendedor.
* Confirmar que permisos no se rompen.
* Build 0 errores.

Commit sugerido:
Summary:
style(empleados): mejorar vistas de personal y permisos

Description:
Moderniza visualmente empleados, portal empleado, vendedor, roles y permisos, manteniendo autorizacion, modulos, rutas, formularios y logica existente.

BLOQUE H — Corrección de textos raros / mojibake
Objetivo:
Buscar y corregir textos visibles dañados por codificación, por ejemplo:

* Ã
* â‚¡
* ContraseÃ±a
* InformaciÃ³n
* CategorÃ­a
* DirecciÃ³n
* AcciÃ³n
* SesiÃ³n

Reglas:

* Solo corregir textos visibles.
* No cambiar nombres de variables.
* No cambiar nombres de clases.
* No cambiar rutas.
* No cambiar strings que sean claves internas o nombres de módulos/permisos.
* No tocar datos SQL sin aprobación.
* Si hay duda, reportar antes.

Archivos permitidos:

* Views/*.cshtml
* docs/*.md
* CSS solo comentarios/texto si aplica.

Pruebas:

* Build 0 errores.
* Revisar páginas principales.
* Confirmar que no cambió lógica.

Commit sugerido:
Summary:
fix(ui): corregir textos visibles dañados

Description:
Corrige textos visibles con problemas de codificacion en vistas y documentacion, sin modificar logica, rutas, permisos ni datos internos.

BLOQUE I — Corregir advertencias no críticas si es seguro
Advertencias actuales conocidas:

* Models/Perfil.cs CS8618
* Models/Modulo.cs CS8618
* Models/HistorialAuditoria.cs CS8618
* Views/Security/Permisos.cshtml CS8602
* Views/Home/Shop.cshtml CS8602

Objetivo:
Corregir advertencias de nullability solo si es seguro y no cambia lógica.

Reglas:

* No cambiar comportamiento funcional.
* No cambiar base de datos.
* No cambiar controladores.
* En modelos, preferir inicialización segura con string.Empty si aplica.
* En vistas, usar null checks o operadores seguros.
* Si la corrección implica duda lógica, no corregir y reportar.

Pruebas:

* Build 0 errores.
* Idealmente reducir advertencias.
* Confirmar Home/Shop y Security/Permisos cargan.

Commit sugerido:
Summary:
chore: reducir advertencias de nullability

Description:
Reduce advertencias de nullability con inicializaciones y validaciones seguras, sin cambiar comportamiento funcional.

BLOQUE J — Responsive final
Objetivo:
Revisar y ajustar experiencia móvil/tablet/escritorio en todo el sistema.

Archivos permitidos:

* Proyecto_Final/wwwroot/css/sprint4-frontend-polish.css
* Proyecto_Final/wwwroot/css/admin.css solo si es necesario y limitado.
* Vistas específicas solo si hay un problema visual claro y reportado.

Revisar:

* Login
* Registro
* Recuperación
* Mi Perfil
* Home
* Shop
* Detalle producto
* Carrito
* Checkout
* Confirmación
* Mis pedidos
* Detalle pedido
* Comprobante
* Admin dashboard
* Inventario
* Pedidos admin
* Facturación
* Clientes
* Créditos
* Consultas
* Empleados
* Portal empleado
* Roles/permisos

Reglas:

* No cambiar lógica.
* No ocultar acciones.
* No romper impresión.
* No crear CSS global peligroso.
* Usar clases s4- scoped o reglas muy específicas.

Pruebas:

* Desktop.
* Móvil.
* Menú/navegación.
* Tablas.
* Botones.
* Formularios.
* Build 0 errores.

Commit sugerido:
Summary:
style(ui): ajustar responsive general

Description:
Ajusta estilos responsive generales para mejorar vistas de cliente, tienda, administracion y empleados sin modificar logica ni rutas.

BLOQUE K — Documentación final
Archivos sugeridos:

* docs/resumen-final-proyecto.md
* docs/frontend-sprint4.md
* docs/credenciales-demo.md
* docs/checklist-qa-final.md

Objetivo:
Documentar el estado final del proyecto, módulos completados, credenciales demo, scripts SQL requeridos, pruebas realizadas y pasos de ejecución.

Debe incluir:

* Descripción del proyecto.
* Módulos implementados.
* Roles y credenciales demo conocidas.
* Scripts SQL importantes.
* Funcionalidades cliente.
* Funcionalidades admin.
* Funcionalidades empleado/vendedor.
* Seguridad aplicada.
* Frontend Sprint 4.
* Checklist de pruebas.
* Archivos que no deben subirse.
* Cómo ejecutar build.
* Cómo ejecutar proyecto.

Reglas:

* No inventar credenciales si no están confirmadas.
* Marcar como “verificar en BD local” si hay duda.
* No incluir secretos.
* No incluir appsettings.
* No incluir cadenas reales de conexión.

Commit sugerido:
Summary:
docs: agregar documentacion final del proyecto

Description:
Agrega documentacion final con resumen de modulos, mejoras visuales, seguridad, credenciales demo, scripts SQL y checklist QA.

BLOQUE L — QA final completo
No necesariamente editar archivos.
Objetivo:
Ejecutar validación final del sistema y reportar resultados.

Checklist cliente:

* Login cliente.
* Home.
* Tienda.
* Detalle producto.
* Agregar carrito.
* Carrito.
* Checkout.
* Provincia/cantón/distrito.
* Confirmación.
* Mis pedidos.
* Detalle pedido.
* Cancelación si aplica.
* Comprobante.
* Impresión comprobante.
* Mi Perfil.
* Logout.

Checklist admin:

* Login admin.
* Dashboard.
* Inventario.
* Crear/editar producto si aplica.
* Pedidos admin.
* Facturación admin.
* Factura admin.
* Clientes.
* Créditos.
* Consultas.
* Empleados.
* Roles/permisos/seguridad.
* Logout.

Checklist empleado/vendedor:

* Login empleado/vendedor si hay credenciales.
* Portal empleado.
* Pedidos vendedor.
* Acciones permitidas.
* Accesos bloqueados.

Checklist técnico:

* dotnet build Trilogia-Cursos\Proyecto_Final.slnx
* git status limpio.
* Sin appsettings modificados.
* Sin bin/ obj/ .vs/ ZIPs.
* Sin errores en consola en pantallas principales.
* Sin 404 de scripts principales.
* Revisar impresión comprobante.
* Revisar responsive básico.

Entrega final:
Al terminar TODOS los bloques, reportá:

1. Rama actual.
2. Commits creados.
3. Archivos modificados por bloque.
4. Resultado final de build.
5. Advertencias restantes.
6. Pruebas realizadas.
7. Pruebas pendientes si alguna no se pudo hacer.
8. Confirmación de appsettings limpios.
9. Confirmación de no bin/obj/.vs/ZIPs.
10. Recomendación para PR final:
    final/cierre-visual-qa -> main

Criterio de parada:
Si en cualquier bloque aparece:

* build roto,
* login roto,
* checkout roto,
* permisos rotos,
* impresión rota,
* conflicto fuerte,
* archivo sensible modificado,
* duda sobre lógica,
  detenete y reportá antes de seguir.

No hagás merge a main.
No hagás push a main.
Al final solo dejá la rama final/cierre-visual-qa lista para PR.
