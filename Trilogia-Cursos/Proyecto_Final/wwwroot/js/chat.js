document.addEventListener("DOMContentLoaded", function () {

    const btnPrivateChat = document.getElementById("btnPrivateChat");
    const btnDepartmentChat = document.getElementById("btnDepartmentChat");

    const chatWindow = document.getElementById("chatWindow");
    const closeChat = document.getElementById("closeChat");

    if (!btnPrivateChat || !btnDepartmentChat || !chatWindow || !closeChat) {
        return;
    }

    const conexionChat = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect()
        .build();

    let usuariosChat = [];
    let usuariosCargados = false;

    let conversacionActualId = null;
    let usuarioActualIdChat = null;

    let departamentosChat = [];
    let departamentosCargados = false;

    // Antes esta variable no estaba declarada (global implícita).
    let modoChatActual = null;

    // Saca la ventana de cualquier navbar, footer o contenedor.
    document.body.appendChild(chatWindow);

    iniciarSignalR();

    conexionChat.on("ReceiveMessage", function (mensaje) {

        const messagesContainer =
            document.getElementById("chatMessages");

        if (!messagesContainer) {
            return;
        }

        if (
            Number(mensaje.conversacionId) !==
            Number(conversacionActualId)
        ) {
            return;
        }

        const esMio =
            Number(mensaje.remitenteId) ===
            Number(usuarioActualIdChat);

        agregarMensajeTiempoReal(mensaje, esMio);
    });

    conexionChat.onreconnected(async function () {

        console.log("✅ SignalR reconectado");

        if (conversacionActualId) {
            try {
                await conexionChat.invoke(
                    "JoinConversation",
                    String(conversacionActualId)
                );
            }
            catch (error) {
                console.error(
                    "No fue posible volver a la conversación:",
                    error
                );
            }
        }
    });

    conexionChat.onreconnecting(function () {
        console.warn("SignalR intentando reconectar...");
    });

    conexionChat.onclose(function () {
        console.warn("SignalR desconectado.");
    });

    btnPrivateChat.addEventListener("click", async function (event) {
        event.preventDefault();

        modoChatActual = "privado";
        chatWindow.classList.add("show");

        if (!usuariosCargados) {
            await cargarUsuariosChat();
        }
        else {
            mostrarPantallaUsuarios();
            mostrarUsuarios(usuariosChat);
            configurarBuscador();
        }
    });

    async function cargarDepartamentosChat() {

        mostrarPantallaDepartamentos();

        const lista = document.getElementById(
            "chatDepartmentsList"
        );

        if (!lista) {
            return;
        }

        lista.innerHTML = `
        <div class="chat-loading">
            Cargando departamentos...
        </div>
    `;

        try {
            const response = await fetch(
                "/Chat/GetDepartments"
            );

            const data = await response.json();

            if (!response.ok || !data.success) {
                throw new Error(
                    data.message ||
                    "No fue posible cargar los departamentos."
                );
            }

            departamentosChat = data.departments || [];
            departamentosCargados = true;

            mostrarDepartamentos(departamentosChat);
        }
        catch (error) {
            console.error(error);

            lista.innerHTML = `
            <div class="chat-loading">
                ${escaparHtml(error.message)}
            </div>
        `;
        }
    }

    function mostrarPantallaDepartamentos() {

        const chatBody = document.querySelector(
            "#chatWindow .chat-body"
        );

        if (!chatBody) {
            return;
        }

        chatBody.innerHTML = `
        <div class="chat-search">
            <i class="fa fa-search"></i>

            <input type="text"
                   id="chatDepartmentSearch"
                   placeholder="Buscar departamento..."
                   autocomplete="off">
        </div>

        <div id="chatDepartmentsList"
             class="chat-users-list">

            <div class="chat-loading">
                Abre el chat para cargar los departamentos.
            </div>

        </div>
    `;

        configurarBuscadorDepartamentos();
    }

    // Antes esta función estaba duplicada (dos definiciones idénticas).
    function mostrarDepartamentos(departamentos) {

        const lista = document.getElementById(
            "chatDepartmentsList"
        );

        if (!lista) {
            return;
        }

        lista.innerHTML = "";

        if (!departamentos || departamentos.length === 0) {
            lista.innerHTML = `
            <div class="chat-loading">
                No hay departamentos disponibles.
            </div>
        `;

            return;
        }

        departamentos.forEach(function (departamento) {

            lista.insertAdjacentHTML(
                "beforeend",
                crearDepartamentoChat(departamento)
            );
        });
    }

    function crearDepartamentoChat(departamento) {

        const nombre =
            departamento.nombre ||
            "Departamento";

        const descripcion =
            departamento.descripcion ||
            "Sin descripción";

        const totalUsuarios =
            Number(departamento.totalUsuarios || 0);

        return `
            <button type="button"
                    class="chat-department-item"
                    data-id="${departamento.perfilId ?? ""}"
                    data-name="${escaparAtributo(nombre)}">

                <div class="chat-user-avatar">
                    <i class="fa fa-users"></i>
                </div>

                <div class="chat-user-info">
                    <span class="chat-user-name">
                        ${escaparHtml(nombre)}
                    </span>

                    <span class="chat-user-email">
                        ${escaparHtml(descripcion)}
                    </span>

                    <span class="chat-department-count">
                        ${totalUsuarios} usuario(s)
                    </span>
                </div>

                <i class="fa fa-chevron-right chat-user-arrow"></i>
            </button>
        `;
    }

    function configurarBuscadorDepartamentos() {

        const buscador = document.getElementById(
            "chatDepartmentSearch"
        );

        if (!buscador) {
            return;
        }

        buscador.addEventListener(
            "input",
            function () {

                const texto = this.value
                    .trim()
                    .toLowerCase();

                const filtrados =
                    departamentosChat.filter(
                        function (departamento) {

                            const nombre = (
                                departamento.nombre || ""
                            ).toLowerCase();

                            const descripcion = (
                                departamento.descripcion || ""
                            ).toLowerCase();

                            return (
                                nombre.includes(texto) ||
                                descripcion.includes(texto)
                            );
                        }
                    );

                mostrarDepartamentos(filtrados);
            }
        );
    }

    btnDepartmentChat.addEventListener("click", async function (event) {
        event.preventDefault();

        modoChatActual = "departamento";
        chatWindow.classList.add("show");

        if (!departamentosCargados) {
            await cargarDepartamentosChat();
        }
        else {
            mostrarPantallaDepartamentos();
            mostrarDepartamentos(departamentosChat);
        }
    });

    closeChat.addEventListener("click", function () {
        chatWindow.classList.remove("show");
    });

    chatWindow.addEventListener("click", async function (event) {

        const sendDepartmentButton =
            event.target.closest("#sendDepartmentMessage");

        if (sendDepartmentButton) {

            const input =
                document.getElementById("chatDepartmentMessageInput");

            const messagesContainer =
                document.getElementById("chatDepartmentMessages");

            if (!input || !messagesContainer) {
                return;
            }

            const perfilId =
                Number(messagesContainer.dataset.profileId);

            const contenido =
                input.value.trim();

            if (!perfilId || !contenido) {
                input.focus();
                return;
            }

            sendDepartmentButton.disabled = true;
            input.disabled = true;

            const enviado = await enviarMensajeDepartamento(
                perfilId,
                contenido
            );

            if (enviado) {
                input.value = "";

                await cargarMensajesDepartamento(
                    perfilId
                );
            }

            sendDepartmentButton.disabled = false;
            input.disabled = false;
            input.focus();

            return;
        }

        const departamentoButton =
            event.target.closest(".chat-department-item");

        if (departamentoButton) {

            const perfilId =
                Number(departamentoButton.dataset.id);

            const nombre =
                departamentoButton.dataset.name ||
                "Departamento";

            if (perfilId > 0) {
                await abrirChatDepartamento(
                    perfilId,
                    nombre
                );
            }

            return;
        }

        const usuarioButton =
            event.target.closest(
                ".chat-user-item:not(.chat-department-item)"
            );

        if (usuarioButton) {

            const usuarioId =
                Number(usuarioButton.dataset.id);

            const nombre =
                usuarioButton.dataset.name || "Usuario";

            const correo =
                usuarioButton.dataset.email || "";

            if (usuarioId > 0) {
                await abrirConversacion(
                    usuarioId,
                    nombre,
                    correo
                );
            }

            return;
        }

        const backButton =
            event.target.closest("#backToChatUsers");

        if (backButton) {

            await salirConversacionActual();

            if (modoChatActual === "departamento") {
                mostrarPantallaDepartamentos();
                mostrarDepartamentos(
                    departamentosChat
                );
            }
            else {
                mostrarPantallaUsuarios();
                mostrarUsuarios(usuariosChat);
                configurarBuscador();
            }

            return;
        }

        const sendButton =
            event.target.closest("#sendChatMessage");

        if (sendButton) {

            const input =
                document.getElementById("chatMessageInput");

            if (!input || !conversacionActualId) {
                return;
            }

            const contenido = input.value.trim();

            if (!contenido) {
                input.focus();
                return;
            }

            sendButton.disabled = true;
            input.disabled = true;

            const enviado = await enviarMensaje(
                conversacionActualId,
                contenido
            );

            if (enviado) {
                input.value = "";
            }

            sendButton.disabled = false;
            input.disabled = false;
            input.focus();

            return;
        }
    });

    async function enviarMensajeDepartamento(
        perfilId,
        contenido
    ) {
        const body = new URLSearchParams();

        body.append("profileId", perfilId);
        body.append("content", contenido);

        try {
            const response = await fetch(
                "/Chat/SendDepartmentMessage",
                {
                    method: "POST",
                    headers: {
                        "Content-Type":
                            "application/x-www-form-urlencoded"
                    },
                    body: body
                }
            );

            const data = await response.json();

            if (!response.ok || !data.success) {
                throw new Error(
                    data.message ||
                    "No fue posible enviar el mensaje al departamento."
                );
            }

            return true;
        }
        catch (error) {
            console.error(error);
            alert(error.message);

            return false;
        }
    }

    chatWindow.addEventListener(
        "keydown",
        function (event) {

            if (
                event.key === "Enter" &&
                event.target.id === "chatMessageInput"
            ) {
                event.preventDefault();

                const sendButton =
                    document.getElementById("sendChatMessage");

                if (sendButton && !sendButton.disabled) {
                    sendButton.click();
                }

                return;
            }

            if (
                event.key === "Enter" &&
                event.target.id === "chatDepartmentMessageInput"
            ) {
                event.preventDefault();

                const sendButton =
                    document.getElementById("sendDepartmentMessage");

                if (sendButton && !sendButton.disabled) {
                    sendButton.click();
                }
            }
        }
    );

    async function iniciarSignalR() {

        try {
            await conexionChat.start();

            console.log("✅ SignalR conectado");
        }
        catch (error) {
            console.error(
                "❌ Error al conectar SignalR:",
                error
            );

            setTimeout(iniciarSignalR, 5000);
        }
    }

    async function esperarConexionSignalR() {

        if (
            conexionChat.state ===
            signalR.HubConnectionState.Connected
        ) {
            return;
        }

        if (
            conexionChat.state ===
            signalR.HubConnectionState.Disconnected
        ) {
            await iniciarSignalR();
        }

        let intentos = 0;

        while (
            conexionChat.state !==
            signalR.HubConnectionState.Connected &&
            intentos < 20
        ) {
            await esperar(250);
            intentos++;
        }

        if (
            conexionChat.state !==
            signalR.HubConnectionState.Connected
        ) {
            throw new Error(
                "No fue posible conectar el chat en tiempo real."
            );
        }
    }

    function esperar(milisegundos) {

        return new Promise(function (resolve) {
            setTimeout(resolve, milisegundos);
        });
    }

    async function cargarUsuariosChat() {

        mostrarPantallaUsuarios();

        const lista =
            document.getElementById("chatUsersList");

        if (!lista) {
            return;
        }

        lista.innerHTML = `
            <div class="chat-loading">
                Cargando usuarios...
            </div>
        `;

        try {
            const response = await fetch(
                "/Chat/GetUsers"
            );

            const data = await response.json();

            if (!response.ok || !data.success) {
                throw new Error(
                    data.message ||
                    "No fue posible cargar los usuarios."
                );
            }

            usuariosChat = data.users || [];
            usuariosCargados = true;

            mostrarUsuarios(usuariosChat);
            configurarBuscador();
        }
        catch (error) {
            console.error(error);

            lista.innerHTML = `
                <div class="chat-loading">
                    ${escaparHtml(error.message)}
                </div>
            `;
        }
    }

    async function abrirConversacion(
        usuarioId,
        nombre,
        correo
    ) {
        const chatBody = document.querySelector(
            "#chatWindow .chat-body"
        );

        if (!chatBody) {
            return;
        }

        chatBody.innerHTML = `
            <div class="chat-loading">
                Abriendo conversación...
            </div>
        `;

        try {
            const body = new URLSearchParams();

            body.append("userId", usuarioId);

            const response = await fetch(
                "/Chat/OpenConversation",
                {
                    method: "POST",
                    headers: {
                        "Content-Type":
                            "application/x-www-form-urlencoded"
                    },
                    body: body
                }
            );

            const data = await response.json();

            if (!response.ok || !data.success) {
                throw new Error(
                    data.message ||
                    "No fue posible abrir la conversación."
                );
            }

            await mostrarConversacion(
                data.conversationId,
                usuarioId,
                nombre,
                correo
            );
        }
        catch (error) {
            console.error(error);

            chatBody.innerHTML = `
                <div class="chat-loading">
                    ${escaparHtml(error.message)}
                </div>

                <button type="button"
                        id="backToChatUsers"
                        class="chat-back-error">
                    Volver a los usuarios
                </button>
            `;
        }
    }

    async function enviarMensaje(
        conversationId,
        contenido
    ) {
        const body = new URLSearchParams();

        body.append(
            "conversationId",
            conversationId
        );

        body.append(
            "content",
            contenido
        );

        try {
            const response = await fetch(
                "/Chat/SendMessage",
                {
                    method: "POST",
                    headers: {
                        "Content-Type":
                            "application/x-www-form-urlencoded"
                    },
                    body: body
                }
            );

            const data = await response.json();

            if (!response.ok || !data.success) {
                throw new Error(
                    data.message ||
                    "No fue posible enviar el mensaje."
                );
            }

            /*
             * No agregamos el mensaje manualmente.
             * SignalR lo enviará tanto al emisor como al receptor.
             */
            return true;
        }
        catch (error) {
            console.error(error);
            alert(error.message);

            return false;
        }
    }

    function agregarMensajeTiempoReal(
        mensaje,
        esMio
    ) {
        const messagesContainer =
            document.getElementById("chatMessages");

        if (!messagesContainer) {
            return;
        }

        const empty = messagesContainer.querySelector(
            ".chat-empty-conversation"
        );

        if (empty) {
            empty.remove();
        }

        messagesContainer.insertAdjacentHTML(
            "beforeend",
            crearMensajeHtml(mensaje, esMio)
        );

        messagesContainer.scrollTop =
            messagesContainer.scrollHeight;
    }

    async function mostrarConversacion(
        conversacionId,
        usuarioId,
        nombre,
        correo
    ) {
        const chatBody = document.querySelector(
            "#chatWindow .chat-body"
        );

        if (!chatBody) {
            return;
        }

        chatBody.innerHTML = `
            <div class="chat-conversation-header">

                <button type="button"
                        id="backToChatUsers"
                        class="chat-back-button"
                        title="Volver">

                    <i class="fa fa-arrow-left"></i>

                </button>

                <div class="chat-conversation-user">

                    <div class="chat-conversation-name">
                        ${escaparHtml(nombre)}
                    </div>

                    <div class="chat-conversation-email">
                        ${escaparHtml(correo)}
                    </div>

                </div>

            </div>

            <div class="chat-messages"
                 id="chatMessages"
                 data-conversation-id="${conversacionId}"
                 data-user-id="${usuarioId}">

                <div class="chat-loading">
                    Cargando mensajes...
                </div>

            </div>

            <div class="chat-message-form">

                <input type="text"
                       id="chatMessageInput"
                       placeholder="Escribe un mensaje..."
                       maxlength="1000"
                       autocomplete="off">

                <button type="button"
                        id="sendChatMessage"
                        title="Enviar">

                    <i class="fa fa-paper-plane"></i>

                </button>

            </div>
        `;

        await esperarConexionSignalR();

        if (
            conversacionActualId &&
            Number(conversacionActualId) !==
            Number(conversacionId)
        ) {
            await conexionChat.invoke(
                "LeaveConversation",
                String(conversacionActualId)
            );
        }

        conversacionActualId =
            Number(conversacionId);

        await conexionChat.invoke(
            "JoinConversation",
            String(conversacionId)
        );

        await cargarMensajes(conversacionId);

        const input =
            document.getElementById("chatMessageInput");

        if (input) {
            input.focus();
        }
    }

    async function salirConversacionActual() {

        if (!conversacionActualId) {
            return;
        }

        try {
            if (
                conexionChat.state ===
                signalR.HubConnectionState.Connected
            ) {
                await conexionChat.invoke(
                    "LeaveConversation",
                    String(conversacionActualId)
                );
            }
        }
        catch (error) {
            console.error(
                "No fue posible salir de la conversación:",
                error
            );
        }

        conversacionActualId = null;
        usuarioActualIdChat = null;
    }

    async function cargarMensajes(conversacionId) {

        const messagesContainer =
            document.getElementById("chatMessages");

        if (!messagesContainer) {
            return;
        }

        try {
            const response = await fetch(
                `/Chat/GetMessages?conversationId=${encodeURIComponent(
                    conversacionId
                )}`
            );

            const data = await response.json();

            if (!response.ok || !data.success) {
                throw new Error(
                    data.message ||
                    "No fue posible cargar los mensajes."
                );
            }

            usuarioActualIdChat =
                Number(data.currentUserId);

            mostrarMensajes(
                data.messages || [],
                usuarioActualIdChat
            );
        }
        catch (error) {
            console.error(error);

            messagesContainer.innerHTML = `
                <div class="chat-loading">
                    ${escaparHtml(error.message)}
                </div>
            `;
        }
    }

    function mostrarMensajes(
        mensajes,
        usuarioActualId
    ) {
        const messagesContainer =
            document.getElementById("chatMessages");

        if (!messagesContainer) {
            return;
        }

        messagesContainer.innerHTML = "";

        if (!mensajes || mensajes.length === 0) {
            messagesContainer.innerHTML = `
                <div class="chat-empty-conversation">

                    <i class="fa fa-comments"></i>

                    <span>
                        Aún no hay mensajes en esta conversación.
                    </span>

                </div>
            `;

            return;
        }

        mensajes.forEach(function (mensaje) {

            const esMio =
                Number(mensaje.remitenteId) ===
                Number(usuarioActualId);

            messagesContainer.insertAdjacentHTML(
                "beforeend",
                crearMensajeHtml(mensaje, esMio)
            );
        });

        messagesContainer.scrollTop =
            messagesContainer.scrollHeight;
    }

    function crearMensajeHtml(
        mensaje,
        esMio
    ) {
        const fecha = mensaje.fechaEnvio
            ? new Date(
                mensaje.fechaEnvio.endsWith("Z")
                    ? mensaje.fechaEnvio
                    : mensaje.fechaEnvio + "Z"
            )
            : null;

        const hora = fecha && !isNaN(fecha.getTime())
            ? fecha.toLocaleTimeString(
                "es-CR",
                {
                    hour: "2-digit",
                    minute: "2-digit",
                    hour12: true
                }
            )
            : "";

        return `
            <div class="chat-message ${esMio
                ? "chat-message-me"
                : "chat-message-other"
            }">

                <div class="chat-message-bubble">

                    <div class="chat-message-content">
                        ${escaparHtml(mensaje.contenido)}
                    </div>

                    <div class="chat-message-time">
                        ${escaparHtml(hora)}
                    </div>

                </div>

            </div>
        `;
    }

    function mostrarPantallaUsuarios() {

        const chatBody = document.querySelector(
            "#chatWindow .chat-body"
        );

        if (!chatBody) {
            return;
        }

        chatBody.innerHTML = `
            <div class="chat-search">

                <i class="fa fa-search"></i>

                <input type="text"
                       id="chatUserSearch"
                       placeholder="Buscar usuario..."
                       autocomplete="off">

            </div>

            <div id="chatUsersList"
                 class="chat-users-list">

                <div class="chat-loading">
                    Abre el chat para cargar los usuarios.
                </div>

            </div>
        `;
    }

    function mostrarUsuarios(usuarios) {

        const lista =
            document.getElementById("chatUsersList");

        if (!lista) {
            return;
        }

        lista.innerHTML = "";

        if (!usuarios || usuarios.length === 0) {
            lista.innerHTML = `
                <div class="chat-loading">
                    No hay usuarios disponibles.
                </div>
            `;

            return;
        }

        usuarios.forEach(function (usuario) {

            lista.insertAdjacentHTML(
                "beforeend",
                crearUsuarioChat(usuario)
            );
        });
    }

    function crearUsuarioChat(usuario) {

        const nombre =
            usuario.nombreCompleto ||
            usuario.nombre ||
            "Usuario";

        const correo =
            usuario.correo ||
            "Sin correo";

        const inicial =
            nombre.trim().charAt(0).toUpperCase() || "?";

        return `
            <button type="button"
                    class="chat-user-item"
                    data-id="${usuario.usuarioId ?? ""}"
                    data-name="${escaparAtributo(nombre)}"
                    data-email="${escaparAtributo(correo)}">

                <div class="chat-user-avatar">

                    ${escaparHtml(inicial)}

                    <span class="chat-user-status"></span>

                </div>

                <div class="chat-user-info">

                    <span class="chat-user-name">
                        ${escaparHtml(nombre)}
                    </span>

                    <span class="chat-user-email">
                        ${escaparHtml(correo)}
                    </span>

                </div>

                <i class="fa fa-chevron-right chat-user-arrow"></i>

            </button>
        `;
    }

    function configurarBuscador() {

        const buscador =
            document.getElementById("chatUserSearch");

        if (!buscador) {
            return;
        }

        buscador.addEventListener(
            "input",
            function () {

                const texto = this.value
                    .trim()
                    .toLowerCase();

                const usuariosFiltrados =
                    usuariosChat.filter(
                        function (usuario) {

                            const nombre = (
                                usuario.nombreCompleto ||
                                usuario.nombre ||
                                ""
                            ).toLowerCase();

                            const correo = (
                                usuario.correo ||
                                ""
                            ).toLowerCase();

                            return (
                                nombre.includes(texto) ||
                                correo.includes(texto)
                            );
                        }
                    );

                mostrarUsuarios(usuariosFiltrados);
            }
        );
    }

    function escaparHtml(valor) {

        const div = document.createElement("div");

        div.textContent = valor ?? "";

        return div.innerHTML;
    }

    function escaparAtributo(valor) {

        return String(valor ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;");
    }

    async function cargarMensajesDepartamento(perfilId) {

        const contenedor =
            document.getElementById(
                "chatDepartmentMessages"
            );

        if (!contenedor) {
            return;
        }

        try {
            const response = await fetch(
                `/Chat/GetDepartmentMessages?profileId=${encodeURIComponent(
                    perfilId
                )}`
            );

            const data = await response.json();

            if (!response.ok || !data.success) {
                throw new Error(
                    data.message ||
                    "No fue posible cargar los mensajes."
                );
            }

            contenedor.innerHTML = "";

            const mensajes = data.messages || [];

            if (mensajes.length === 0) {
                contenedor.innerHTML = `
                    <div class="chat-empty-conversation">
                        <i class="fa fa-users"></i>

                        <span>
                            Aún no hay mensajes en este departamento.
                        </span>
                    </div>
                `;

                return;
            }

            mensajes.forEach(function (mensaje) {

                const esMio =
                    Number(mensaje.remitenteId) ===
                    Number(data.currentUserId);

                contenedor.insertAdjacentHTML(
                    "beforeend",
                    crearMensajeHtml(
                        mensaje,
                        esMio
                    )
                );
            });

            contenedor.scrollTop =
                contenedor.scrollHeight;
        }
        catch (error) {
            console.error(error);

            contenedor.innerHTML = `
                <div class="chat-loading">
                    ${escaparHtml(error.message)}
                </div>
            `;
        }
    }

    // Antes esta función estaba FUERA del DOMContentLoaded, por lo que
    // no tenía acceso a escaparHtml, modoChatActual ni conexionChat.
    // Ahora vive dentro del mismo closure que el resto del chat.
    async function abrirChatDepartamento(
        perfilId,
        nombre
    ) {
        modoChatActual = "departamento";

        const chatBody = document.querySelector(
            "#chatWindow .chat-body"
        );

        if (!chatBody) {
            return;
        }

        chatBody.innerHTML = `
            <div class="chat-conversation-header">

                <button type="button"
                        id="backToChatUsers"
                        class="chat-back-button"
                        title="Volver">
                    <i class="fa fa-arrow-left"></i>
                </button>

                <div class="chat-conversation-user">

                    <div class="chat-conversation-name">
                        ${escaparHtml(nombre)}
                    </div>

                    <div class="chat-conversation-email">
                        Chat general del departamento
                    </div>

                </div>

            </div>

            <div class="chat-messages"
                 id="chatDepartmentMessages"
                 data-profile-id="${perfilId}">

                <div class="chat-empty-conversation">
                    <i class="fa fa-users"></i>

                    <span>
                        Chat de ${escaparHtml(nombre)}
                    </span>
                </div>

            </div>

            <div class="chat-message-form">

                <input type="text"
                       id="chatDepartmentMessageInput"
                       placeholder="Escribe un comunicado..."
                       maxlength="1000"
                       autocomplete="off">

                <button type="button"
                        id="sendDepartmentMessage"
                        title="Enviar">
                    <i class="fa fa-paper-plane"></i>
                </button>

            </div>
        `;
        await cargarMensajesDepartamento(perfilId);

        const input =
            document.getElementById(
                "chatDepartmentMessageInput"
            );

        if (input) {
            input.focus();
        }
    }

});