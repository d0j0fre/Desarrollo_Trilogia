# Reversión de 0260

Revisar y registrar primero las asignaciones actuales: la migración agrega permisos sin identificar cuáles existían antes, por lo que no se deben eliminar asignaciones de forma automática. Si se decide revertir la protección, eliminar `dbo.tr_Perfiles_ImpedirInactivarConUsuarios` mediante un script de reversión aprobado y registrar el cambio en el ledger. Evaluar por separado las asignaciones y el permiso de Finanzas con el responsable de seguridad. No borrar datos de usuarios ni permisos que se estén utilizando.
