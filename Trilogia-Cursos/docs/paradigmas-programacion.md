# Paradigmas de programación aplicados

La consigna académica existente en `docs/paradigmas-sc250.md` define exactamente estos cuatro paradigmas: **orientado a objetos, imperativo, funcional y declarativo**. Por esa razón no se sustituye “imperativo” por “dirigido por eventos”. El documento SC-250 conserva la defensa extensa; esta página es el mapa canónico y verificable del código vigente.

| Paradigma | Definición breve | Ubicación y elementos reales | Historias | Por qué es real | Cómo probarlo |
|---|---|---|---|---|---|
| Orientado a objetos | El sistema se compone de objetos con responsabilidades y contratos explícitos. | `Services/PurchasingDbService.cs`: `IPurchasingService`/`PurchasingDbService`; `Services/CrossSellDbService.cs`: `ICrossSellService`/`CrossSellDbService`; registro DI en `Program.cs`. | CU-101–104, CU-262 | Los controladores dependen de interfaces, no construyen acceso SQL ni almacenamiento; la implementación puede sustituirse en pruebas. | Ejecutar `PurchasingTests` y compilar la solución para verificar contratos e inyección. |
| Imperativo | Un flujo ordena instrucciones y efectos de estado paso a paso. | `PurchaseOrdersController.Receive` coordina validación, identidad de usuario, servicio, mensajes y redirección; `PurchasingDbService.ReceiveAsync` ejecuta el procedimiento transaccional. El checkout histórico de `CartController` es evidencia adicional. | CU-102, CU-173 | El orden importa: validar antes de escribir, confirmar inventario/auditoría y solo después responder. No es un ejemplo aislado. | Ejecutar la prueba funcional `0013_purchasing_functional_local.sql` y comprobar reintento, recepción parcial y sobre-recepción. |
| Funcional | Funciones deterministas transforman entradas sin depender de estado externo ni mutarlas. | `PurchasingPolicy.NormalizeLines`, `CalculatePriceVariation`; `ReportMetrics.CalculateAverage`; `CrossSellPolicy.Select`. | CU-102, CU-104, CU-132/134, CU-262 | Las mismas entradas producen la misma salida y las políticas se prueban sin MVC ni base de datos. | Ejecutar `PurchasingTests`, `SalesReportTests` y `CrossSellTests`, incluyendo no mutación, redondeo, soporte y fallback. |
| Declarativo | Se describe el resultado o la regla deseada y el motor decide cómo obtenerla. | DataAnnotations en modelos; LINQ en `CrossSellPolicy`; SQL parametrizado `sp_Reportes_VentasDetallado`, `sp_Reportes_DesempenoVendedores` y `sp_Ventas_CrossSellSuggestions` en 0015/0016. | CU-132, CU-134, CU-262 | Filtros, agregaciones, agrupaciones y constraints expresan qué filas/resultados son válidos sin codificar el plan físico. | Ejecutar ScriptDom sobre `database`, aplicar 0015–0016 en LocalDB y probar agrupaciones, filtros y resultados vacíos. |

## Demostración mínima

1. Mostrar que `PurchaseOrdersController` recibe `IPurchasingService` y que la transacción vive en SQL.
2. Ejecutar las pruebas puras de `PurchasingPolicy`, `ReportMetrics` y `CrossSellPolicy` dos veces y comparar resultados.
3. Abrir 0015 y cambiar únicamente los parámetros del reporte para demostrar agrupación declarativa.
4. Ejecutar el flujo de recepción para observar la secuencia imperativa y confirmar que un reintento no duplica inventario.

La evidencia extensa, fragmentos comentados y guion académico permanecen en `docs/paradigmas-sc250.md`; ambos documentos usan la misma lista y no presentan paradigmas contradictorios.
