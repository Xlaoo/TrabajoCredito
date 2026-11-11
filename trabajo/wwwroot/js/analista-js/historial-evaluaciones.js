let solicitudSeleccionada = 0;
let solicitudCancelacionSeleccionada = 0;
let imagenSeleccionada = null;

document.addEventListener("DOMContentLoaded", function () {
    document.addEventListener("click", function (e) {
        var btn = e.target.closest(".btn-ver-detalle-evaluacion");
        if (btn) {
            abrirDetalle(
                btn.dataset.id,
                btn.dataset.cliente || "",
                btn.dataset.dni || "",
                btn.dataset.celular || "",
                btn.dataset.correo || "",
                btn.dataset.monto || "",
                btn.dataset.plazo || "",
                btn.dataset.motivo || "",
                btn.dataset.ocupacion || "",
                btn.dataset.ingreso || "",
                btn.dataset.egreso || "",
                btn.dataset.estado || "",
                btn.dataset.observacion || ""
            );
            return;
        }

        var btnCanc = e.target.closest(".btn-ver-cancelacion");
        if (btnCanc) {
            abrirModalCancelacion(
                btnCanc.dataset.id,
                btnCanc.dataset.num || "",
                btnCanc.dataset.cliente || "",
                btnCanc.dataset.dni || "",
                btnCanc.dataset.celular || "",
                btnCanc.dataset.correo || "",
                btnCanc.dataset.montoSolicitado || "",
                btnCanc.dataset.montoDevuelto || "",
                btnCanc.dataset.plazo || "",
                btnCanc.dataset.metodo || "",
                btnCanc.dataset.codigo || "",
                btnCanc.dataset.motivo || "",
                btnCanc.dataset.fecha || "",
                btnCanc.dataset.estado || ""
            );
            return;
        }

        var btnChatSol = e.target.closest(".btn-chat-solicitud");
        if (btnChatSol && btnChatSol.id !== "btnChatClienteDesdeModal") {
            var idSol = btnChatSol.dataset.id;
            if (idSol) {
                abrirChat(idSol);
            }
            return;
        }

        var btnChatAdmin = e.target.closest(".btn-abrir-chat-admin");
        if (btnChatAdmin) {
            var solRef = btnChatAdmin.dataset.solicitud || "";
            abrirChatAdmin(solRef);
            return;
        }

        if (e.target.closest("#btnCerrarModalCancelacion")) {
            cerrarModalCancelacion();
            return;
        }

        if (e.target.closest("#btnChatAdminDesdeModal")) {
            cerrarModalCancelacion();
            var ref = solicitudCancelacionSeleccionada > 0
                ? "SOL-" + solicitudCancelacionSeleccionada.toString().padStart(5, "0")
                : "";
            abrirChatAdmin(ref);
            return;
        }

        if (e.target.closest("#btnChatClienteDesdeModal")) {
            abrirMensajeDesdeCancelacion();
            return;
        }

        if (e.target.closest("#btnAceptarCancelacion")) {
            confirmarDecisionCancelacion("Aceptar");
            return;
        }

        if (e.target.closest("#btnRechazarCancelacion")) {
            confirmarDecisionCancelacion("Rechazar");
            return;
        }

        if (e.target.closest("#btnAceptarDetalle") || e.target.closest(".btn-aceptar")) {
            cerrarDetalle();
            return;
        }

        if (e.target.closest("#btnMensajeHistorial") || e.target.closest(".btn-mensaje")) {
            abrirMensajeDesdeHistorial();
            return;
        }

        if (e.target.closest("#btnCerrarChat") || e.target.closest(".btn-cerrar-chat")) {
            cerrarChat();
            return;
        }

        if (e.target.closest("#btnSubirImagenChat") || e.target.closest(".btn-chat-icon")) {
            var input = document.getElementById("fileImagen");
            if (input) input.click();
            return;
        }

        if (e.target.closest("#btnEnviarChat") || e.target.closest(".btn-enviar-chat")) {
            enviarMensaje();
            return;
        }

        if (e.target.closest(".btn-quitar-imagen")) {
            quitarImagen();
            return;
        }

        if (e.target.classList.contains("imagen-chat")) {
            verImagen(e.target.getAttribute("src"));
            return;
        }
    });

    var fileInput = document.getElementById("fileImagen");
    if (fileInput) {
        fileInput.addEventListener("change", function () {
            if (this.files.length > 0) {
                imagenSeleccionada = this.files[0];
                var previewDiv = document.getElementById("nombreArchivoSeleccionado");
                if (previewDiv) {
                    previewDiv.innerHTML = `
                        <div class="archivo-preview">
                            📷 <span>${imagenSeleccionada.name}</span>
                            <button type="button" class="btn-quitar-imagen">✕</button>
                        </div>
                    `;
                }
            }
        });
    }

    var chkVerif = document.getElementById("chkVerificado");
    if (chkVerif) {
        chkVerif.addEventListener("change", function () {
            var btnAceptar = document.getElementById("btnAceptarCancelacion");
            if (btnAceptar) {
                btnAceptar.disabled = !this.checked;
            }
        });
    }

    // Inicializar SignalR para el chat con Administrador si el modal existe
    inicializarSignalRAdmin();
});

function abrirDetalle(id, cliente, dni, celular, correo, monto, plazo, motivo, ocupacion, ingreso, egreso, estado, observacion) {
    solicitudSeleccionada = id;

    var modalTitulo = document.getElementById("modalTitulo");
    if (modalTitulo) {
        modalTitulo.innerText = "Detalle de Evaluación - SOL-" + id.toString().padStart(5, "0");
    }

    var mCliente = document.getElementById("mCliente");
    if (mCliente) mCliente.innerText = cliente;

    var mDni = document.getElementById("mDni");
    if (mDni) mDni.innerText = dni;

    var mCelular = document.getElementById("mCelular");
    if (mCelular) mCelular.innerText = celular;

    var mCorreo = document.getElementById("mCorreo");
    if (mCorreo) mCorreo.innerText = correo;

    var mMonto = document.getElementById("mMonto");
    if (mMonto) mMonto.innerText = "S/ " + monto;

    var mPlazo = document.getElementById("mPlazo");
    if (mPlazo) mPlazo.innerText = plazo + " meses";

    var mMotivo = document.getElementById("mMotivo");
    if (mMotivo) mMotivo.innerText = motivo;

    var mOcupacion = document.getElementById("mOcupacion");
    if (mOcupacion) mOcupacion.innerText = ocupacion;

    var mIngreso = document.getElementById("mIngreso");
    if (mIngreso) mIngreso.innerText = "S/ " + ingreso;

    var mEgreso = document.getElementById("mEgreso");
    if (mEgreso) mEgreso.innerText = "S/ " + egreso;

    var mEstado = document.getElementById("mEstado");
    if (mEstado) mEstado.innerText = estado;

    var mSolicitud = document.getElementById("mSolicitud");
    if (mSolicitud) mSolicitud.innerText = "SOL-" + id.toString().padStart(5, "0");

    var mObservacion = document.getElementById("mObservacion");
    if (mObservacion) mObservacion.innerText = observacion;

    var modal = document.getElementById("modalDetalle");
    if (modal) {
        modal.style.display = "flex";
    }
}

function cerrarDetalle() {
    var modal = document.getElementById("modalDetalle");
    if (modal) {
        modal.style.display = "none";
    }
}

function abrirMensajeDesdeHistorial() {
    if (solicitudSeleccionada == 0 || solicitudSeleccionada == null) {
        alert("No se encontró la solicitud seleccionada.");
        return;
    }

    cerrarDetalle();

    setTimeout(function () {
        abrirChat(solicitudSeleccionada);
    }, 100);
}

function abrirChat(idSolicitud) {
    solicitudSeleccionada = idSolicitud;

    var modalChat = document.getElementById("modalChat");
    if (modalChat) {
        modalChat.style.display = "flex";
    }

    cargarInfoSolicitud(idSolicitud);
    cargarMensajes(idSolicitud);
}

function cargarInfoSolicitud(idSolicitud) {
    fetch('/Analista/ObtenerInfoSolicitud?idSolicitud=' + encodeURIComponent(idSolicitud))
        .then(r => r.json())
        .then(data => {
            var tit = document.getElementById("tituloSolicitud");
            if (tit) tit.innerHTML = "Mensajes - Solicitud " + data.numeroSolicitud;

            var cli = document.getElementById("chatCliente");
            if (cli) cli.innerHTML = data.cliente;

            var est = document.getElementById("chatEstado");
            if (est) est.innerHTML = data.estado;
        })
        .catch(err => console.error("Error al cargar info solicitud:", err));
}

function quitarImagen() {
    imagenSeleccionada = null;
    var fileInput = document.getElementById("fileImagen");
    if (fileInput) fileInput.value = "";
    var preview = document.getElementById("nombreArchivoSeleccionado");
    if (preview) preview.innerHTML = "";
}

function cerrarChat() {
    var modalChat = document.getElementById("modalChat");
    if (modalChat) {
        modalChat.style.display = "none";
    }
    quitarImagen();
}

function enviarMensaje() {
    var txtMensaje = document.getElementById("txtMensaje");
    var mensaje = txtMensaje ? txtMensaje.value.trim() : "";

    if (mensaje === "" && imagenSeleccionada == null) {
        alert("Debe escribir un mensaje o adjuntar una imagen.");
        return;
    }

    var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";

    let formData = new FormData();
    formData.append("idSolicitud", solicitudSeleccionada);
    formData.append("mensaje", mensaje);

    if (token) {
        formData.append("__RequestVerificationToken", token);
    }

    if (imagenSeleccionada != null) {
        formData.append("imagen", imagenSeleccionada);
    }

    fetch('/Analista/EnviarMensaje', {
        method: 'POST',
        headers: token ? { "RequestVerificationToken": token } : {},
        body: formData
    })
        .then(r => r.json())
        .then(data => {
            if (txtMensaje) txtMensaje.value = "";
            quitarImagen();
            cargarMensajes(solicitudSeleccionada);
        })
        .catch(err => console.error("Error al enviar mensaje:", err));
}

function verImagen(url) {
    if (window.Swal) {
        Swal.fire({
            imageUrl: url,
            imageAlt: 'Imagen del chat',
            showConfirmButton: false,
            showCloseButton: true,
            width: '70%',
            background: '#ffffff'
        });
    } else {
        window.open(url, '_blank');
    }
}

function cargarMensajes(idSolicitud) {
    fetch('/Analista/ObtenerMensajes?idSolicitud=' + encodeURIComponent(idSolicitud))
        .then(r => r.json())
        .then(data => {
            let html = "";

            if (!data || data.length === 0) {
                html = `
                    <div class="chat-vacio">
                        <img src="https://cdn-icons-png.flaticon.com/512/1041/1041916.png"
                             class="chat-vacio-img" />
                        <h4>No hay mensajes todavía</h4>
                        <p>Inicia una conversación con el Usuario</p>
                    </div>`;
            } else {
                data.forEach(m => {
                    if (m.tipoUsuario === "Analista") {
                        html += `
                            <div class="mensaje-analista">
                                <div class="chat-derecha">
                                    <div class="cabecera-derecha">
                                        <span class="nombre-chat">${m.nombre}</span>
                                        <div class="avatar-chat">
                                            <i class="fas fa-user-circle"></i>
                                        </div>
                                    </div>
                                    ${m.imagen
                                        ? `<div class="imagen-contenedor">
                                               <img src="${m.imagen}"
                                                    class="imagen-chat"
                                                    alt="Adjunto">
                                           </div>`
                                        : `<div class="burbuja-analista">
                                               <div class="texto-chat">${m.mensaje}</div>
                                           </div>`
                                    }
                                    ${m.imagen && m.mensaje !== "[Imagen]"
                                        ? `<div class="burbuja-analista burbuja-mt-6">
                                               <div class="texto-chat">${m.mensaje}</div>
                                           </div>`
                                        : ''
                                    }
                                    <div class="fecha-chat fecha-derecha">${m.fecha}</div>
                                </div>
                            </div>`;
                    } else {
                        html += `
                            <div class="mensaje-cliente">
                                <div class="avatar-chat">
                                    <i class="fas fa-user-circle"></i>
                                </div>
                                <div>
                                    <div class="nombre-chat">${m.nombre}</div>
                                    ${m.imagen
                                        ? `<div class="imagen-contenedor">
                                               <img src="${m.imagen}"
                                                    class="imagen-chat"
                                                    alt="Adjunto">
                                           </div>`
                                        : `<div class="burbuja-cliente">
                                               <div class="texto-chat">${m.mensaje}</div>
                                           </div>`
                                    }
                                    ${m.imagen && m.mensaje !== "[Imagen]"
                                        ? `<div class="burbuja-cliente burbuja-mt-6">
                                               <div class="texto-chat">${m.mensaje}</div>
                                           </div>`
                                        : ''
                                    }
                                    <div class="fecha-chat">${m.fecha}</div>
                                </div>
                            </div>`;
                    }
                });
            }

            var cont = document.getElementById("chatMensajes");
            if (cont) cont.innerHTML = html;
        })
        .catch(err => console.error("Error al cargar mensajes:", err));
}

function abrirModalCancelacion(id, num, cliente, dni, celular, correo, montoSolicitado, montoDevuelto, plazo, metodo, codigo, motivo, fecha, estado) {
    solicitudCancelacionSeleccionada = parseInt(id, 10);

    var btnChatModal = document.getElementById("btnChatClienteDesdeModal");
    if (btnChatModal) {
        btnChatModal.dataset.id = id;
    }

    var modalTitulo = document.getElementById("tituloModalCancelacion");
    if (modalTitulo) {
        modalTitulo.innerHTML = `<i class="fas fa-file-invoice-dollar"></i> Revisión de Solicitud de Cancelación - ${num || ("SOL-" + id.toString().padStart(5, "0"))}`;
    }

    var cCliente = document.getElementById("cCliente");
    if (cCliente) cCliente.innerText = cliente || "-";

    var cDni = document.getElementById("cDni");
    if (cDni) cDni.innerText = dni || "-";

    var cCelular = document.getElementById("cCelular");
    if (cCelular) cCelular.innerText = celular || "-";

    var cCorreo = document.getElementById("cCorreo");
    if (cCorreo) cCorreo.innerText = correo || "-";

    var cSolicitud = document.getElementById("cSolicitud");
    if (cSolicitud) cSolicitud.innerText = num || ("SOL-" + id.toString().padStart(5, "0"));

    var cMontoPrestamo = document.getElementById("cMontoPrestamo");
    if (cMontoPrestamo) cMontoPrestamo.innerText = "S/ " + montoSolicitado;

    var cPlazo = document.getElementById("cPlazo");
    if (cPlazo) cPlazo.innerText = plazo + " meses";

    var cFecha = document.getElementById("cFecha");
    if (cFecha) cFecha.innerText = fecha || "-";

    var cMontoDevuelto = document.getElementById("cMontoDevuelto");
    if (cMontoDevuelto) cMontoDevuelto.innerText = "S/ " + montoDevuelto;

    var cMetodo = document.getElementById("cMetodo");
    if (cMetodo) cMetodo.innerText = metodo || "Transferencia bancaria";

    var cEntidad = document.getElementById("cEntidad");
    if (cEntidad) cEntidad.innerText = metodo || "Entidad Financiera";

    var cCodigo = document.getElementById("cCodigo");
    if (cCodigo) cCodigo.innerText = codigo || "Pendiente";

    var cEstadoRevision = document.getElementById("cEstadoRevision");
    if (cEstadoRevision) cEstadoRevision.innerText = estado || "Pendiente";

    var cMotivo = document.getElementById("cMotivo");
    if (cMotivo) cMotivo.innerText = motivo || "Sin motivo especificado.";

    var chk = document.getElementById("chkVerificado");
    if (chk) chk.checked = false;

    var btnAceptar = document.getElementById("btnAceptarCancelacion");
    if (btnAceptar) btnAceptar.disabled = true;

    var txtObs = document.getElementById("txtObservacionAnalista");
    if (txtObs) txtObs.value = "";

    var modal = document.getElementById("modalRevisionCancelacion");
    if (modal) {
        modal.style.display = "flex";
    }
}

function cerrarModalCancelacion() {
    var modal = document.getElementById("modalRevisionCancelacion");
    if (modal) {
        modal.style.display = "none";
    }
}

function abrirMensajeDesdeCancelacion() {
    if (!solicitudCancelacionSeleccionada || solicitudCancelacionSeleccionada === 0) {
        alert("No se encontró la solicitud seleccionada.");
        return;
    }

    cerrarModalCancelacion();

    setTimeout(function () {
        abrirChat(solicitudCancelacionSeleccionada);
    }, 150);
}

function confirmarDecisionCancelacion(decision) {
    if (!solicitudCancelacionSeleccionada || solicitudCancelacionSeleccionada === 0) {
        alert("No se ha seleccionado una solicitud válida.");
        return;
    }

    var txtObs = document.getElementById("txtObservacionAnalista");
    var observacion = txtObs ? txtObs.value.trim() : "";

    // Validación obligatoria de observación en frontend (tanto para Aceptar como para Rechazar)
    if (!observacion) {
        if (window.Swal) {
            Swal.fire({
                icon: "warning",
                title: "Observación obligatoria",
                text: "Debe ingresar obligatoriamente una observación o motivo para registrar la decisión.",
                confirmButtonColor: "#6d28d9"
            });
        } else {
            alert("Debe ingresar obligatoriamente una observación o motivo para registrar la decisión.");
        }
        if (txtObs) txtObs.focus();
        return;
    }

    if (decision === "Aceptar") {
        var chk = document.getElementById("chkVerificado");
        var verificado = chk ? chk.checked : false;

        if (!verificado) {
            if (window.Swal) {
                Swal.fire({
                    icon: "warning",
                    title: "Verificación requerida",
                    text: "Debe marcar que ha comprobado los datos de la transferencia antes de confirmar la cancelación.",
                    confirmButtonColor: "#6d28d9"
                });
            } else {
                alert("Debe marcar que ha comprobado los datos de la transferencia antes de confirmar la cancelación.");
            }
            return;
        }

        if (window.Swal) {
            Swal.fire({
                title: "¿Estás seguro de confirmar la cancelación?",
                text: "Esta acción registrará la decisión del analista y el crédito quedará definitivamente cancelado.",
                icon: "warning",
                showCancelButton: true,
                confirmButtonColor: "#16a34a",
                cancelButtonColor: "#64748b",
                confirmButtonText: "Sí, confirmar cancelación",
                cancelButtonText: "Cancelar"
            }).then((result) => {
                if (result.isConfirmed) {
                    ejecutarDecisionCancelacion(solicitudCancelacionSeleccionada, "Aceptar", true, observacion);
                }
            });
        } else {
            if (confirm("¿Estás seguro de confirmar la cancelación? Se registrará la decisión del analista.")) {
                ejecutarDecisionCancelacion(solicitudCancelacionSeleccionada, "Aceptar", true, observacion);
            }
        }
    } else {
        if (window.Swal) {
            Swal.fire({
                title: "¿Estás seguro de rechazar la cancelación?",
                text: "Esta acción registrará el rechazo del analista. La solicitud volverá al estado Aprobado y el cliente mantendrá el crédito activo con sus cuotas vigentes.",
                icon: "warning",
                showCancelButton: true,
                confirmButtonColor: "#dc2626",
                cancelButtonColor: "#64748b",
                confirmButtonText: "Sí, rechazar",
                cancelButtonText: "Cancelar"
            }).then((result) => {
                if (result.isConfirmed) {
                    ejecutarDecisionCancelacion(solicitudCancelacionSeleccionada, "Rechazar", false, observacion);
                }
            });
        } else {
            if (confirm("¿Estás seguro de rechazar la cancelación? La solicitud volverá al estado Aprobado.")) {
                ejecutarDecisionCancelacion(solicitudCancelacionSeleccionada, "Rechazar", false, observacion);
            }
        }
    }
}

function ejecutarDecisionCancelacion(idSolicitud, decision, verificado, observacion) {
    var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";

    var formData = new FormData();
    formData.append("idSolicitud", idSolicitud);
    formData.append("decision", decision);
    formData.append("verificado", verificado ? "true" : "false");
    formData.append("observacion", observacion);
    if (token) {
        formData.append("__RequestVerificationToken", token);
    }

    if (window.Swal) {
        Swal.fire({
            title: "Procesando...",
            text: "Registrando la decisión del analista en el sistema...",
            allowOutsideClick: false,
            didOpen: () => {
                Swal.showLoading();
            }
        });
    }

    fetch('/Analista/ProcesarDecisionCancelacion', {
        method: 'POST',
        headers: token ? { "RequestVerificationToken": token } : {},
        body: formData
    })
    .then(r => r.json())
    .then(data => {
        if (data.success) {
            if (window.Swal) {
                Swal.fire({
                    icon: "success",
                    title: "Operación completada",
                    text: data.mensaje,
                    confirmButtonColor: "#6d28d9"
                }).then(() => {
                    location.reload();
                });
            } else {
                alert(data.mensaje);
                location.reload();
            }
        } else {
            if (window.Swal) {
                Swal.fire({
                    icon: "error",
                    title: "No se pudo procesar",
                    text: data.mensaje || "Ocurrió un error al procesar la cancelación.",
                    confirmButtonColor: "#6d28d9"
                });
            } else {
                alert(data.mensaje || "Ocurrió un error al procesar la cancelación.");
            }
        }
    })
    .catch(err => {
        console.error("Error al procesar cancelación:", err);
        if (window.Swal) {
            Swal.fire({
                icon: "error",
                title: "Error de comunicación",
                text: "No se pudo conectar con el servidor.",
                confirmButtonColor: "#6d28d9"
            });
        } else {
            alert("No se pudo conectar con el servidor.");
        }
    });
}

// =========================================================================
// CHAT ANALISTA <-> ADMINISTRADOR INTEGRADO EN MODAL (SIGNALR)
// =========================================================================
let hubConnectionAdmin = null;

function inicializarSignalRAdmin() {
    const modalChatAdmin = document.getElementById("modalChatAdmin");
    if (!modalChatAdmin || typeof signalR === "undefined") return;

    if (hubConnectionAdmin) return;

    const idAdministrador = parseInt(modalChatAdmin.getAttribute("data-admin-id") || "0", 10);
    const idAnalista = parseInt(modalChatAdmin.getAttribute("data-analista-id") || "0", 10);

    hubConnectionAdmin = new signalR.HubConnectionBuilder()
        .withUrl("/chatAnalistaHub")
        .withAutomaticReconnect()
        .build();

    hubConnectionAdmin.on("RecibirMensajeAdmin", function (idRemitente, rolRemitente, mensaje, fechaTexto, idMensaje) {
        agregarMensajeAdminAlDom(mensaje, fechaTexto, rolRemitente === "Analista" ? "analista" : "admin", idMensaje);
        scrollChatAdminAlFinal();
    });

    hubConnectionAdmin.on("ActualizarEstadoAdmin", function (activo, texto) {
        actualizarIndicadorAdmin(activo, texto);
    });

    hubConnectionAdmin.start()
        .then(() => {
            console.log("[SignalR Historial] Conectado al chat de soporte con Administrador.");
        })
        .catch(err => {
            console.error("[SignalR Historial] Error conexion hub:", err);
        });

    const formChatAdmin = document.getElementById("formChatAdmin");
    if (formChatAdmin) {
        formChatAdmin.addEventListener("submit", function (e) {
            e.preventDefault();
            enviarMensajeAdminHub(idAdministrador, idAnalista);
        });
    }
}

function abrirChatAdmin(solicitudRef) {
    inicializarSignalRAdmin();

    const inputMsg = document.getElementById("mensajeAdminInput");
    if (inputMsg && solicitudRef) {
        inputMsg.value = `[${solicitudRef}] `;
    }

    const modalElem = document.getElementById("modalChatAdmin");
    if (modalElem && typeof bootstrap !== "undefined") {
        const modalInstance = bootstrap.Modal.getInstance(modalElem) || new bootstrap.Modal(modalElem);
        modalInstance.show();
        setTimeout(() => {
            scrollChatAdminAlFinal();
            if (inputMsg) inputMsg.focus();
        }, 300);
    }
}

function enviarMensajeAdminHub(idAdmin, idAnalista) {
    const inputMsg = document.getElementById("mensajeAdminInput");
    if (!inputMsg) return;

    const texto = inputMsg.value.trim();
    if (!texto) return;

    if (!hubConnectionAdmin || hubConnectionAdmin.state !== signalR.HubConnectionState.Connected) {
        alert("La conexión con el administrador aún no está lista. Intente en un instante.");
        return;
    }

    const btnEnviar = document.getElementById("btnEnviarAdmin");
    if (btnEnviar) btnEnviar.disabled = true;

    hubConnectionAdmin.invoke("EnviarMensaje", idAdmin, idAnalista, texto, "Analista")
        .then(() => {
            inputMsg.value = "";
            if (btnEnviar) btnEnviar.disabled = false;
            inputMsg.focus();
        })
        .catch(err => {
            console.error("[SignalR Historial] Error al enviar mensaje:", err);
            if (btnEnviar) btnEnviar.disabled = false;
        });
}

function agregarMensajeAdminAlDom(texto, fechaTexto, claseRol, idMensaje) {
    const contenedor = document.getElementById("chatMensajesAdmin");
    if (!contenedor) return;

    const divMsg = document.createElement("div");
    divMsg.className = `chat-message ${claseRol}`;
    if (idMensaje) divMsg.dataset.idMensaje = idMensaje;

    divMsg.innerHTML = `
        <div class="chat-bubble">${escapeHtml(texto)}</div>
        <div class="chat-meta">${claseRol === "analista" ? "Analista" : "Administrador"} · ${fechaTexto}</div>
    `;

    contenedor.appendChild(divMsg);
}

function actualizarIndicadorAdmin(activo, texto) {
    const estadoElem = document.getElementById("chatEstadoAdmin");
    const textoElem = document.getElementById("chatEstadoAdminTexto");
    if (estadoElem) {
        estadoElem.className = `chat-estado ${activo ? "activo" : "inactivo"}`;
    }
    if (textoElem) {
        textoElem.textContent = texto || (activo ? "Administrador Activo" : "Administrador Inactivo");
    }
}

function scrollChatAdminAlFinal() {
    const contenedor = document.getElementById("chatMensajesAdmin");
    if (contenedor) {
        contenedor.scrollTop = contenedor.scrollHeight;
    }
}

function escapeHtml(texto) {
    var div = document.createElement("div");
    div.innerText = texto;
    return div.innerHTML;
}
