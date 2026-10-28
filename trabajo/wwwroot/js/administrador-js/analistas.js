// Analistas - Panel Administrador CrediPlus
document.addEventListener("DOMContentLoaded", () => {
    const datosEl = document.getElementById("analistasAdminDatos");
    const idAdministrador = datosEl ? parseInt(datosEl.dataset.idAdmin || "0", 10) : 0;
    const idAnalista = datosEl ? parseInt(datosEl.dataset.idAnalista || "0", 10) : 0;
    const remitenteRol = "Administrador";
    const estadoAnalistaUrl = datosEl ? (datosEl.dataset.estadoUrl || "") : "";

    const chatMensajes = document.getElementById("chatMensajes");
    const formChat = document.getElementById("formChatAnalista");
    const mensajeInput = document.getElementById("mensajeInput");
    const btnEnviar = document.getElementById("btnEnviarMensaje");
    const estadoAnalistaBadge = document.getElementById("estadoAnalistaBadge");
    const ultimaConexionAnalista = document.getElementById("ultimaConexionAnalista");
    const chatEstadoAnalista = document.getElementById("chatEstadoAnalista");

    let connection = null;

    function enfocarChat() {
        const panel = document.getElementById("panelChatAnalista");
        if (panel) {
            panel.scrollIntoView({ behavior: "smooth", block: "center" });
        }
        if (mensajeInput && !mensajeInput.disabled) {
            setTimeout(() => mensajeInput.focus(), 450);
        }
    }

    function scrollChatFinal() {
        if (chatMensajes) {
            chatMensajes.scrollTop = chatMensajes.scrollHeight;
        }
    }

    function agregarMensaje(remitente, mensaje, fechaEnvio) {
        if (!chatMensajes) return;

        const empty = document.getElementById("chatVacio");
        if (empty) empty.remove();

        const clase = remitente === "Administrador" ? "admin" : "analista";

        const contenedor = document.createElement("div");
        contenedor.className = "chat-message " + clase;

        const burbuja = document.createElement("div");
        burbuja.className = "chat-bubble";
        burbuja.textContent = mensaje;

        const meta = document.createElement("div");
        meta.className = "chat-meta";
        meta.textContent = remitente + " · " + fechaEnvio;

        contenedor.appendChild(burbuja);
        contenedor.appendChild(meta);
        chatMensajes.appendChild(contenedor);

        scrollChatFinal();
    }

    function cambiarClaseEstado(elemento, conectado) {
        if (!elemento) return;
        elemento.classList.remove("activo", "inactivo");
        elemento.classList.add(conectado ? "activo" : "inactivo");
    }

    function aplicarEstadoAnalista(conectado, texto, ultimaConexion) {
        const textoFinal = texto || (conectado ? "Activo" : "Inactivo");

        cambiarClaseEstado(estadoAnalistaBadge, conectado);
        cambiarClaseEstado(chatEstadoAnalista, conectado);

        const estadoTexto = estadoAnalistaBadge?.querySelector(".estado-texto");
        if (estadoTexto) {
            estadoTexto.textContent = textoFinal;
        }

        const chatTexto = chatEstadoAnalista?.querySelector(".chat-estado-texto");
        if (chatTexto) {
            chatTexto.textContent = textoFinal;
        }

        if (ultimaConexionAnalista) {
            ultimaConexionAnalista.textContent = ultimaConexion
                ? "Última conexión: " + ultimaConexion
                : "Estado actualizado en tiempo real";
        }
    }

    async function actualizarEstadoAnalista() {
        if (!idAnalista || idAnalista <= 0 || !estadoAnalistaUrl) {
            return;
        }

        try {
            const respuesta = await fetch(`${estadoAnalistaUrl}?idAnalista=${encodeURIComponent(idAnalista)}`, {
                method: "GET",
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json"
                }
            });

            if (!respuesta.ok) return;

            const data = await respuesta.json();
            aplicarEstadoAnalista(data.conectado === true, data.texto, data.ultimaConexion);
        } catch (error) {
            console.log("No se pudo consultar el estado del analista.", error);
        }
    }

    async function iniciarChatTiempoReal() {
        if (!idAdministrador || !idAnalista || idAdministrador <= 0 || idAnalista <= 0) {
            return;
        }

        if (!window.signalR) {
            console.error("SignalR no cargó correctamente.");
            return;
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl("/chatAnalistaHub")
            .withAutomaticReconnect()
            .build();

        connection.on("RecibirMensaje", function (data) {
            const remitente = data.remitenteRol || data.RemitenteRol || "Analista";
            const mensaje = data.mensaje || data.Mensaje || "";
            const fechaEnvio = data.fechaEnvio || data.FechaEnvio || "Ahora";

            agregarMensaje(remitente, mensaje, fechaEnvio);
        });

        connection.onreconnecting(function () {
            if (btnEnviar) btnEnviar.disabled = true;
        });

        connection.onreconnected(async function () {
            try {
                await connection.invoke("UnirseConversacion", idAdministrador, idAnalista);
                if (btnEnviar) btnEnviar.disabled = false;
            } catch (error) {
                console.error("Error al reconectar al grupo:", error);
            }
        });

        connection.onclose(function () {
            if (btnEnviar) btnEnviar.disabled = true;
        });

        try {
            await connection.start();
            await connection.invoke("UnirseConversacion", idAdministrador, idAnalista);

            if (btnEnviar) btnEnviar.disabled = false;
        } catch (error) {
            console.error("Error SignalR:", error);
        }
    }

    if (formChat) {
        formChat.addEventListener("submit", async function (event) {
            event.preventDefault();

            if (!mensajeInput || !btnEnviar) return;

            const texto = mensajeInput.value.trim();
            if (!texto) return;

            if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
                alert("El chat todavía no está conectado. Espera unos segundos e intenta de nuevo.");
                return;
            }

            btnEnviar.disabled = true;

            try {
                await connection.invoke("EnviarMensaje", idAdministrador, idAnalista, texto);
                mensajeInput.value = "";
            } catch (error) {
                console.error("Error enviando mensaje:", error);
                alert("No se pudo enviar el mensaje. Revisa la conexión del chat.");
            } finally {
                btnEnviar.disabled = false;
                mensajeInput.focus();
            }
        });
    }

    scrollChatFinal();
    actualizarEstadoAnalista();
    iniciarChatTiempoReal();

    setInterval(actualizarEstadoAnalista, 10000);
});
