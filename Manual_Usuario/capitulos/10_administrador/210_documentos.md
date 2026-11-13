# Documentos digitales {#mod-documentos}

Documentos digitales es el archivo privado de la empresa: patentes, permisos sanitarios, pólizas, contratos y certificaciones. Cada documento guarda su archivo (PDF o imagen), sus fechas de emisión y vencimiento, y todas sus versiones. Nada se borra: un documento viejo se inactiva y conserva su historial.

Se abre en **Administración → Seguridad → Documentos digitales**.

## Buscar y consultar documentos {#mod-documentos-buscar}

### Para qué sirve

Encontrar rápido un documento (por ejemplo, la póliza que pide una aseguradora) y saber cuáles están por vencer.

### Quién puede usarlo

Administrador.

### Pasos

1. Abra **Administración → Seguridad → Documentos digitales**. Las tarjetas de arriba cuentan los documentos **VIGENTES**, **PRÓXIMOS A VENCER** (30 días o menos), **VENCIDOS** y **SIN VENCIMIENTO**.

   ![Lista de documentos digitales con filtros](capturas/administrador/adm-documentos-01.png)

   1. Escriba parte del título o la referencia.
   2. Use las listas para filtrar por tipo, departamento, estado o vencimiento (**Vigente**, **Por vencer**, **Vencido**, **No vence**).
   3. Haga clic en **Filtrar**.
   4. **Nuevo** abre el formulario para subir un documento.
   5. **Ver** abre el detalle del documento.

### Resultado esperado

La tabla muestra cada documento con su referencia, tipo, departamento, fecha de **VENCIMIENTO** (con una etiqueta **Vigente**, **Por vencer** o **Vencido**) y su **ESTADO**.

La columna **VERSIÓN** muestra el número de versión de cada documento (**v1**, **v2**…).

## Subir un documento nuevo {#mod-documentos-crear}

### Para qué sirve

Guardar en el sistema una copia segura de un documento legal o comercial, con su fecha de vencimiento para que el sistema avise a tiempo.

### Quién puede usarlo

Administrador.

### Pasos

1. En **Documentos digitales**, haga clic en **Nuevo**.
2. Complete el formulario:

   ![Formulario para subir un documento](capturas/administrador/adm-documentos-02.png)

   1. **Tipo** (Certificación, Contrato, Licencia, Permiso, Póliza u Otro) y **Departamento responsable**.
   2. **Título** y **Descripción**: qué documento es.
   3. **Referencia**: el número oficial (de póliza, de permiso, de contrato).
   4. **Emisión** y **Vencimiento**. Si el documento no vence, marque **No vence**.
   5. **Archivo privado**: haga clic en **Seleccionar archivo** y elija el PDF, JPG o PNG (máximo 10 MB).
   6. Deje el **Estado** en **Vigente** y haga clic en **Guardar documento**.

### Resultado esperado

Aparece el mensaje «Documento almacenado de forma privada.» y el documento aparece en la lista como versión 1. El sistema calcula una huella (**Integridad**) que permite comprobar que el archivo no se alteró.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Seleccione un tipo de documento. / El título es obligatorio. | Complete los datos que faltan. |
| Seleccione un archivo. | Haga clic en **Seleccionar archivo** y elija el documento. |
| Indique la fecha de vencimiento o marque que no vence. | Escriba el vencimiento o marque **No vence**. |
| La fecha de vencimiento no puede ser anterior a la emisión. | Revise las dos fechas. |
| El archivo supera el tamaño máximo permitido de 10 MB. | Escanee el documento con menor resolución o divídalo. |
| El archivo debe estar en formato PDF, JPG, JPEG o PNG. | Convierta el archivo a PDF. |
| El tipo declarado no coincide con la extensión del archivo. / El contenido del archivo no corresponde a un formato admitido. | El archivo está dañado o se le cambió la extensión. Use el archivo original. |
| No fue posible guardar el documento. | Vuelva a intentar. Si continúa, avise a Soporte. |

## Descargar, actualizar o inactivar un documento {#mod-documentos-detalle}

### Para qué sirve

Obtener una copia del documento, reemplazarlo cuando se renueva (por ejemplo, la póliza del año nuevo) o retirarlo cuando ya no aplica.

### Quién puede usarlo

Administrador.

### Pasos

1. En la lista, haga clic en **Ver**.

   ![Detalle de un documento](capturas/administrador/adm-documentos-03.png)

   1. **Descarga autorizada**: descarga el archivo actual. Cada descarga queda registrada.
   2. **Publicar nueva versión**: haga clic en **Seleccionar archivo** y elija el documento renovado.
   3. Haga clic en **Guardar nueva versión**.
   4. **Inactivar documento**: lo retira de la lista de vigentes sin borrar sus versiones.
   5. **Editar**: cambia los datos (fechas, estado, referencia) sin cambiar el archivo.

2. Para corregir datos, haga clic en **Editar**, cambie lo necesario (por ejemplo, la nueva fecha de **Vencimiento** y el **Estado**: **Vigente**, **Suspendido** o **Archivado**) y haga clic en **Guardar cambios**.

   ![Editar los datos de un documento](capturas/administrador/adm-documentos-05.png)

### Resultado esperado

- Nueva versión: «Nueva versión almacenada.». El **Historial de versiones** muestra la versión nueva arriba; las anteriores se pueden seguir descargando con **Descargar**.

  ![Historial de versiones de un documento](capturas/administrador/adm-documentos-04.png)

- Editar: «Documento actualizado.».
- Inactivar: «Documento desactivado sin borrar su historial.». Para volver a activarlo, abra el detalle y haga clic en el botón de reactivar: «Documento reactivado.».

> [!CONSEJO] Cuando renueve un documento, suba la versión nueva **y** cambie la fecha de **Vencimiento** con **Editar**. Así el sistema deja de mostrar la alerta del documento viejo.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Los mismos mensajes del archivo que en [Subir un documento nuevo](#mod-documentos-crear). | Revise el formato y el tamaño del archivo. |
| No fue posible reemplazar el archivo. | Vuelva a intentar. Si continúa, avise a Soporte. |
