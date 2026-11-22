# Reversión de 0327

Aditiva y sin tablas ni datos nuevos.

1. Quitar los avisos:
   - `DROP TRIGGER dbo.TR_Reclamos_NotificarCierre;`
   - `DROP TRIGGER dbo.TR_MetasVendedor_NotificarVendedor;`
   - `DROP TRIGGER dbo.TR_ChatDepartamentoMiembros_NotificarMiembro;`
2. Marcar la fila `0327_avisos_reclamo_meta_departamento` como `RolledBack` en `dbo.SchemaMigrationHistory`.

Los avisos ya creados quedan en `dbo.NotificacionesUsuario` (tipos `ReclamoCerrado`, `MetaAsignada` y `ChatDepartamento`); no estorban. Restaurar un BACPAC es el último recurso.
