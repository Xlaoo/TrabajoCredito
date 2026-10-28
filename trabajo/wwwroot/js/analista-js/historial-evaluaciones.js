let solicitudSeleccionada = 0;
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

    let formData = new FormData();
    formData.append("idSolicitud", solicitudSeleccionada);
    formData.append("mensaje", mensaje);

    if (imagenSeleccionada != null) {
        formData.append("imagen", imagenSeleccionada);
    }

    fetch('/Analista/EnviarMensaje', {
        method: 'POST',
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
