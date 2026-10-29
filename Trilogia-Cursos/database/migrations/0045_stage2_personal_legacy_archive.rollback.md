# 0045 — Compensación sin pérdida de historia

Antes de instalar, respaldar con CHECKSUM, verificar y restaurar en un destino separado.
La instalación de módulos y ledger es transaccional; cualquier error revierte ese conjunto.
No modifica conversaciones, mensajes, propietarios ni recibos existentes; tampoco concede permisos.

Después de instalar, ante un defecto detener el host, deshabilitar el acceso al archivo y
revocar exclusivamente los dos grants por objeto del Reader que se hayan aprovisionado.
Mantener el cierre de escritores legacy y conservar los módulos, ledger e historia para diagnóstico.
No reabrir el escritor antiguo, borrar el ledger ni restaurar sobre principal automáticamente.
Una restauración que sobrescriba datos necesita confirmación específica y preservación de actividad posterior.
