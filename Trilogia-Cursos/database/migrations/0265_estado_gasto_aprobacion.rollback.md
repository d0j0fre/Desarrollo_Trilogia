# Reversión de 0265

No eliminar el nuevo estado mientras existan gastos que lo usen. Primero reconciliar con Finanzas los gastos en `Requiere Aprobación` y, mediante una migración revisada, convertirlos a `Registrado`. Restaurar después las definiciones de los procedimientos y la vista anteriores a 0265, y por último restablecer la restricción de estados. Conservar auditoría y registrar la reversión en el ledger.
