# Planilla {#mod-planilla}

La planilla calcula el pago de cada empleado en un período (quincena o mes): su salario, los ingresos adicionales (bonos, comisiones, horas extra) y las deducciones (cargas sociales, aportes). Cada cálculo pasa por tres personas distintas antes de pagarse: un Administrador lo calcula, el Gerente lo aprueba y otro Administrador registra el pago.

Se abre en **Administración → Personal → Planilla**.

El trabajo se hace en este orden:

1. Configurar las **reglas** de ingresos y deducciones (se hace una vez y se actualiza cuando cambian).
2. Abrir el **período** de pago.
3. **Calcular** la planilla de cada empleado.
4. **Aprobar** (el Gerente) y **pagar** (un Administrador distinto del que calculó) cada cálculo.

> [!NOTA] El sistema no trae porcentajes legales precargados. Cada regla y cada período piden una **fuente o aprobación**: la ley, el reglamento o el acuerdo que respalda el dato. Así queda claro de dónde sale cada monto.

**De dónde salen los datos del cálculo:**

| Dato | De dónde lo toma el sistema |
|---|---|
| Salario | El salario vigente del empleado en cada día del período, según su historial en [Empleados](#mod-empleados). Si el salario cambió a mitad de quincena, se promedian los días con cada salario. Luego se multiplica por el **Factor salarial** del período. |
| Horas extra y ausencias | Las [jornadas aprobadas](#mod-aprobar-jornadas) del empleado dentro del período. |
| Comisiones | Las facturas de los pedidos que vendió el empleado dentro del período, multiplicadas por el porcentaje de la regla **COMISION_VENTAS**. |
| Deducciones e ingresos adicionales | Las reglas vigentes en la fecha del cálculo. |

> [!CONSEJO] Mantenga al día la ficha de cada empleado en [Empleados](#mod-empleados): su **Identificación** (cédula, obligatoria y única) y, cuando cambie el salario, el **Motivo de cambio salarial** y la **Fecha de vigencia del salario**. Así la planilla usa el salario correcto aunque el cambio caiga a mitad del período.

## Configurar una regla de ingreso o deducción {#mod-planilla-regla}

### Para qué sirve

Definir un concepto que se suma o se resta del salario, con la fecha desde la que rige. Por ejemplo, la cuota obrera, un aporte a la asociación solidarista o un bono.

### Quién puede usarlo

Administrador. El Gerente ve las reglas y los cálculos, pero no los formularios para cambiarlos. El Supervisor no tiene el permiso **Consultar planilla**: en su panel solo ve el resto del área **Personal**.

### Pasos

1. Abra **Administración → Personal → Planilla**.
2. Complete el recuadro **Regla versionada**:

   ![Formulario de regla versionada](capturas/administrador/adm-planilla-01.png)

   1. **Código**: un nombre corto en MAYÚSCULAS, sin espacios ni tildes, con guion bajo. Por ejemplo, **COMISION_VENTAS**.
   2. **Nombre**: como aparecerá en la boleta de pago.
   3. **Tipo**: **Ingreso** (suma) o **Deducción** (resta).
   4. **Cálculo**: cómo se obtiene el monto:
      - **Monto fijo**: el mismo monto cada período.
      - **% de salario**: un porcentaje del salario del período.
      - **Por hora extra**: un monto por cada hora extra aprobada.
      - **% bruto**: un porcentaje del total bruto (salario más ingresos). Úselo para las cargas sociales.
   5. **Valor**: el monto en colones o el porcentaje. Los porcentajes se escriben **como se leen**: **5** es 5 % y **10,67** es 10,67 %. La ayuda debajo del campo cambia según el tipo de cálculo que elija.
   6. Complete **Vigente desde** (y **Vigente hasta**, si la regla termina), la **Fuente o aprobación**, y haga clic en **Guardar regla**.

   **Tope opcional** limita el monto máximo de la regla en cada período.

Dos códigos tienen un uso especial y el sistema revisa que estén bien configurados:

| Código | Para qué | Cómo se configura |
|---|---|---|
| **COMISION_VENTAS** | Pagar comisión a los vendedores sobre lo que facturaron en el período. | **Ingreso**, **% bruto**, con el porcentaje (por ejemplo, **3** para 3 %). |
| **AUSENCIA_HORA** | Rebajar las horas de ausencia aprobadas. | **Deducción**, **Monto fijo**, con el monto que se rebaja por cada hora. |

> [!NOTA] Un porcentaje debe estar entre 0 y 100. Los montos fijos y los montos **Por hora extra** pueden ser de ₡1 000 o más, por ejemplo **15000**.

### Resultado esperado

Aparece el mensaje «Regla versionada guardada.». La regla se agrega a la tabla **Reglas versionadas**, que muestra su **Código**, **Tipo**, **Cálculo**, **Valor** (los porcentajes con el símbolo %, por ejemplo **10,67 %**, y los montos en colones), **Tope**, **Vigencia** y **Fuente**. Los cálculos con fecha dentro de su vigencia la usan.

![Tabla de reglas versionadas](capturas/administrador/adm-planilla-06.png)

Para cambiar una regla, guarde una **versión nueva** con el mismo código y una fecha **Vigente desde** posterior. La versión anterior se conserva para los períodos ya calculados.

## Abrir un período de pago {#mod-planilla-periodo}

### Para qué sirve

Definir las fechas que cubre el pago y qué parte del salario mensual corresponde.

### Quién puede usarlo

Administrador.

### Pasos

1. Complete el recuadro **Período**:

   ![Formulario para abrir un período](capturas/administrador/adm-planilla-02.png)

   1. **Frecuencia**: **Quincenal** o **Mensual**.
   2. **Desde**: primer día del período.
   3. **Hasta**: último día. El período puede tener 31 días como máximo y no puede traslaparse con otro.
   4. **Factor salarial**: la parte del salario mensual que se paga. Para una quincena escriba **0,5**; para un mes, **1**.
   5. **Fuente o aprobación del factor**: el documento o acuerdo que lo respalda.
   6. Haga clic en **Crear período**.

### Resultado esperado

Aparece el mensaje «Periodo creado.». El período queda abierto y aparece en la lista **Período** del recuadro **Calcular empleado**, con sus fechas (por ejemplo, «Quincenal · 16/10/2026 al 31/10/2026»).

## Calcular la planilla de un empleado {#mod-planilla-calcular}

### Para qué sirve

Obtener el bruto, las deducciones y el neto a pagar de un empleado en el período, con su salario vigente, sus jornadas aprobadas, sus ventas facturadas y las reglas vigentes.

### Quién puede usarlo

Administrador.

### Pasos

1. Antes de calcular, revise que las jornadas del empleado estén aprobadas (vea [Aprobar jornadas](#mod-aprobar-jornadas)).
2. Complete el recuadro **Calcular empleado**:

   ![Formulario para calcular la planilla de un empleado](capturas/administrador/adm-planilla-03.png)

   1. **Período**: elija un período abierto de la lista. Si dice **No hay períodos abiertos**, cree uno primero.
   2. **Empleado**: elija un empleado activo de la lista (aparece su nombre y su puesto).
   3. Haga clic en **Calcular planilla**.

   No hace falta escribir las comisiones: el recuadro marcado explica que se calculan solas con las facturas del período y la regla **COMISION_VENTAS**.

### Resultado esperado

Aparece el mensaje «Planilla calculada de forma idempotente.» y el cálculo se agrega a **Cálculos registrados** con el estado **Borrador**. Si hace clic dos veces, el sistema no duplica el cálculo.

## Aprobar, pagar o revertir un cálculo {#mod-planilla-aprobar}

### Para qué sirve

Controlar que ningún pago salga sin revisión: una persona calcula, otra aprueba y una tercera registra el pago.

### Quién puede usarlo

El **Gerente** aprueba. El **Administrador** calcula y paga, y también puede revertir. Por seguridad, **quien calculó no puede aprobar**, y **quien paga no puede ser ninguno de los dos anteriores**. Se necesitan tres usuarios distintos: un Administrador que calcula, el Gerente que aprueba y otro Administrador que paga.

### Pasos

1. Baje hasta **Cálculos registrados**. Cada fila muestra el **PERÍODO** (con fechas como 16/10/2026), el **BRUTO**, las **DEDUCCIONES** y el **NETO** en colones, y el **ESTADO**. Los botones que ve dependen de sus permisos:

   ![Cálculos registrados y sus acciones](capturas/administrador/adm-planilla-04.png)

   1. **Aprobar**: en un cálculo **Borrador**, si usted no lo calculó. Es la tarea del Gerente.
   2. **Pagar**: en un cálculo **Aprobada**, si usted no lo calculó ni lo aprobó. Es la tarea de un Administrador.
   3. **Revertir**: en un cálculo **Aprobada** o **Pagada**. Escriba el **Motivo de reversión** antes de hacer clic.

   Si su puesto no puede hacer ninguna acción sobre un cálculo, la fila dice **Sin acciones disponibles para su perfil**.

2. Confirme en la ventana **Confirmar acción** con **Sí, continuar**.

### Resultado esperado

Aparece el mensaje «Estado de planilla actualizado.» y el estado cambia a **Aprobada**, **Pagada** o **Revertida**. Un cálculo **Revertida** ya no tiene acciones. Cada cambio de estado queda en [Auditoría](#mod-auditoria).

### Si algo sale mal

Los errores aparecen en un mensaje rojo arriba de la planilla, con la causa. Usted sigue en la misma pantalla:

![Mensaje de planilla cuando la operación no se permite](capturas/administrador/adm-planilla-05.png)

| Mensaje | Causa más común | Qué hacer |
|---|---|---|
| La aprobación requiere un actor distinto del calculador. | Intentó aprobar un cálculo que usted mismo hizo. | Pida al Gerente que lo apruebe. |
| El pago requiere un tercer actor y aprobación previa. | Intentó pagar un cálculo que usted calculó o aprobó, o que todavía no está **Aprobada**. | Pida a otro Administrador que registre el pago. |
| Ya existe cálculo para el empleado y periodo. | Ese empleado ya tiene un cálculo en el período. | Revíselo en **Cálculos registrados**. Para rehacerlo, reviértalo primero. |
| La solicitud no es válida: … | Falta un dato o tiene un formato incorrecto. El texto dice qué campo revisar. | Corrija el campo indicado y vuelva a intentarlo. |
| La solicitud no es válida: COMISION_VENTAS debe ser un ingreso de tipo % bruto (por ejemplo 5 para 5 %). | La regla **COMISION_VENTAS** no tiene el tipo o el cálculo correctos. | Configúrela como indica la tabla de códigos especiales. |
| La solicitud no es válida: Un porcentaje debe estar entre 0 y 100 (por ejemplo 10,67 para 10,67 %). | Escribió un porcentaje mayor que 100. | Escriba el porcentaje como se lee, por ejemplo **10,67**. |
| Revise las reglas vigentes de comisiones, ausencias y horas extra antes de calcular la planilla. | Las deducciones superan el bruto; hay horas extra aprobadas sin una regla **Por hora extra**; hay ausencias sin la regla **AUSENCIA_HORA**; o el vendedor tiene ventas facturadas y no existe la regla **COMISION_VENTAS**. | Revise y complete las reglas (vea [Configurar una regla](#mod-planilla-regla)). |
| No fue posible procesar la operación de planilla. El incidente quedó registrado; intente de nuevo en unos minutos. | Falla técnica del sistema. | Espere unos minutos y repita. Si continúa, avise a Soporte. |
