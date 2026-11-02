# Rollback de 0251

## Qué introduce

- Solo inserta filas en `dbo.PerfilPermisos`: la matriz que la 0028 preveía para
  los perfiles de oficina (Supervisor, Cajero, Bodeguero, Facturador, Crédito y
  Cobro, Compras, Soporte, Auditor Interno) y `MOVIL_ACCESO` de la 0025 para
  Bodeguero. Esos perfiles se crean después de las migraciones (datos demo) y
  habían quedado sin esas asignaciones.
- Si la 0260 ya estaba aplicada, también aplica su regla de conservación a los
  perfiles a los que acaba de dar un permiso del módulo Finanzas: hoy, Cajero
  recibe `FINANZAS_INGRESOS_EGRESOS_VER` por `LIQUIDACION_FINANCIERA`. Así el
  resultado es el mismo que en el orden numérico (0251 y luego 0260), donde ese
  permiso lo asigna la propia 0260.
- Cada fila nueva queda marcada con `UsuarioAsignacionNombre` que empieza por
  `Migración 0251` (la de la regla de Finanzas, `Migración 0251 (regla 0260)`).
  No crea permisos, no cambia procedimientos ni quita asignaciones existentes.

## Revertir

Antes de borrar, revisar con el responsable de seguridad que nadie haya
reasignado a mano alguno de esos permisos después de aplicar la migración.
Luego, en una transacción:

```sql
BEGIN TRANSACTION;
DELETE FROM dbo.PerfilPermisos WHERE UsuarioAsignacionNombre LIKE N'Migración 0251%';
DELETE FROM dbo.SchemaMigrationHistory WHERE MigrationId = N'0251_perfiles_oficina_permisos';
COMMIT TRANSACTION;
```

Si la 0260 se aplicó después de la 0251, el `FINANZAS_INGRESOS_EGRESOS_VER` de
Cajero quedó marcado como `Migración 0260` y este borrado no lo quita. Cajero
conservaría «Ingresos y egresos» sin «Liquidación de cobros»: decidir con el
responsable de seguridad si se quita también:

```sql
DELETE pp FROM dbo.PerfilPermisos pp
JOIN dbo.Perfiles pf ON pf.PerfilId = pp.PerfilId
JOIN dbo.Permisos p ON p.PermisoId = pp.PermisoId
WHERE pf.Nombre = N'Cajero' AND p.Codigo = N'FINANZAS_INGRESOS_EGRESOS_VER'
  AND pp.UsuarioAsignacionNombre = N'Migración 0260';
```

Efecto: esos perfiles vuelven a no ver sus módulos en el menú web (el menú
muestra cada opción según el permiso de la pantalla) y pierden el acceso a la
aplicación móvil. El código de la aplicación no depende de la migración.
