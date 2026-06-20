# Endpoints de Proyecto_FinalAPI

## Descripcion general

`Proyecto_FinalAPI` es el proyecto API del sistema DistribuidoraJJ / Licorera La Bodega. Actualmente expone endpoints para autenticacion basica y consulta publica del catalogo de productos.

El API usa controladores ASP.NET Core y servicios propios que consultan procedimientos almacenados existentes. Los endpoints de productos son publicos y de solo lectura. No existen todavia endpoints protegidos para pedidos, facturacion, inventario administrativo, clientes, creditos, roles o permisos.

## Nota de seguridad sobre autenticacion API

Actualmente el login del API devuelve datos del usuario autenticado, pero no emite JWT, API key, cookie de sesion compartida ni token de autorizacion.

Por esa razon, no deben exponerse endpoints protegidos nuevos hasta disenar e implementar una estrategia formal de autenticacion y autorizacion API. Cualquier endpoint futuro que maneje pedidos, facturacion, inventario administrativo o datos sensibles debe validar identidad, rol y permisos antes de devolver o modificar informacion.

## Autenticacion

### POST /api/auth/login

**Descripcion:** valida credenciales de usuario y devuelve datos basicos si el inicio de sesion es correcto.

**Parametros:** body JSON.

```json
{
  "email": "admin@distribuidorajj.com",
  "password": "1234"
}
```

**Respuesta 200:**

```json
{
  "success": true,
  "message": "Inicio de sesion correcto.",
  "userId": 1,
  "fullName": "Administrador",
  "email": "admin@distribuidorajj.com",
  "role": "Administrador"
}
```

**Codigos esperados:**

- `200`: credenciales validas.
- `400`: correo o contrasena no enviados.
- `401`: credenciales invalidas.
- `429`: demasiados intentos fallidos.
- `500`: error interno no esperado.

**Notas de seguridad:** no devuelve token de autorizacion. Los datos devueltos no deben usarse por si solos como prueba de autorizacion para endpoints protegidos futuros.

### POST /api/auth/register

**Descripcion:** registra una cuenta cliente usando nombre, correo y contrasena.

**Parametros:** body JSON.

```json
{
  "fullName": "Cliente Demo",
  "email": "cliente.demo@correo.com",
  "password": "1234"
}
```

**Respuesta 200:**

```json
{
  "success": true,
  "message": "Cuenta creada correctamente.",
  "userId": null,
  "fullName": null,
  "email": null,
  "role": null
}
```

**Codigos esperados:**

- `200`: cuenta creada.
- `400`: datos obligatorios incompletos.
- `409`: correo ya registrado.
- `500`: error interno no esperado.

**Notas de seguridad:** no debe revelar informacion tecnica ni detalles internos de base de datos.

### POST /api/auth/forgot-password

**Descripcion:** inicia el flujo de recuperacion de contrasena con mensajes seguros para evitar enumeracion de usuarios.

**Parametros:** body JSON.

```json
{
  "email": "cliente.demo@correo.com"
}
```

**Respuesta 200:**

```json
{
  "success": true,
  "message": "Si la solicitud es valida, se procesara la recuperacion de contrasena.",
  "userId": null,
  "fullName": null,
  "email": null,
  "role": null
}
```

**Codigos esperados:**

- `200`: solicitud procesada con mensaje generico.
- `400`: correo no enviado.
- `429`: limite de intentos alcanzado.
- `500`: error interno no esperado.

**Notas de seguridad:** mantiene respuesta generica aunque el correo no exista. No debe revelar si una cuenta esta registrada.

### POST /api/auth/reset-password

**Descripcion:** restablece la contrasena usando un token valido de recuperacion.

**Parametros:** body JSON.

```json
{
  "token": "token-de-recuperacion",
  "newPassword": "NuevaClave123"
}
```

**Respuesta 200:**

```json
{
  "success": true,
  "message": "La contrasena se actualizo correctamente.",
  "userId": null,
  "fullName": null,
  "email": null,
  "role": null
}
```

**Codigos esperados:**

- `200`: contrasena actualizada.
- `400`: solicitud invalida, token invalido o token vencido.
- `429`: limite de intentos alcanzado.
- `500`: error interno no esperado.

**Notas de seguridad:** debe mantenerse el limite de intentos y no mostrar errores tecnicos.

## Productos publicos

### GET /api/products

**Descripcion:** devuelve el catalogo publico de productos activos.

**Parametros:** ninguno.

**Ejemplo de request:**

```http
GET /api/products
```

**Respuesta 200:**

```json
[
  {
    "productoId": 1,
    "nombre": "Whisky Premium",
    "categoria": "Whisky",
    "descripcion": "Producto de catalogo",
    "precio": 18500.00,
    "stock": 12,
    "imagenUrl": "~/img/whisky-premium.webp",
    "esDestacado": true,
    "estadoStock": "Disponible"
  }
]
```

**Codigos esperados:**

- `200`: listado cargado correctamente.
- `500`: error interno al cargar catalogo.

**Notas de seguridad:** endpoint publico y de solo lectura. No expone costos internos, proveedores ni datos administrativos.

### GET /api/products?categoria=

**Descripcion:** devuelve productos activos filtrados por categoria.

**Parametros:**

- `categoria`: nombre de categoria.

**Ejemplo de request:**

```http
GET /api/products?categoria=Whisky
```

**Respuesta 200:**

```json
[
  {
    "productoId": 1,
    "nombre": "Whisky Premium",
    "categoria": "Whisky",
    "descripcion": "Producto de catalogo",
    "precio": 18500.00,
    "stock": 12,
    "imagenUrl": "~/img/whisky-premium.webp",
    "esDestacado": true,
    "estadoStock": "Disponible"
  }
]
```

**Codigos esperados:**

- `200`: listado filtrado cargado correctamente.
- `500`: error interno al cargar catalogo.

**Notas de seguridad:** usa parametros y procedimientos almacenados. No permite escritura.

### GET /api/products?buscar=

**Descripcion:** devuelve productos activos filtrados por texto de busqueda.

**Parametros:**

- `buscar`: texto a buscar en nombre, categoria o descripcion.

**Ejemplo de request:**

```http
GET /api/products?buscar=whisky
```

**Respuesta 200:**

```json
[
  {
    "productoId": 1,
    "nombre": "Whisky Premium",
    "categoria": "Whisky",
    "descripcion": "Producto de catalogo",
    "precio": 18500.00,
    "stock": 12,
    "imagenUrl": "~/img/whisky-premium.webp",
    "esDestacado": true,
    "estadoStock": "Disponible"
  }
]
```

**Codigos esperados:**

- `200`: listado filtrado cargado correctamente.
- `500`: error interno al cargar catalogo.

**Notas de seguridad:** endpoint publico y parametrizado. No expone informacion sensible.

### GET /api/products/{id}

**Descripcion:** devuelve el detalle publico de un producto activo.

**Parametros:**

- `id`: identificador numerico del producto.

**Ejemplo de request:**

```http
GET /api/products/1
```

**Respuesta 200:**

```json
{
  "productoId": 1,
  "nombre": "Whisky Premium",
  "categoria": "Whisky",
  "descripcion": "Producto de catalogo",
  "precio": 18500.00,
  "stock": 12,
  "imagenUrl": "~/img/whisky-premium.webp",
  "esDestacado": true,
  "estadoStock": "Disponible"
}
```

**Respuesta 404:**

```json
{
  "message": "Producto no encontrado."
}
```

**Respuesta 400:**

```json
{
  "message": "El identificador del producto debe ser mayor a cero."
}
```

**Codigos esperados:**

- `200`: producto encontrado.
- `400`: `id` menor o igual a cero.
- `404`: producto inexistente o no activo.
- `500`: error interno al cargar producto.

**Notas de seguridad:** recibe `ProductoId`, no permite modificar inventario y no expone datos administrativos.

### GET /api/products/categories

**Descripcion:** devuelve categorias activas disponibles en tienda.

**Parametros:** ninguno.

**Ejemplo de request:**

```http
GET /api/products/categories
```

**Respuesta 200:**

```json
[
  "Whisky",
  "Ron",
  "Vino"
]
```

**Codigos esperados:**

- `200`: categorias cargadas correctamente.
- `500`: error interno al cargar categorias.

**Notas de seguridad:** endpoint publico y de solo lectura.

### GET /api/products/featured?take=

**Descripcion:** devuelve productos destacados del catalogo publico.

**Parametros:**

- `take`: cantidad maxima de productos a devolver. Si no se indica, usa `8`. Si es mayor a `24`, el API lo limita a `24`.

**Ejemplo de request:**

```http
GET /api/products/featured?take=4
```

**Respuesta 200:**

```json
[
  {
    "productoId": 1,
    "nombre": "Whisky Premium",
    "categoria": "Whisky",
    "descripcion": "Producto de catalogo",
    "precio": 18500.00,
    "stock": 12,
    "imagenUrl": "~/img/whisky-premium.webp",
    "esDestacado": true,
    "estadoStock": "Disponible"
  }
]
```

**Respuesta 400:**

```json
{
  "message": "La cantidad solicitada debe ser mayor a cero."
}
```

**Codigos esperados:**

- `200`: productos destacados cargados correctamente.
- `400`: `take` menor o igual a cero.
- `500`: error interno al cargar destacados.

**Notas de seguridad:** endpoint publico y de solo lectura. El parametro `take` se limita a un maximo de `24` para evitar respuestas excesivas.

## Pendiente para versiones futuras

Los siguientes modulos no deben exponerse por API hasta implementar autenticacion y autorizacion API:

- Pedidos de cliente.
- Pedidos admin.
- Inventario admin.
- Facturacion.
- Clientes y creditos.
- Roles y permisos.

Antes de agregar esos endpoints se debe definir una estrategia formal, por ejemplo JWT o un mecanismo equivalente, con validacion de identidad, rol, permisos y propiedad de datos cuando aplique.

## Pruebas recomendadas

- Ejecutar `dotnet build Trilogia-Cursos\Proyecto_Final.slnx`.
- Probar `GET /api/products`.
- Probar filtros `categoria` y `buscar`.
- Probar `GET /api/products/{id}` con producto existente.
- Probar `GET /api/products/0` y confirmar `400`.
- Probar `GET /api/products/-1` y confirmar `400`.
- Probar `GET /api/products/{id}` con producto inexistente y confirmar `404`.
- Probar `GET /api/products/categories`.
- Probar `GET /api/products/featured?take=4`.
- Probar `GET /api/products/featured?take=0` y confirmar `400`.
- Probar `GET /api/products/featured?take=-1` y confirmar `400`.
- Probar `GET /api/products/featured?take=100` y confirmar maximo 24 resultados.
- Confirmar que endpoints protegidos futuros no se agregaron en este bloque.
