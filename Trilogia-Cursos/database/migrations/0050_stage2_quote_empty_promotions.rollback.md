# Compensación de 0050

Depende de los bytes y del módulo originales acreditados de 0049. Mantener su
archivo/ledger inmutables. La corrección convierte explícitamente el resultado
vacío de FOR JSON en `[]` antes de JSON_QUERY y marca el snapshot como versión2.
El motor y la fórmula monetaria se conservan; un catálogo ausente no se considera
una lista vacía desde C#. El adaptador exige arrays y versión acreditados.

No reescribir propietarios/snapshots anteriores, ni renovarlos con el mismo UUID.
Si un intento quedó con fuente incompleta, conservarlo y pedir una intención nueva.
Ante un fallo, mantener desactivada la autoridad, retirar solo su configuración
y conservar ledger, propietarios, evidencia y respaldo. No restaurar principal
automáticamente ni habilitar la ruta antigua como fallback.
