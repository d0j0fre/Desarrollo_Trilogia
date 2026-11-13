# Alertas documentales {#mod-alertas-documentales}

Alertas documentales avisa qué documentos de [Documentos digitales](#mod-documentos) vencieron o están por vencer, ordenados por urgencia, para renovarlos antes de tener un problema con una municipalidad, el Ministerio de Salud o una aseguradora.

Se abre en **Administración → Seguridad → Alertas documentales**, o con el botón **Alertas** de Documentos digitales.

## Revisar y atender las alertas de vencimiento {#mod-alertas-documentales-atender}

### Para qué sirve

Saber qué trámites de renovación hay que empezar, y dejar constancia de que cada alerta ya se atendió.

### Quién puede usarlo

Administrador.

### Pasos

1. Abra **Administración → Seguridad → Alertas documentales**.

   ![Alertas de vencimiento agrupadas por prioridad](capturas/administrador/adm-alertas-01.png)

   1. **Generar sin duplicar**: revisa todos los documentos y crea las alertas que falten. No repite las que ya existen.
   2. **Estado**: **Activa** (pendiente) o **Atendida**.
   3. **Departamento** responsable.
   4. **Horizonte máximo**: hasta cuántos días antes del vencimiento quiere ver (por ejemplo, 30). Luego haga clic en **Aplicar**.
   5. **Atender**: marca la alerta como atendida, cuando ya inició la renovación.

### Resultado esperado

Las tarjetas muestran las **ALERTAS ACTIVAS** y los **DOCUMENTOS VENCIDOS**. Las alertas aparecen agrupadas por urgencia:

| Grupo | Qué hacer |
|---|---|
| Vencidas · Requieren atención inmediata | El documento ya venció. Renuévelo hoy. |
| Vencen hoy · Prioridad del día | El documento vence hoy. Renuévelo hoy mismo. |
| Próximos 7 días · Atender esta semana | Termine la renovación esta semana. |
| Entre 8 y 15 días · Planificar renovación | Empiece el trámite. |
| Entre 16 y 30 días · Seguimiento preventivo | Prepare los requisitos. |

Al hacer clic en **Generar sin duplicar** aparece el mensaje «Proceso completado: N alerta(s) nueva(s) o pendiente(s) de notificación.». Al hacer clic en **Atender** aparece «Alerta marcada como atendida.» y la alerta pasa a **Atendida**.

![Mensaje después de generar las alertas](capturas/administrador/adm-alertas-02.png)

> [!NOTA] El sistema genera las alertas solo todos los días a partir de las 6:00 a. m. El botón **Generar sin duplicar** sirve para no esperar al día siguiente, por ejemplo justo después de subir un documento.

> [!CONSEJO] Después de renovar, suba la versión nueva del documento y cambie su fecha de vencimiento (vea [Descargar, actualizar o inactivar un documento](#mod-documentos-detalle)). Si no, la alerta vuelve a aparecer.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Sin alertas para estos filtros | No hay documentos que venzan dentro del horizonte elegido. Amplíe el **Horizonte máximo** o cambie el **Estado** a **Todos**. |
| No fue posible … (generar o atender la alerta) | Vuelva a intentar. Si continúa, avise a Soporte. |
