# Publicar versiones de la aplicación móvil {#mod-app-publicar}

La aplicación móvil no está en Google Play: se descarga desde el propio sistema, en la página **Aplicación móvil**. Cuando el equipo de desarrollo entrega una versión nueva, el Administrador la publica aquí. Desde ese momento, el personal la descarga escaneando el código QR, y la aplicación avisa a quien tenga una versión vieja (vea [Actualizar la aplicación](#gen-app-actualizar)).

Se abre con el botón **Aplicación móvil** del menú superior, o en **Administración → Logística y Flota → Aplicación móvil**.

## Publicar una versión nueva {#mod-app-publicar-version}

### Para qué sirve

Poner a disposición del personal la versión nueva de la aplicación y, si hace falta, obligar a actualizar a quienes tengan una versión que ya no sirve.

### Quién puede usarlo

Administrador. El resto del personal ve en la misma página solo el código QR para descargar (vea [Instalar la aplicación móvil](#gen-app-instalar)).

### Pasos

1. Pida al equipo de desarrollo el archivo de la aplicación (termina en **-arm64-v8a-release.apk**), el número de versión y el número de compilación.
2. Abra **Aplicación móvil** y baje hasta **Publicar una versión**. Si la sección está cerrada, haga clic en su título para abrirla.
3. Complete el formulario:

   ![Formulario Publicar una versión](capturas/administrador/adm-app-01.png)

   1. **Archivo de la aplicación (.apk)**: haga clic en **Seleccionar archivo** y elija el archivo.
   2. **Versión**: el número de versión, por ejemplo **1.3.0**.
   3. **Compilación**: el número de compilación que aparece en la aplicación, en **Ajustes → Versión**. Debe ser mayor que el de la última versión publicada.
   4. **Mínima soportada**: el número de compilación más viejo que todavía puede usarse. Las versiones anteriores quedan bloqueadas y deben actualizarse.
   5. **Novedades de la versión**: qué cambió, en una línea. El personal lo ve antes de descargar.
   6. Haga clic en **Publicar versión**.

   Si la actualización es obligatoria, escriba también el **Mensaje si la actualización es obligatoria** que verán los teléfonos bloqueados.

> [!ADVERTENCIA] Use el número de compilación **exacto** que muestra la aplicación. El equipo de desarrollo le suma 2000 al número con que compila (si compiló con 6, la aplicación muestra 2006). Si los números no coinciden, el bloqueo por versión mínima no funciona.

### Resultado esperado

Aparece el mensaje «Versión 1.3.0 publicada. Ya se puede instalar escaneando el código.». La parte de arriba de la página muestra el código QR, el botón **Descargar en este dispositivo** y la versión, el tamaño y la fecha:

![Código para instalar la versión publicada](capturas/administrador/adm-app-02.png "ancho=8")

## Retirar una versión publicada {#mod-app-publicar-retirar}

### Para qué sirve

Dejar de ofrecer una versión que salió con un error, para que nadie más la descargue.

### Quién puede usarlo

Administrador.

### Pasos

1. En **Aplicación móvil**, busque la tabla **Versiones publicadas**.

   ![Versiones publicadas y botón Retirar](capturas/administrador/adm-app-03.png)

2. Haga clic en **Retirar** en la fila de la versión y confirme en la ventana del navegador con **Aceptar**.

### Resultado esperado

Aparece el mensaje «La versión se retiró de circulación.». Los teléfonos que ya la tenían instalada siguen funcionando; la página vuelve a ofrecer la versión anterior que siga en circulación.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Elija el archivo de la aplicación. / Hay que subir el archivo de la aplicación. | Seleccione el archivo .apk. |
| El archivo tiene que ser un .apk de Android. / El archivo no parece una aplicación de Android… / El archivo está dañado o no es un paquete… | Pida de nuevo el archivo al equipo de desarrollo. |
| El número de compilación debe ser mayor que el de la última versión publicada. | Revise el número en la aplicación (**Ajustes → Versión**). |
| La versión mínima no puede ser mayor que la que se publica. | La **Mínima soportada** debe ser igual o menor que la **Compilación**. |
| Revise los datos de la versión. | Falta la versión, la compilación o la mínima soportada. |
| No fue posible publicar la versión. / No fue posible retirar la versión. | Vuelva a intentar. Si continúa, avise a Soporte. |
