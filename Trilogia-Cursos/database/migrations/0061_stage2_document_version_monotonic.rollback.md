# Reversión operativa de publicación documental monótona

Deshabilitar nuevas publicaciones y el flujo de reconciliación antes de retirar
los binarios que acreditan 0061. Conservar documentos, versiones Ready/Pending,
archivos, auditoría y el ledger Applied. No reinstalar 0055 ni 0056, no retroceder
punteros ni convertir Ready a Pending para reproducir una operación.

0061 altera solamente `DocumentSecurity.Document_MarkVersionReady`, conserva su
firma y grants. Reemplazarlo posteriormente requiere una migración nueva revisada.
El rollback de aplicación no debe activar un escritor anterior que pueda retroceder
la versión actual. No hay compensación automática de un archivo ya publicado.

Un documento cuyo puntero actual ya sea inconsistente con versiones Ready más
nuevas queda bloqueado. Conciliar ese histórico exige evidencia y una decisión
separada; esta migración no reescribe filas ni intenta repararlo por inferencia.

Antes de instalar: verificar hashes 0056/0060, huellas previas, respaldo CHECKSUM,
VERIFYONLY y restauración separada. Ensayar v3 antes de v2, orden normal,
reintento Ready actual/histórico, creador ajeno, departamento revocado, transacción
ausente, documento/versiones inconsistentes y concurrencia. Confirmar que cada
Pending cambia una vez y agrega una sola auditoría; los retries no cambian el
puntero, timestamp ni auditoría. No restaura sobre principal ni elimina evidencias.
