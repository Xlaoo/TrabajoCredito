let solicitudSeleccionada = 0;
let montoSeleccionado = 0;
let solicitudEditando = 0;
let huboCambio = false;
let montoOriginal = {};
let plazoOriginal = {};
let solicitudActual = 0;
let imagenSeleccionada = null;

function getCsrfToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
}

function marcarCambio() {
    huboCambio = true;
}

document.addEventListener("DOMContentLoaded", function () {
    // Entradas editables
    document.querySelectorAll(".input-edicion, .select-edicion").forEach(function (el) {
        el.addEventListener("input", marcarCambio);
        el.addEventListener("change", marcarCambio);
    });

    // Botones de acción en tarjetas de solicitud
    document.querySelectorAll(".btn-borrar-solicitud").forEach(function (btn) {
        btn.addEventListener("click", function () {
            const id = Number(this.dataset.idSolicitud);
            borrarSolicitud(id);
        });
    });

    document.querySelectorAll(".btn-ver-observacion").forEach(function (btn) {
        btn.addEventListener("click", function () {
            verObservacionBoton(this);
        });
    });

    document.querySelectorAll(".btn-editar-solicitud").forEach(function (btn) {
        btn.addEventListener("click", function () {
            const id = Number(this.dataset.idSolicitud);
            const estado = this.dataset.estado || "";
            editarSolicitud(id, estado);
        });
    });

    document.querySelectorAll(".btn-cancelar-solicitud").forEach(function (btn) {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            const id = Number(this.dataset.idSolicitud || this.getAttribute("data-id-solicitud"));
            const estado = this.dataset.estado || this.getAttribute("data-estado") || "";
            const monto = this.dataset.monto || this.getAttribute("data-monto") || "0";
            cancelarSolicitud(id, estado, monto);
        });
    });

    document.addEventListener("click", function (e) {
        const btn = e.target.closest(".btn-cancelar-solicitud");
        if (btn) {
            e.preventDefault();
            const id = Number(btn.dataset.idSolicitud || btn.getAttribute("data-id-solicitud"));
            const estado = btn.dataset.estado || btn.getAttribute("data-estado") || "";
            const monto = btn.dataset.monto || btn.getAttribute("data-monto") || "0";
            cancelarSolicitud(id, estado, monto);
        }
    });

    document.querySelectorAll(".btn-guardar-edicion").forEach(function (btn) {
        btn.addEventListener("click", function () {
            const id = Number(this.dataset.idSolicitud);
            guardarEdicion(id);
        });
    });

    document.querySelectorAll(".btn-cancelar-cambios").forEach(function (btn) {
        btn.addEventListener("click", function () {
            const id = Number(this.dataset.idSolicitud);
            cancelarCambios(id);
        });
    });

    // Enlace para botón abrir chat
    document.querySelectorAll(".btn-abrir-chat").forEach(function (btn) {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            const id = Number(this.dataset.idSolicitud || this.getAttribute("data-id-solicitud"));
            abrirChat(id);
        });
    });

    document.addEventListener("click", function (e) {
        const btn = e.target.closest(".btn-abrir-chat");
        if (btn) {
            e.preventDefault();
            const id = Number(btn.dataset.idSolicitud || btn.getAttribute("data-id-solicitud"));
            abrirChat(id);
        }
    });

    // Modales y botones de cierre
    document.querySelectorAll(".btn-cerrar-observacion").forEach(function (btn) {
        btn.addEventListener("click", cerrarObservacion);
    });

    const btnCerrarChat = document.getElementById("btnCerrarChat");
    if (btnCerrarChat) {
        btnCerrarChat.addEventListener("click", cerrarChat);
    }

    const modalChatEl = document.getElementById("modalChat");
    if (modalChatEl) {
        modalChatEl.addEventListener("click", function (e) {
            if (e.target === modalChatEl) {
                cerrarChat();
            }
        });
    }

    const modalPagoEl = document.getElementById("modalPago");
    if (modalPagoEl) {
        modalPagoEl.addEventListener("click", function (e) {
            if (e.target === modalPagoEl) {
                cerrarModalPago();
            }
        });
    }

    const modalEvalEl = document.getElementById("modalCancelacionEvaluacion");
    if (modalEvalEl) {
        modalEvalEl.addEventListener("click", function (e) {
            if (e.target === modalEvalEl) {
                cerrarModalEvaluacion();
            }
        });
    }

    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape") {
            cerrarChat();
            cerrarModalEvaluacion();
            cerrarModalPago();
            cerrarObservacion();
        }
    });

    const btnAdjuntar = document.getElementById("btnAdjuntarImagen");
    const fileImagen = document.getElementById("fileImagen");
    if (btnAdjuntar && fileImagen) {
        btnAdjuntar.addEventListener("click", function () {
            fileImagen.click();
        });

        fileImagen.addEventListener("change", function () {
            if (this.files.length > 0) {
                imagenSeleccionada = this.files[0];
                const previewDiv = document.getElementById("nombreArchivoSeleccionado");
                if (previewDiv) {
                    previewDiv.innerHTML = `
                        <div class="archivo-preview">
                            📷 ${imagenSeleccionada.name}
                            <button type="button" id="btnQuitarImagen">✕</button>
                        </div>
                    `;
                    document.getElementById("btnQuitarImagen")?.addEventListener("click", quitarImagen);
                }
            }
        });
    }

    const btnEnviarMensaje = document.getElementById("btnEnviarMensaje");
    if (btnEnviarMensaje) {
        btnEnviarMensaje.addEventListener("click", enviarMensaje);
    }

    const txtMensaje = document.getElementById("txtMensaje");
    if (txtMensaje) {
        txtMensaje.addEventListener("keydown", function (e) {
            if (e.key === "Enter") {
                e.preventDefault();
                enviarMensaje();
            }
        });
    }

    // Modal de cancelación de solicitud aprobada
    const metodoPagoSelect = document.getElementById("metodoPago");
    if (metodoPagoSelect) {
        metodoPagoSelect.addEventListener("change", mostrarDatosPago);
    }

    const btnCerrarModalPago = document.getElementById("btnCerrarModalPago");
    if (btnCerrarModalPago) {
        btnCerrarModalPago.addEventListener("click", cerrarModalPago);
    }

    const btnConfirmarPagoCanc = document.getElementById("btnConfirmarPagoCancelacion");
    if (btnConfirmarPagoCanc) {
        btnConfirmarPagoCanc.addEventListener("click", confirmarPagoCancelacion);
    }

    // Modal de cancelación en evaluación
    const btnCerrarModalEval = document.getElementById("btnCerrarModalEvaluacion");
    if (btnCerrarModalEval) {
        btnCerrarModalEval.addEventListener("click", cerrarModalEvaluacion);
    }

    const btnConfirmarEval = document.getElementById("btnConfirmarCancelacionEvaluacion");
    if (btnConfirmarEval) {
        btnConfirmarEval.addEventListener("click", confirmarCancelacionEvaluacion);
    }

    // Delegación en imágenes de chat
    const chatMensajes = document.getElementById("chatMensajes");
    if (chatMensajes) {
        chatMensajes.addEventListener("click", function (e) {
            if (e.target && e.target.classList.contains("imagen-chat")) {
                const url = e.target.dataset.imgUrl || e.target.getAttribute("src");
                if (url) verImagen(url);
            }
        });
    }

    // Delegación para input de código de operación en modal de pago
    const datosPagoDiv = document.getElementById("datosPago");
    if (datosPagoDiv) {
        datosPagoDiv.addEventListener("input", function (e) {
            if (e.target && e.target.id === "codigoOperacion") {
                e.target.value = e.target.value.replace(/[^0-9]/g, "");
            }
        });
    }

    // Advertencia de cambios sin guardar al navegar
    document.querySelectorAll(".menu a").forEach(function (link) {
        link.addEventListener("click", function (e) {
            if (solicitudEditando !== 0 && huboCambio) {
                let salir = confirm("¿Deseas salir sin guardar los cambios?");
                if (!salir) {
                    e.preventDefault();
                }
            }
        });
    });
});

function editarSolicitud(idSolicitud, estado) {
    if (estado === "Pendiente cancelación") {
        alert("No puedes editar esta solicitud porque ya está en proceso de cancelación.");
        return;
    }

    solicitudEditando = idSolicitud;
    huboCambio = false;

    const montoInput = document.getElementById("montoInput_" + idSolicitud);
    const plazoInput = document.getElementById("plazoInput_" + idSolicitud);
    const montoTexto = document.getElementById("montoTexto_" + idSolicitud);
    const plazoTexto = document.getElementById("plazoTexto_" + idSolicitud);
    const btnGuardar = document.getElementById("btnGuardar_" + idSolicitud);
    const btnCancelarCambios = document.getElementById("btnCancelarCambios_" + idSolicitud);
    const btnCancelar = document.getElementById("btnCancelar_" + idSolicitud);
    const btnCronograma = document.getElementById("btnCronograma_" + idSolicitud);
    const btnObservacion = document.getElementById("btnObservacion_" + idSolicitud);

    if (montoInput) montoOriginal[idSolicitud] = montoInput.value;
    if (plazoInput) plazoOriginal[idSolicitud] = plazoInput.value;

    if (montoTexto) montoTexto.classList.add("oculto");
    if (plazoTexto) plazoTexto.classList.add("oculto");

    if (montoInput) montoInput.classList.remove("oculto");
    if (plazoInput) plazoInput.classList.remove("oculto");

    if (btnGuardar) btnGuardar.classList.remove("oculto");
    if (btnCancelarCambios) btnCancelarCambios.classList.remove("oculto");

    if (btnCancelar) {
        btnCancelar.disabled = true;
        btnCancelar.classList.add("btn-disabled");
    }

    if (btnCronograma) {
        btnCronograma.disabled = true;
        btnCronograma.classList.add("btn-disabled");
    }

    if (btnObservacion) {
        btnObservacion.disabled = true;
        btnObservacion.classList.add("bloqueado");
    }
}

function cancelarCambios(idSolicitud) {
    if (huboCambio) {
        let confirmar = confirm("¿Deseas cancelar los cambios realizados?");
        if (!confirmar) return;
    }

    const montoInput = document.getElementById("montoInput_" + idSolicitud);
    const plazoInput = document.getElementById("plazoInput_" + idSolicitud);
    const montoTexto = document.getElementById("montoTexto_" + idSolicitud);
    const plazoTexto = document.getElementById("plazoTexto_" + idSolicitud);
    const btnGuardar = document.getElementById("btnGuardar_" + idSolicitud);
    const btnCancelarCambios = document.getElementById("btnCancelarCambios_" + idSolicitud);
    const btnCancelar = document.getElementById("btnCancelar_" + idSolicitud);
    const btnCronograma = document.getElementById("btnCronograma_" + idSolicitud);
    const btnObservacion = document.getElementById("btnObservacion_" + idSolicitud);

    if (montoInput && montoOriginal[idSolicitud] !== undefined) {
        montoInput.value = montoOriginal[idSolicitud];
    }
    if (plazoInput && plazoOriginal[idSolicitud] !== undefined) {
        plazoInput.value = plazoOriginal[idSolicitud];
    }

    if (montoTexto) montoTexto.classList.remove("oculto");
    if (plazoTexto) plazoTexto.classList.remove("oculto");

    if (montoInput) montoInput.classList.add("oculto");
    if (plazoInput) plazoInput.classList.add("oculto");

    if (btnGuardar) btnGuardar.classList.add("oculto");
    if (btnCancelarCambios) btnCancelarCambios.classList.add("oculto");

    if (btnCancelar) {
        btnCancelar.disabled = false;
        btnCancelar.classList.remove("btn-disabled");
    }

    if (btnCronograma) {
        btnCronograma.disabled = false;
        btnCronograma.classList.remove("btn-disabled");
    }

    if (btnObservacion) {
        btnObservacion.disabled = false;
        btnObservacion.classList.remove("bloqueado");
    }

    solicitudEditando = 0;
    huboCambio = false;
}

function guardarEdicion(idSolicitud) {
    const nuevoMonto = document.getElementById("montoInput_" + idSolicitud)?.value;
    const nuevoPlazo = document.getElementById("plazoInput_" + idSolicitud)?.value;

    if (nuevoMonto < 1000) {
        alert("El monto mínimo permitido es S/ 1,000.");
        return;
    }

    if (nuevoMonto > 50000) {
        alert("El monto máximo permitido es S/ 50,000.");
        return;
    }

    const csrfToken = getCsrfToken();
    const params = new URLSearchParams();
    params.append("idSolicitud", idSolicitud);
    params.append("nuevoMonto", nuevoMonto);
    params.append("nuevoPlazo", nuevoPlazo);
    if (csrfToken) params.append("__RequestVerificationToken", csrfToken);

    const headers = { "Content-Type": "application/x-www-form-urlencoded" };
    if (csrfToken) headers["RequestVerificationToken"] = csrfToken;

    fetch("/Login/EditarSolicitud", {
        method: "POST",
        headers: headers,
        body: params.toString()
    })
    .then(response => response.json())
    .then(data => {
        alert(data.mensaje);
        if (data.ok) {
            location.reload();
        }
    })
    .catch(() => {
        alert("No se pudo guardar la edición.");
    });
}

function cancelarSolicitud(idSolicitud, estado, monto) {
    if (!idSolicitud) return;
    solicitudSeleccionada = idSolicitud;
    montoSeleccionado = monto;

    const estadoRaw = (estado || "").trim().toLowerCase();
    const estadoNorm = estadoRaw.normalize("NFD").replace(/[\u0300-\u036f]/g, "");

    if (estadoNorm === "pendiente" || estadoNorm === "pendiente cancelacion") {
        alert("No puedes cancelar esta solicitud.");
        return;
    }

    if (estadoNorm === "en evaluacion") {
        const motivoEval = document.getElementById("motivoEvaluacion");
        if (motivoEval) motivoEval.value = "";

        const modalEval = document.getElementById("modalCancelacionEvaluacion");
        if (modalEval) {
            modalEval.style.display = "flex";
            modalEval.classList.add("activo");
            modalEval.classList.remove("oculto");
        }
        return;
    }

    if (estadoNorm === "aprobado") {
        const montoDevolver = document.getElementById("montoDevolver");
        if (montoDevolver) {
            const num = Number(monto);
            montoDevolver.value = "S/ " + (isNaN(num) ? monto : num.toLocaleString("es-PE"));
        }

        const modalPago = document.getElementById("modalPago");
        if (modalPago) {
            modalPago.style.display = "flex";
            modalPago.classList.add("activo");
            modalPago.classList.remove("oculto");
        }
        return;
    }
}

function cerrarModalEvaluacion() {
    const modal = document.getElementById("modalCancelacionEvaluacion");
    if (modal) {
        modal.style.display = "none";
        modal.classList.remove("activo");
        modal.classList.add("oculto");
    }
    const motivoEval = document.getElementById("motivoEvaluacion");
    if (motivoEval) motivoEval.value = "";
}

function confirmarCancelacionEvaluacion() {
    let motivo = document.getElementById("motivoEvaluacion")?.value.trim() || "";

    if (motivo === "") {
        Swal.fire({
            icon: "warning",
            title: "Motivo obligatorio",
            text: "Debe ingresar el motivo de cancelación.",
            confirmButtonColor: "#6d28d9"
        });
        return;
    }

    const csrfToken = getCsrfToken();
    const params = new URLSearchParams();
    params.append("idSolicitud", solicitudSeleccionada);
    params.append("motivo", motivo);
    if (csrfToken) params.append("__RequestVerificationToken", csrfToken);

    const headers = { "Content-Type": "application/x-www-form-urlencoded" };
    if (csrfToken) headers["RequestVerificationToken"] = csrfToken;

    fetch("/Login/CancelarEvaluacion", {
        method: "POST",
        headers: headers,
        body: params.toString()
    })
    .then(response => response.json())
    .then(data => {
        if (data.ok) {
            Swal.fire({
                icon: "success",
                title: "Solicitud cancelada",
                text: data.mensaje,
                confirmButtonColor: "#16a34a"
            }).then(() => {
                location.reload();
            });
        } else {
            Swal.fire({
                icon: "error",
                title: "Error",
                text: data.mensaje,
                confirmButtonColor: "#dc2626"
            });
        }
    })
    .catch(() => {
        Swal.fire({
            icon: "error",
            title: "Error",
            text: "No se pudo cancelar la solicitud.",
            confirmButtonColor: "#dc2626"
        });
    });
}

function cerrarModalPago() {
    const modal = document.getElementById("modalPago");
    if (modal) {
        modal.style.display = "none";
        modal.classList.remove("activo");
        modal.classList.add("oculto");
    }

    const metodo = document.getElementById("metodoPago");
    if (metodo) metodo.value = "";

    const datos = document.getElementById("datosPago");
    if (datos) {
        datos.style.display = "none";
        datos.classList.add("oculto");
        datos.innerHTML = "";
    }

    const motivo = document.getElementById("motivoCancelacion");
    if (motivo) motivo.value = "";
}

function confirmarPagoCancelacion() {
    let metodo = document.getElementById("metodoPago")?.value || "";
    let codigo = document.getElementById("codigoOperacion")?.value || "";
    let motivo = document.getElementById("motivoCancelacion")?.value.trim() || "";

    if (metodo === "") {
        alert("Debe seleccionar un método de pago.");
        return;
    }

    if (!codigo) {
        alert("Debe ingresar el código de operación.");
        return;
    }

    if (motivo === "") {
        alert("Debe ingresar el motivo de cancelación.");
        return;
    }

    const csrfToken = getCsrfToken();
    const params = new URLSearchParams();
    params.append("idSolicitud", solicitudSeleccionada);
    params.append("montoDevuelto", montoSeleccionado);
    params.append("metodoPago", metodo);
    params.append("codigoOperacion", codigo);
    params.append("motivoCancelacion", motivo);
    if (csrfToken) params.append("__RequestVerificationToken", csrfToken);

    const headers = { "Content-Type": "application/x-www-form-urlencoded" };
    if (csrfToken) headers["RequestVerificationToken"] = csrfToken;

    fetch("/Login/RegistrarCancelacion", {
        method: "POST",
        headers: headers,
        body: params.toString()
    })
    .then(response => response.json())
    .then(data => {
        alert(data.mensaje);

        const btnEditar = document.getElementById("btnEditar_" + solicitudSeleccionada);
        const btnCronograma = document.getElementById("btnCronograma_" + solicitudSeleccionada);
        const btnCancelar = document.getElementById("btnCancelar_" + solicitudSeleccionada);

        if (btnEditar) {
            btnEditar.disabled = true;
            btnEditar.classList.add("btn-disabled");
        }
        if (btnCronograma) {
            btnCronograma.disabled = true;
            btnCronograma.classList.add("btn-disabled");
        }
        if (btnCancelar) {
            btnCancelar.disabled = true;
            btnCancelar.classList.add("btn-disabled");
        }

        location.reload();
    })
    .catch(() => {
        alert("No se pudo registrar la cancelación.");
    });
}

function mostrarDatosPago() {
    let metodoSelect = document.getElementById("metodoPago");
    let metodo = metodoSelect ? metodoSelect.value : "";
    let datos = document.getElementById("datosPago");

    if (!datos) return;

    if (metodo === "") {
        datos.style.display = "none";
        datos.classList.add("oculto");
        datos.innerHTML = "";
        return;
    }

    datos.style.display = "block";
    datos.classList.remove("oculto");
    datos.innerHTML = `
        <div class="pago-info">
            <h4>Pago por ${metodo}</h4>
            <p><b>Empresa:</b> CrediPlus Financiera</p>
            <p><b>Referencia:</b> Devolución total del préstamo.</p>
            <input id="codigoOperacion"
                   type="text"
                   maxlength="20"
                   placeholder="Ingrese código de operación">
        </div>`;
}

function verObservacionBoton(boton) {
    let texto = boton.getAttribute("data-observacion");
    if (!texto || texto.trim() === "") {
        texto = "Sin observaciones del analista.";
    }

    const txtObservacion = document.getElementById("textoObservacion");
    if (txtObservacion) txtObservacion.value = texto;

    const modalObs = document.getElementById("modalObservacion");
    if (modalObs) modalObs.classList.remove("oculto");
}

function cerrarObservacion() {
    const modal = document.getElementById("modalObservacion");
    if (modal) modal.classList.add("oculto");
}

function borrarSolicitud(idSolicitud) {
    if (!confirm("¿Seguro que deseas borrar esta solicitud rechazada?")) {
        return;
    }

    const csrfToken = getCsrfToken();
    const params = new URLSearchParams();
    params.append("idSolicitud", idSolicitud);
    if (csrfToken) params.append("__RequestVerificationToken", csrfToken);

    const headers = { "Content-Type": "application/x-www-form-urlencoded" };
    if (csrfToken) headers["RequestVerificationToken"] = csrfToken;

    fetch("/Login/BorrarSolicitudRechazada", {
        method: "POST",
        headers: headers,
        body: params.toString()
    })
    .then(response => response.json())
    .then(data => {
        alert(data.mensaje);
        location.reload();
    })
    .catch(() => {
        alert("No se pudo borrar la solicitud.");
    });
}

function mostrarAviso(texto) {
    Swal.fire({
        icon: "warning",
        title: "Atención",
        text: texto,
        confirmButtonText: "Entendido",
        confirmButtonColor: "#6d28d9",
        allowOutsideClick: false
    });
}

function abrirChat(idSolicitud) {
    if (!idSolicitud) return;
    solicitudActual = idSolicitud;
    const modalChat = document.getElementById("modalChat");
    if (modalChat) {
        modalChat.style.display = "flex";
        modalChat.classList.add("activo");
        modalChat.classList.remove("oculto");
    }

    cargarInfoSolicitud(idSolicitud);
    cargarMensajes(idSolicitud);
}

function cargarInfoSolicitud(idSolicitud) {
    fetch("/Login/ObtenerInfoSolicitud?idSolicitud=" + idSolicitud)
        .then(r => r.json())
        .then(data => {
            const tit = document.getElementById("tituloSolicitud");
            const analista = document.getElementById("chatAnalista");
            const correo = document.getElementById("chatCorreo");

            if (tit) tit.textContent = "Mensajes - Analista";
            if (analista) analista.textContent = data.analista || "-";
            if (correo) correo.textContent = data.correo || "-";
        })
        .catch(err => {
            console.error("Error al obtener info de solicitud:", err);
        });
}

function quitarImagen() {
    imagenSeleccionada = null;
    const file = document.getElementById("fileImagen");
    if (file) file.value = "";
    const nameEl = document.getElementById("nombreArchivoSeleccionado");
    if (nameEl) nameEl.innerHTML = "";
}

function enviarMensaje() {
    let txtMensaje = document.getElementById("txtMensaje");
    let mensaje = txtMensaje ? txtMensaje.value.trim() : "";

    if (mensaje === "" && imagenSeleccionada == null) {
        mostrarAviso("Debe escribir un mensaje o adjuntar una imagen.");
        return;
    }

    let formData = new FormData();
    formData.append("idSolicitud", solicitudActual);
    formData.append("mensaje", mensaje);

    if (imagenSeleccionada != null) {
        formData.append("imagen", imagenSeleccionada);
    }

    const csrfToken = getCsrfToken();
    if (csrfToken) {
        formData.append("__RequestVerificationToken", csrfToken);
    }

    const headers = {};
    if (csrfToken) {
        headers["RequestVerificationToken"] = csrfToken;
    }

    fetch("/Login/EnviarMensaje", {
        method: "POST",
        headers: headers,
        body: formData
    })
    .then(r => r.json())
    .then(data => {
        if (txtMensaje) txtMensaje.value = "";
        quitarImagen();
        cargarMensajes(solicitudActual);
    })
    .catch(err => {
        console.error("Error al enviar mensaje:", err);
        alert("Error al enviar mensaje");
    });
}

function verImagen(url) {
    Swal.fire({
        imageUrl: url,
        imageAlt: "Imagen",
        showConfirmButton: false,
        showCloseButton: true,
        width: "80%"
    });
}

function cargarMensajes(idSolicitud) {
    fetch("/Login/ObtenerMensajes?idSolicitud=" + idSolicitud)
        .then(r => r.json())
        .then(data => {
            let html = "";

            if (!data || !Array.isArray(data) || data.length === 0) {
                html = `
                <div class="chat-vacio">
                    <i class="fas fa-comments"></i>
                    <h4>No hay mensajes todavía</h4>
                    <p>Inicia una conversación con el analista</p>
                </div>`;
            } else {
                data.forEach(function (m) {
                    const esUsuario = m.tipoUsuario === "Usuario";
                    const nombre = m.nombre || (esUsuario ? "Cliente" : "Analista");
                    const fecha = m.fecha ? (m.fecha.includes("T") ? m.fecha.replace("T", " ").substring(0, 16) : m.fecha) : "";
                    const tieneImagen = !!m.imagen;
                    const textoValido = m.mensaje && m.mensaje !== "[Imagen]";

                    if (esUsuario) {
                        html += `
                        <div class="mensaje-analista">
                            <div class="chat-derecha">
                                <div class="cabecera-derecha">
                                    <span class="nombre-chat">${nombre}</span>
                                    <div class="avatar-chat">
                                        <i class="fas fa-user-circle"></i>
                                    </div>
                                </div>
                                <div class="${tieneImagen ? 'imagen-contenedor' : 'burbuja-analista'}">
                                    ${tieneImagen ? `<img src="${m.imagen}" data-img-url="${m.imagen}" class="imagen-chat" alt="Adjunto" />` : ''}
                                    ${textoValido ? `<div class="texto-chat">${m.mensaje}</div>` : ''}
                                </div>
                                <div class="fecha-chat fecha-derecha">${fecha}</div>
                            </div>
                        </div>`;
                    } else {
                        html += `
                        <div class="mensaje-cliente">
                            <div class="avatar-chat">
                                <i class="fas fa-user-circle"></i>
                            </div>
                            <div>
                                <div class="nombre-chat">${nombre}</div>
                                <div class="${tieneImagen ? 'imagen-contenedor' : 'burbuja-cliente'}">
                                    ${tieneImagen ? `<img src="${m.imagen}" data-img-url="${m.imagen}" class="imagen-chat" alt="Adjunto" />` : ''}
                                    ${textoValido ? `<div class="texto-chat">${m.mensaje}</div>` : ''}
                                </div>
                                <div class="fecha-chat">${fecha}</div>
                            </div>
                        </div>`;
                    }
                });
            }

            const chatMensajes = document.getElementById("chatMensajes");
            if (chatMensajes) {
                chatMensajes.innerHTML = html;
                chatMensajes.scrollTop = chatMensajes.scrollHeight;
            }
        })
        .catch(err => {
            console.error("Error al cargar mensajes:", err);
        });
}

function cerrarChat() {
    const modal = document.getElementById("modalChat");
    if (modal) {
        modal.style.display = "none";
        modal.classList.remove("activo");
        modal.classList.add("oculto");
    }
}

window.abrirChat = abrirChat;
window.cerrarChat = cerrarChat;
window.cancelarSolicitud = cancelarSolicitud;
window.cerrarModalEvaluacion = cerrarModalEvaluacion;
window.confirmarCancelacionEvaluacion = confirmarCancelacionEvaluacion;
window.cerrarModalPago = cerrarModalPago;
window.confirmarPagoCancelacion = confirmarPagoCancelacion;
window.mostrarDatosPago = mostrarDatosPago;
