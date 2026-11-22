# Reversión de 0336

Aditiva: una función y un procedimiento nuevos; no cambia tablas ni datos (`Facturas.Estado` no se toca).

1. `DROP PROCEDURE dbo.sp_Facturas_Saldo; DROP FUNCTION dbo.fn_Cliente_FacturasSaldo;`
2. Marcar la fila `0336_facturas_saldo_pagada` como `RolledBack` en `dbo.SchemaMigrationHistory`.

El sitio web publicado con esta rama consulta `sp_Facturas_Saldo` dentro de un `try/catch`: si el procedimiento no existe, las facturas se siguen mostrando como «Generada» (como antes) y no se rompe ninguna pantalla.

Restaurar un BACPAC es el último recurso.
