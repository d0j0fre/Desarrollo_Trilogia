# Recuperar la contraseña {#gen-recuperar}

## Solicitar el enlace de recuperación {#gen-recuperar-solicitar}

### Para qué sirve

Crear una contraseña nueva cuando olvidó la actual. El sistema le envía a su correo un enlace para cambiarla. Nadie de la empresa puede ver su contraseña, ni siquiera el Administrador.

### Quién puede usarlo

Todos los puestos y los clientes.

### Pasos

1. En la página de inicio de sesión, haga clic en **¿Olvidó su contraseña?**

   ![Enlace para recuperar la contraseña](capturas/general/gen-recuperar-01.png)

2. Complete el formulario:

   ![Formulario para pedir el enlace de recuperación](capturas/general/gen-recuperar-02.png)

   1. En **Correo electrónico**, escriba el correo de su cuenta.
   2. Haga clic en **ENVIAR ENLACE**.

3. Abra su correo. Busque el mensaje de recuperación (revise también la carpeta de correo no deseado).

### Resultado esperado

Aparece el mensaje «Le enviamos un correo con las instrucciones y el enlace para restablecer su contraseña.» y en unos minutos recibe el correo.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| El correo ingresado no está registrado. | Revise que esté bien escrito. Si es personal de la empresa, pregunte al Administrador con qué correo se creó su cuenta. |
| No fue posible enviar el correo de recuperación en este momento. Intente nuevamente más tarde. | Espere unos minutos y vuelva a intentarlo. |
| No fue posible procesar la solicitud en este momento. Intente nuevamente. | El servidor no respondió. Espere un minuto y vuelva a intentarlo. |
| El correo es obligatorio / Formato de correo inválido | Escriba su correo completo, con @ y dominio (por ejemplo, nombre@labodega.cr). |

> [!NOTA] Por seguridad, solo se pueden pedir **5 enlaces cada 15 minutos**. Si pide más, el sistema lo regresa a **¿Olvidó su contraseña?** con el mensaje «Alcanzó el límite de solicitudes de recuperación de contraseña. Intente nuevamente en N minutos.». Espere ese tiempo y use el último correo recibido.

## Elegir la contraseña nueva {#gen-recuperar-cambiar}

### Para qué sirve

Terminar la recuperación con el enlace que llegó a su correo.

### Quién puede usarlo

Quien recibió el correo de recuperación.

### Pasos

1. En el correo de recuperación, haga clic en el enlace. Se abre el sistema en la página **Elija su nueva contraseña**.

   ![Formulario para elegir la contraseña nueva](capturas/general/gen-recuperar-03.png)

   1. En **Nueva contraseña**, escriba la contraseña nueva.
   2. En **Confirmar contraseña**, escríbala otra vez, igual.
   3. Haga clic en **CAMBIAR CONTRASEÑA**.

2. Inicie sesión con la contraseña nueva (vea [Entrar al sistema](#gen-login-entrar)).

### Resultado esperado

Aparece el mensaje «La contraseña se actualizó correctamente.» La contraseña anterior deja de funcionar, también en la aplicación móvil.

> [!CONSEJO] Use una contraseña que no use en otros sitios: combine palabras, números y algún símbolo. Evite su nombre, su cédula o fechas.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| El enlace de recuperación no es válido o ya venció. | El enlace se usa una sola vez y dura poco tiempo. Pida uno nuevo desde [Solicitar el enlace de recuperación](#gen-recuperar-solicitar). |
| Las contraseñas no coinciden | Escriba la misma contraseña en los dos campos. |
| La contraseña debe tener al menos 4 caracteres | Use una contraseña más larga. Se recomiendan 8 caracteres o más. |
| La solicitud de restablecimiento no es válida. | Abra el enlace completo desde el correo, sin modificarlo. Si persiste, pida un enlace nuevo. |
| No fue posible restablecer la contraseña en este momento. Intente nuevamente. | El servidor no respondió. Espere un minuto y vuelva a intentarlo. |
