# Auditoría {#mod-auditoria}

La Auditoría es el registro de lo que hace cada usuario en el sistema: quién inició sesión, quién creó, editó o inactivó algo, quién despachó una ruta o resolvió una solicitud, y cuándo. Sirve para revisar el control interno y para aclarar quién hizo un cambio. Es solo de consulta: nadie puede modificar ni borrar sus registros desde esta pantalla.

Se abre en **Administración → Seguridad → Auditoría**.

## Consultar el registro de auditoría {#mod-auditoria-consultar}

### Para qué sirve

Encontrar las acciones de una persona, de un módulo o de un tipo de operación, por ejemplo todos los registros de empleados nuevos de la semana.

### Quién puede usarlo

Auditor Interno (solo consulta) y Administrador.

### Pasos

1. Abra **Auditoría**. La tabla muestra los registros más recientes primero, hasta 200, de todas las bitácoras.
2. Elija la bitácora en las pestañas de arriba: **Todas las bitácoras** (todo junto), **Registro general** (lo que hacen los usuarios en el sistema), **RR. HH.** (jornadas, licencias y tareas del personal) o **Planilla** (reglas, períodos, cálculos, aprobaciones y pagos). La pestaña elegida queda resaltada.
3. Use los filtros:

   ![Pestañas y filtros de la auditoría](capturas/auditor/aud-auditoria-01.png)

   1. **Módulo**: el área del sistema (por ejemplo **Autenticación**, **Empleados**, **Inventario**, **Pedidos** o **Planilla**). La lista trae los módulos que realmente tienen registros. Deje **Todos** para buscar en todos.
   2. **Acción**: el tipo de operación, por ejemplo **Login**, **Crear**, **Cambio de estado** o **Resolver solicitud**. También trae solo las acciones que existen.
   3. **Buscar**: un nombre, un correo, un módulo o una palabra de la descripción.
   4. Haga clic en **Filtrar**.

Las pestañas **RR. HH.** y **Planilla** son de solo lectura y muestran las mismas columnas que el registro general. Al cambiar de pestaña, los filtros se reinician.

### Resultado esperado

La tabla muestra solo los registros que cumplen los filtros. Cada fila dice:

| Columna | Qué muestra |
|---|---|
| **Fecha** | El día y la hora de la acción. |
| **Usuario** | El nombre y el correo de quien la hizo. |
| **Rol** | El puesto que tenía en ese momento. |
| **Origen** | La bitácora de donde viene el registro: **General**, **RR. HH.** o **Planilla**. |
| **Módulo** | El área del sistema. |
| **Acción** | Qué hizo. |
| **Descripción** | El detalle: qué registro tocó y cuál fue el cambio. |
| **IP** | La dirección de la red desde donde se conectó. |

![Registro encontrado con los filtros](capturas/auditor/aud-auditoria-02.png)

Para volver a ver todo en la pestaña en que está, haga clic en **Limpiar filtros**.

> [!CONSEJO] Si no encuentra una palabra en las listas **Módulo** y **Acción**, deje **Todos** y **Todas** y escriba la palabra en **Buscar**: también busca en el módulo y en la descripción.

![Búsqueda por una palabra de la descripción](capturas/auditor/aud-auditoria-03.png)

> [!NOTA] La pantalla muestra como máximo 200 registros. Si busca algo antiguo, use filtros más precisos (el correo de la persona y una palabra de la descripción) para que el registro entre en esos 200.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| No hay registros de auditoría para mostrar. | Ningún registro cumple los filtros. Quite un filtro o revise lo que escribió en **Buscar**. |

> [!CONSEJO] Fuera de la oficina, el Auditor Interno puede revisar la bitácora en la aplicación móvil (vea [Bitácora](#app-bitacora)).
