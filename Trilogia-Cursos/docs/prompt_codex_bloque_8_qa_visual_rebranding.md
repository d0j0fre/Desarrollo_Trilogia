# Prompt Codex — Bloque 8: QA visual general y cierre del rebranding

## Objetivo

Quiero continuar con el **Bloque 8** del rebranding visual del proyecto:

```text
Trilogia-Cursos / Proyecto_Final
```

Este bloque es de **QA visual general y limpieza final**.

No quiero cambios grandes.  
Solo se permiten ajustes visuales pequeños si se detecta algo roto, ilegible o inconsistente.

---

# Contexto

Ya se completaron y commitearon los bloques de rebranding visual:

1. Layout compartido, assets de marca y favicon.
2. Login y Registro.
3. Home, Shop y Detail.
4. Carrito, Checkout y Confirmación.
5. Portal cliente, comprobantes y facturación.
6. Backoffice:
   - Admin
   - Inventory
   - OrdersAdmin
   - SellerOrders
   - EmployeePortal
   - Roles
   - Permissions

---

# Marca

Nombre visible:

```text
Supermercado Mayoreo
```

Subtítulo:

```text
Licorera - Distribuidora
```

---

# Paleta oficial

```text
Rojo principal: #8B0E16
Rojo secundario: #C41E26
Dorado principal: #D4AF37
Dorado claro: #F2D77A
Negro carbón: #1F1F1F
Blanco: #FFFFFF
```

---

# Reglas obligatorias

Antes de tocar archivos:

1. Confirmar rama actual.
2. Confirmar `git status`.
3. Revisar primero.
4. No implementar cambios sin explicar qué problema se encontró.

No tocar:

```text
main
Danny
appsettings.json
appsettings.Development.json
SQL
database/
controladores
servicios
modelos
rutas
lógica de negocio
permisos
sesiones
facturación
inventario
pedidos
roles
autenticación
checkout
procedimientos almacenados
```

No hacer:

```text
push
merge
Pull Request
rediseños grandes
renombres técnicos
cambios masivos
```

No incluir en commit:

```text
prompts .md no relacionados
ZIPs
bin/
obj/
.vs/
archivos generados
```

---

# Rutas a revisar

Revisar estas rutas por HTTP o mediante redirección esperada a Login cuando sean protegidas:

```text
/
/Home
/Home/Shop
/Home/Detail/1
/Account/Login
/Account/Register
/Account/Registro
/Cart
/Cart/Checkout
/ClientPortal
/Billing
/Admin
/Inventory
/Inventory/Movements
/OrdersAdmin
/SellerOrders
/EmployeePortal
/Roles
/Permissions
```

Si `/Home/Detail/1` no existe, usar un producto real disponible.

Si `/Account/Register` no existe y la ruta real es `/Account/Registro`, validar la ruta real.

Las rutas protegidas pueden redirigir a Login y terminar en HTTP 200. Eso es correcto si ya era el comportamiento esperado.

---

# Assets a revisar

Validar que respondan HTTP 200:

```text
/brand/logo-icon.png
/brand/logo-navbar.png
/brand/favicon.ico
/brand/favicon-32x32.png
/brand/apple-touch-icon.png
/css/brand-system.css
/css/brand-overrides.css
```

---

# Revisión visual esperada

Verificar:

1. Favicon cargando correctamente.
2. Logo visible y proporcionado.
3. Textos de marca coherentes.
4. No deben quedar textos viejos como:
   - `Licorera La Bodega`
   - `LB` como marca principal
   - colores negro/dorado viejos cuando contradigan la nueva marca
5. Botones principales visibles.
6. Tablas legibles.
7. Cards alineadas.
8. Formularios sin campos cortados.
9. Badges con buen contraste.
10. Dropdowns legibles.
11. Responsive básico conservado.
12. Páginas imprimibles conservan impresión funcional.
13. No hay assets 404.
14. No hay CSS roto.
15. No hay errores Razor.

---

# Validaciones funcionales que NO deben romperse

Verificar que se conserven:

```text
formularios POST
antiforgery
asp-controller
asp-action
asp-route
asp-for
IDs funcionales
name de inputs
checkout.js
validaciones
enlaces Ver
enlaces Editar
botones Cancelar
botones Comprobante
botones Imprimir
botones Generar factura
permisos
redirecciones a Login
```

---

# Si se encuentran problemas

Corregir solo detalles visuales pequeños.

Preferir centralizar en:

```text
Proyecto_Final/wwwroot/css/brand-overrides.css
```

Editar vistas solo si es estrictamente necesario.

No cambiar lógica.

---

# Build obligatorio

Ejecutar:

```bash
dotnet build Trilogia-Cursos\Proyecto_Final.slnx
```

Resultado esperado:

```text
0 errores
0 warnings
```

---

# Git status final

Confirmar que solo aparezcan archivos esperados.

No deben aparecer:

```text
ZIPs
bin/
obj/
.vs/
appsettings.json
appsettings.Development.json
database/
controladores
servicios
modelos
archivos generados
prompts .md no relacionados
```

---

# Salida final esperada

Al terminar, entregar:

1. Rama actual.
2. Git status inicial.
3. Checklist de rutas revisadas.
4. Assets revisados.
5. Problemas encontrados.
6. Correcciones realizadas, si hubo.
7. Archivos modificados.
8. Validación HTTP.
9. Validación de contratos:
   - formularios
   - antiforgery
   - rutas
   - botones
10. Resultado de build.
11. Git status final.
12. Commit sugerido.

---

# Commit sugerido si hubo cambios

```text
Summary:
chore(brand): cerrar qa visual del rebranding

Description:
Realiza ajustes menores finales del rebranding visual de Supermercado Mayoreo / Licorera - Distribuidora.

Valida layout, autenticación, tienda, carrito, checkout, portal cliente, facturación y backoffice.

Conserva rutas, formularios, antiforgery, permisos, controladores, servicios, modelos, SQL, appsettings y lógica de negocio sin modificaciones.

Validación:
- Rutas principales revisadas por HTTP o redirección esperada a Login.
- Assets de marca responden HTTP 200.
- CSS responde HTTP 200.
- Formularios, enlaces, botones y antiforgery se mantienen intactos.
- Build 0 errores / 0 warnings.
- Solo se modificaron archivos visuales esperados.
```

---

# Si no hubo cambios

No crear commit.

Entregar reporte final indicando:

```text
El rebranding visual queda validado sin modificaciones adicionales.
Build 0 errores / 0 warnings.
Git status limpio o solo con archivos no trackeados fuera de alcance.
```
