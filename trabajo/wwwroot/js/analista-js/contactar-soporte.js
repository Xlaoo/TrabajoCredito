function actualizarContador() {
    let mensajeEl = document.getElementById("mensaje");
    let contadorEl = document.getElementById("contadorMensaje");
    if (mensajeEl && contadorEl) {
        contadorEl.innerText = mensajeEl.value.length;
    }
}

function enviarSoporte(event) {
    if (event) event.preventDefault();

    let mensajeOk = document.getElementById("mensajeOk");
    if (mensajeOk) mensajeOk.classList.add("activo");

    let asunto = document.getElementById("asunto");
    if (asunto) asunto.value = "";
    let tipo = document.getElementById("tipoConsulta");
    if (tipo) tipo.value = "";
    let mensaje = document.getElementById("mensaje");
    if (mensaje) mensaje.value = "";
    let contador = document.getElementById("contadorMensaje");
    if (contador) contador.innerText = "0";

    setTimeout(() => {
        if (mensajeOk) mensajeOk.classList.remove("activo");
    }, 4000);
}

function toggleFaq(elemento) {
    if (elemento) elemento.classList.toggle("abierto");
}

function mostrarMasFaq() {
    let lista = document.querySelector(".faq-list");
    let texto = document.getElementById("textoFaq");
    if (!lista) return;

    lista.classList.toggle("mostrar-todo");

    if (texto) {
        if (lista.classList.contains("mostrar-todo")) {
            texto.innerText = "Ver menos preguntas";
        } else {
            texto.innerText = "Ver todas las preguntas";
        }
    }
}

document.addEventListener("DOMContentLoaded", function () {
    const formSoporte = document.getElementById("formSoporte");
    if (formSoporte) {
        formSoporte.addEventListener("submit", enviarSoporte);
    }

    const mensajeInput = document.getElementById("mensaje");
    if (mensajeInput) {
        mensajeInput.addEventListener("input", actualizarContador);
    }

    const btnFaq = document.getElementById("btnMostrarMasFaq");
    if (btnFaq) {
        btnFaq.addEventListener("click", mostrarMasFaq);
    }

    const faqList = document.querySelector(".faq-list");
    if (faqList) {
        faqList.addEventListener("click", function (e) {
            const item = e.target.closest(".faq-item");
            if (item) toggleFaq(item);
        });
    }

    // ==========================================
    // CHAT SIGNALR CON ADMINISTRADOR
    // ==========================================
    const modalChat = document.getElementById("modalChatAdmin");
    if (modalChat && typeof signalR !== "undefined") {
        const idAdministradorSoporte = parseInt(modalChat.getAttribute("data-admin-id") || "0", 10);
        const idAnalistaSoporte = parseInt(modalChat.getAttribute("data-analista-id") || "0", 10);
        const chatMensajesAdmin = document.getElementById("chatMensajesAdmin");
        const formChatAdmin = document.getElementById("formChatAdmin");
        const mensajeAdminInput = document.getElementById("mensajeAdminInput");
        const btnEnviarAdmin = document.getElementById("btnEnviarAdmin");
        const alertaError = document.getElementById("chatAlertaError");
        const alertaErrorTexto = document.getElementById("chatAlertaErrorTexto");
        const estadoAdminEl = document.getElementById("chatEstadoAdmin");
        const estadoAdminTextoEl = document.getElementById("chatEstadoAdminTexto");

        let enviando = false;

        function mostrarAlertaError(texto) {
            if (alertaError && alertaErrorTexto) {
                alertaErrorTexto.textContent = texto;
                alertaError.classList.remove("d-none");
            }
        }

        function ocultarAlertaError() {
            if (alertaError) {
                alertaError.classList.add("d-none");
            }
        }

        function mostrarValidacionVacio() {
            if (!mensajeAdminInput) return;
            mensajeAdminInput.classList.add("input-invalido");
            mensajeAdminInput.focus();
            setTimeout(() => {
                mensajeAdminInput.classList.remove("input-invalido");
            }, 2000);
        }

        function scrollChatAlFinal() {
            if (chatMensajesAdmin) {
                chatMensajesAdmin.scrollTop = chatMensajesAdmin.scrollHeight;
            }
        }

        async function actualizarEstadoAdmin() {
            try {
                const resp = await fetch("/Analista/EstadoAdminActual", {
                    method: "GET",
                    headers: { "Accept": "application/json" }
                });
                if (!resp.ok) return;
                const data = await resp.json();
                if (estadoAdminEl) {
                    estadoAdminEl.classList.remove("activo", "inactivo");
                    estadoAdminEl.classList.add(data.activo ? "activo" : "inactivo");
                }
                if (estadoAdminTextoEl) {
                    estadoAdminTextoEl.textContent = data.texto || (data.activo ? "Administrador Activo" : "Administrador Inactivo");
                }
            } catch (err) {
                // Error de red ignorado silenciosamente
            }
        }

        let connectionAdmin = new signalR.HubConnectionBuilder()
            .withUrl("/chatAnalistaHub")
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .build();

        connectionAdmin.on("RecibirMensaje", function (data) {
            const idMensaje = data.idMensaje || data.IdMensaje;
            const remitente = data.remitenteRol || data.RemitenteRol || "Analista";
            const mensaje = data.mensaje || data.Mensaje || "";
            const fecha = data.fechaEnvio || data.FechaEnvio || new Date().toLocaleString();

            if (chatMensajesAdmin) {
                if (idMensaje && chatMensajesAdmin.querySelector(`[data-id-mensaje="${idMensaje}"]`)) {
                    return;
                }

                const clase = remitente === "Analista" ? "analista" : "admin";

                const divMsg = document.createElement("div");
                divMsg.className = "chat-message " + clase;
                if (idMensaje) {
                    divMsg.setAttribute("data-id-mensaje", idMensaje);
                }

                const divBubble = document.createElement("div");
                divBubble.className = "chat-bubble";
                divBubble.textContent = mensaje;

                const divMeta = document.createElement("div");
                divMeta.className = "chat-meta";
                divMeta.textContent = remitente + " · " + fecha;

                divMsg.appendChild(divBubble);
                divMsg.appendChild(divMeta);
                chatMensajesAdmin.appendChild(divMsg);

                scrollChatAlFinal();
            }
        });

        connectionAdmin.onreconnecting(function () {
            mostrarAlertaError("Reconectando con el servidor de chat...");
        });

        connectionAdmin.onreconnected(async function () {
            try {
                await connectionAdmin.invoke("UnirseConversacion", idAdministradorSoporte, idAnalistaSoporte);
                ocultarAlertaError();
            } catch (err) {
                console.error("Error al reincorporarse al grupo:", err);
            }
        });

        connectionAdmin.onclose(function () {
            mostrarAlertaError("Conexión con el chat interrumpida. Reintentando al interactuar...");
        });

        async function iniciarSignalR() {
            try {
                await connectionAdmin.start();
                await connectionAdmin.invoke("UnirseConversacion", idAdministradorSoporte, idAnalistaSoporte);
                ocultarAlertaError();
            } catch (err) {
                console.error("SignalR connection error:", err);
                mostrarAlertaError("No se pudo conectar al chat en tiempo real. Se reintentará automáticamente.");
            }
        }

        iniciarSignalR();

        async function enviarMensajeAdmin(e) {
            if (e) e.preventDefault();
            if (enviando) return;

            if (!mensajeAdminInput) return;

            const texto = mensajeAdminInput.value.trim();
            if (!texto) {
                mostrarValidacionVacio();
                return;
            }

            if (!connectionAdmin || connectionAdmin.state !== signalR.HubConnectionState.Connected) {
                mostrarAlertaError("El chat no está conectado aún. Espera unos segundos e intenta nuevamente.");
                try {
                    if (connectionAdmin && connectionAdmin.state === signalR.HubConnectionState.Disconnected) {
                        await connectionAdmin.start();
                        await connectionAdmin.invoke("UnirseConversacion", idAdministradorSoporte, idAnalistaSoporte);
                        ocultarAlertaError();
                    }
                } catch (reconnectErr) {
                    return;
                }
                if (connectionAdmin.state !== signalR.HubConnectionState.Connected) {
                    return;
                }
            }

            try {
                enviando = true;
                if (btnEnviarAdmin) btnEnviarAdmin.disabled = true;

                await connectionAdmin.invoke(
                    "EnviarMensaje",
                    idAdministradorSoporte,
                    idAnalistaSoporte,
                    texto
                );

                mensajeAdminInput.value = "";
                ocultarAlertaError();
            } catch (err) {
                console.error("Error al enviar mensaje SignalR:", err);
                mostrarAlertaError("No se pudo enviar el mensaje. Conservamos tu texto para que puedas reintentar.");
            } finally {
                enviando = false;
                if (btnEnviarAdmin) btnEnviarAdmin.disabled = false;
                mensajeAdminInput.focus();
            }
        }

        if (formChatAdmin) {
            formChatAdmin.addEventListener("submit", enviarMensajeAdmin);
        }

        if (btnEnviarAdmin) {
            btnEnviarAdmin.addEventListener("click", function (e) {
                enviarMensajeAdmin(e);
            });
        }

        if (mensajeAdminInput) {
            mensajeAdminInput.addEventListener("keydown", function (e) {
                if (e.key === "Enter" && !e.shiftKey) {
                    e.preventDefault();
                    enviarMensajeAdmin(e);
                }
            });

            mensajeAdminInput.addEventListener("input", function () {
                mensajeAdminInput.classList.remove("input-invalido");
            });
        }

        modalChat.addEventListener("shown.bs.modal", function () {
            scrollChatAlFinal();
            if (mensajeAdminInput) mensajeAdminInput.focus();
            actualizarEstadoAdmin();
        });

        setInterval(actualizarEstadoAdmin, 15000);
    }
});
