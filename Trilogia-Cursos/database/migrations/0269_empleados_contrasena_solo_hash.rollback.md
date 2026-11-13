# Reversión de 0269

Restaura la firma anterior (con `@Contrasena` en texto, que el disparador `tr_Usuarios_SinTextoPlano` de 0310 convierte al instante). Solo sirve si se despliega a la vez el MVC anterior, que envía `@Contrasena`:

- `sp_Admin_CreateEmployee`: parámetro `@Contrasena NVARCHAR(255)` en lugar de `@ContrasenaHash`, e `INSERT dbo.Usuarios (PerfilId, NombreCompleto, Correo, Contrasena, Telefono, Direccion, Activo)` con `@Contrasena`.
- `sp_RRHH_UpdateEmployee`: parámetro `@Contrasena NVARCHAR(255)=NULL` y `Contrasena = CASE WHEN NULLIF(LTRIM(RTRIM(@Contrasena)),N'') IS NULL THEN Contrasena ELSE @Contrasena END` en el `UPDATE` de `dbo.Usuarios`.

La definición completa anterior está en `0017_rrhh_employee_records.sql` (`sp_RRHH_UpdateEmployee`) y en `cu080_empleados_gestion_patch.sql` (`sp_Admin_CreateEmployee`).

No cambia tablas ni datos: las cuentas creadas con 0269 conservan su hash y siguen iniciando sesión. No se recupera ninguna contraseña en texto. Marcar la fila `0269_empleados_contrasena_solo_hash` como `RolledBack` en `dbo.SchemaMigrationHistory`.
