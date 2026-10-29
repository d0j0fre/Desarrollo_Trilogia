# Candidatos de laboratorio: referencia para integración

Estos archivos son copias textuales para revisión, no migraciones oficiales ni componentes registrados. No ejecutarlos en una base compartida, no copiar sus marcadores de laboratorio y no interpretar estas fuentes como un permiso para habilitar funcionalidades.

- `chat-identidad`, `chat-recursos`, `chat-mensajes`, `chat-persistencia` y `chat-composicion` reflejan el trabajo D/C ensayado en una copia SQL. El registro común de UUID añade un trigger a Chat P y requiere reconciliar su manifiesto, privilegios, cortes y regresión. Las pruebas realizadas y sus límites figuran en el informe principal.
- `boletas-notificacion` y `boletas-verificacion` son un candidato ensayado exclusivamente con datos ficticios. Faltan migración oficial, integración HTTP, SMTP, manifiestos de permisos y corte del emisor anterior. Los marcadores son propios de laboratorio.
- `alertas-documentos` es un núcleo preparatorio con pruebas aisladas; no hay integración durable SQL/HTTP certificada.

Los nombres de las copias terminan en `.txt` para evitar su ejecución accidental. El material no incluye datos reales, credenciales, respaldos, logs privados ni claves. Las evidencias sensibles originales permanecen en el entorno de desarrollo.
