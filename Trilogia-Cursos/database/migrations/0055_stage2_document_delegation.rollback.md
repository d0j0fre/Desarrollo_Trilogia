# Compensación de la delegación documental

Antes de instalar en principal: verificar identidad física, ledger, SHA256 exacto y prerrequisitos; conservar respaldo COPY_ONLY CHECKSUM/VERIFYONLY y evidencia de restauración separada. Ensayar primero en copia acreditada.

La migración es aditiva. No concede permisos a perfiles ni relaciones a personas. El aprovisionamiento separado habilita solamente los nuevos procedimientos públicos y metadata de verificación para APP; retira sus EXECUTE de los lectores/escritores documentales anteriores.

Ante un fallo, deshabilitar Documents:DelegationEnabled en la configuración protegida y reiniciar ordenadamente. El módulo queda indisponible (503), sin fallback a procedimientos que confían únicamente en @Actor. No conceder owner, permisos amplios ni restaurar grants antiguos para superar un gate.

Revocar autorizaciones mediante SetGrant con login original vigente, motivo y revisión conocida. Conservar tabla de grants, auditoría inmutable, documentos, versiones, archivos y ledger. No purgar ni restaurar sobre principal. Una reversión SQL requiere una migración aditiva revisada contra las huellas instaladas.
