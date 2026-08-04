# Porcentajes por módulo y escenario

## Método

Cada historia vale 100 puntos: criterios/trazabilidad 10, funcionalidad vertical 35, pruebas 25, DB/migración/seguridad/permisos 15, QA 10 y documentación 5. Se normalizaron 92 historias únicas y se usó peso igual porque las estimaciones son no especificadas.

Los escenarios `Auto` y `QA/Azure` son proyecciones conservadoras por fila de la matriz; no demuestran ejecución futura.

| Módulo | HU | Main | #117 | Auto | QA/Azure |
|---|---:|---:|---:|---:|---:|
| Activos | 4 | 60,00% | 60,00% | 65,00% | 75,00% |
| Asistente | 3 | 40,00% | 68,33% | 74,00% | 81,67% |
| Autenticación | 4 | 45,75% | 45,75% | 51,00% | 57,25% |
| Chat | 3 | 80,00% | 80,00% | 85,00% | 95,00% |
| Clientes y crédito | 3 | 37,00% | 37,00% | 42,00% | 47,00% |
| Compras | 6 | 20,00% | 76,67% | 83,00% | 88,33% |
| Consultas | 3 | 37,00% | 37,00% | 42,00% | 47,00% |
| Devoluciones y garantías | 4 | 65,00% | 65,00% | 70,00% | 80,00% |
| Documentos | 2 | 0,00% | 80,00% | 85,00% | 95,00% |
| Facturación y finanzas | 4 | 37,00% | 37,00% | 42,00% | 47,00% |
| Flotilla | 4 | 60,00% | 60,00% | 65,00% | 75,00% |
| Inteligencia de inventario | 3 | 0,00% | 85,00% | 92,00% | 95,00% |
| Inventario | 6 | 37,00% | 37,00% | 42,00% | 47,00% |
| Inventario avanzado | 2 | 0,00% | 85,00% | 92,00% | 95,00% |
| KPIs | 3 | 60,00% | 60,00% | 65,00% | 75,00% |
| Logística | 4 | 50,00% | 71,25% | 76,75% | 85,00% |
| Perfil | 3 | 37,00% | 37,00% | 42,00% | 47,00% |
| Portal cliente | 4 | 37,00% | 37,00% | 42,00% | 47,00% |
| Presupuestos y gastos | 3 | 0,00% | 80,00% | 85,00% | 95,00% |
| Promociones | 4 | 75,00% | 75,00% | 80,00% | 90,00% |
| Reclamos | 2 | 60,00% | 60,00% | 65,00% | 75,00% |
| Reportes | 4 | 24,25% | 66,75% | 72,75% | 78,00% |
| RRHH y planilla | 4 | 17,50% | 17,50% | 74,50% | 84,50% |
| Rutas inteligentes | 3 | 60,00% | 60,00% | 65,00% | 75,00% |
| Seguridad | 3 | 37,00% | 37,00% | 42,00% | 47,00% |
| Ventas móviles | 4 | 37,00% | 37,00% | 42,00% | 47,00% |

## Componentes actuales de #117

| Dimensión | Puntos | Máximo | Cumplimiento |
|---|---:|---:|---:|
| Criterios y trazabilidad | 460 | 920 | 50,00% |
| Implementación funcional | 2.595 | 3.220 | 80,59% |
| Pruebas | 885 | 2.300 | 38,48% |
| DB, seguridad, permisos y migración | 845 | 1.380 | 61,23% |
| QA manual/ambiente | 25 | 920 | 2,72% |
| Documentación y enlaces | 355 | 460 | 77,17% |

## Ramas comparables

| Corte | Porcentaje global | Limitación |
|---|---:|---|
| `main@434a6ee` | 39,78% | No contiene #117 |
| `#112@b6e7d7a` | 48,75% | No contiene los ocho flujos exclusivos finales de #117 |
| `#117@107bce2` | 56,14% | PR abierta; no integrada en main |
| PR #114 aislada | no especificado | Suite y migraciones fueron sustituidas durante #115 |
| PR #116 aislada | no especificado | Numeración SQL incompatible y un único check `validate` |
| Rama futura tras automatización | 63,70% proyectado | Requiere Puerta A y validación real |

No se redondea una rama incompatible a un porcentaje aparentemente comparable.
