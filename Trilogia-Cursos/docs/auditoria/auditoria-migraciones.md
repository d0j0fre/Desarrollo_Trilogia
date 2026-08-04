# Auditoría de migraciones 0013-0016

## Inventario

| Migración | Historias | Ledger/SHA | Transacción | Verify | Segunda ejecución | Rollback | Evidencia actual |
|---|---|---|---|---|---|---|---|
| 0013 | CU-101-104 | Sí | `XACT_ABORT` y transacción | Sí | Rechazada por contrato | Compensatorio documentado | Limpia, legado y flujo funcional documentados |
| 0014 | CU-084 | Sí | `XACT_ABORT` y transacción | Sí | Rechazada | Compensatorio documentado | Secuencia local documentada |
| 0015 | CU-132/134 | Sí | `XACT_ABORT` y transacción | Sí | Rechazada | Compensatorio documentado | Procedimientos invocados localmente |
| 0016 | CU-262 | Sí | `XACT_ABORT` y transacción | Sí | Rechazada | Compensatorio documentado | Venta cruzada invocada localmente |

0013 valida dependencias y contratos legados, preserva datos y registra auditoría. 0014-0016 son aditivas. Todas exigen un SHA hexadecimal de 64 caracteres y registran `SchemaMigrationHistory`.

## Hallazgos

- La suite ScriptDom actual pasa: 94 archivos, 951 lotes, 0 errores.
- La documentación existente registra 0013 sobre esquema mínimo y legado, y la secuencia 0013-0016 en LocalDB desechable.
- Compatibilidad limpia/legada individual de 0014-0016: no especificado.
- `DBCC CHECKDB` para la secuencia 0013-0016 en esta ejecución: no especificado.
- Los rollback son planes compensatorios, no scripts destructivos. Esto es correcto para objetos que pueden contener datos o dependencias.
- #116 reutiliza 0007-0012; esas migraciones no se deben incorporar ni renumerar mecánicamente.

## Harness propuesto tras Puerta A

1. Crear base LocalDB desechable mínima.
2. Crear copia con esquema legado compatible y datos sintéticos.
3. Calcular SHA-256 real sin alterar registros existentes.
4. Ejecutar 0013-0016 en orden con `sqlcmd -b`.
5. Ejecutar cada `verify.sql`.
6. Confirmar rechazo de segunda ejecución.
7. Probar recepciones, discrepancias, reportes y venta cruzada con datos suficientes/insuficientes.
8. Ejecutar `DBCC CHECKDB` si LocalDB lo permite.
9. Destruir únicamente las bases desechables creadas por el harness.

No se ejecutó SQL contra Azure ni una base compartida. No se modificaron hashes ni ledger.
