# Departamentos del chat {#mod-chat-departamentos}

Un departamento del chat es un grupo de conversación por área (Bodega, Ventas, Cobros…). Los mensajes que alguien envía al departamento los reciben todos sus miembros. El Administrador crea los departamentos y decide quién pertenece a cada uno; el uso diario del chat se explica en [Chat interno](#mod-chat).

Se abre con el acceso rápido **Departamentos** del [Dashboard administrativo](#mod-dashboard), o en **Chat → Administrar departamentos**.

## Crear un departamento {#mod-chat-departamentos-crear}

### Para qué sirve

Abrir un canal de conversación para un área, para que el personal le escriba al área completa y no a una sola persona.

### Quién puede usarlo

Administrador.

### Pasos

1. Abra la pantalla **Departamentos de chat**.
2. En el recuadro **Crear departamento**:

   ![Crear un departamento del chat](capturas/administrador/adm-chat-01.png)

   1. **Nombre**: el nombre del área, por ejemplo **Facturación**.
   2. **Descripción**: para qué se usa el canal (opcional).
   3. Haga clic en **Crear**.

### Resultado esperado

Aparece el mensaje «Departamento de chat creado.» y el departamento se agrega debajo, todavía sin miembros («Este departamento todavía no tiene miembros.»).

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| Revise el nombre y la descripción del departamento. | El nombre es obligatorio y no puede pasar de 50 caracteres. |
| No fue posible crear el departamento. | Puede que ya exista uno con ese nombre. Revise la lista. |

## Administrar los miembros de un departamento {#mod-chat-departamentos-miembros}

### Para qué sirve

Decidir quién recibe los mensajes del departamento y quién puede escribir en él.

### Quién puede usarlo

Administrador.

### Pasos

1. Busque el recuadro del departamento:

   ![Datos y miembros de un departamento](capturas/administrador/adm-chat-02.png)

   1. **Nombre** y **Descripción**: puede corregirlos aquí.
   2. **Activo**: desmárquelo para cerrar el canal sin borrarlo.
   3. Haga clic en **Guardar** para guardar el nombre, la descripción o el estado.
   4. **Miembros**: las personas que ya pertenecen, con su permiso (**Puede publicar** o solo lectura). **Retirar** saca a una persona del departamento (confirme en la ventana **Retirar del departamento**).
   5. **Agregar miembro**: elija el usuario en la lista **Usuario**. Desmarque **Puede publicar** si esa persona solo debe leer.
   6. Haga clic en **Agregar**.

### Resultado esperado

- Al agregar: «Miembro agregado o actualizado.» y la persona aparece en **Miembros**. Si ya era miembro, se actualiza su permiso.
- Al retirar: «Miembro retirado.».
- Al guardar los datos: «Departamento actualizado.».

> [!CONSEJO] Agregue al departamento a la jefatura del área además del personal. Así ve las consultas que llegan aunque no las responda.

### Si algo sale mal

| Mensaje | Qué hacer |
|---|---|
| El departamento o usuario no es válido. | Elija un usuario en la lista antes de hacer clic en **Agregar**. |
| No fue posible agregar el miembro. / No fue posible retirar el miembro. | Vuelva a cargar la página e intente de nuevo. |
| Los datos del departamento no son válidos. / No fue posible actualizar el departamento. | Revise que el nombre no esté vacío. |
