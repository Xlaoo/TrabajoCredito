// clientes.js
function abrirDetalleCliente(nombre, dni, celular, correo, genero) {
    const elNombre = document.getElementById("detalleNombre");
    const elDni = document.getElementById("detalleDni");
    const elCelular = document.getElementById("detalleCelular");
    const elCorreo = document.getElementById("detalleCorreo");
    const elGenero = document.getElementById("detalleGenero");

    if (elNombre) elNombre.innerText = nombre || "";
    if (elDni) elDni.innerText = dni || "";
    if (elCelular) elCelular.innerText = celular || "";
    if (elCorreo) elCorreo.innerText = correo || "";
    if (elGenero) elGenero.innerText = genero || "No especificado";

    const modalEl = document.getElementById("modalDetalleCliente");
    if (modalEl && typeof bootstrap !== "undefined") {
        new bootstrap.Modal(modalEl).show();
    }
}

function abrirEditarCliente(id, nombre, apellido, dni, celular, correo, genero, estadoActivo) {
    const elId = document.getElementById("editarId");
    const elNombre = document.getElementById("editarNombre");
    const elApellido = document.getElementById("editarApellido");
    const elDni = document.getElementById("editarDni");
    const elCelular = document.getElementById("editarCelular");
    const elCorreo = document.getElementById("editarCorreo");
    const elGenero = document.getElementById("editarGenero");
    const elActivo = document.getElementById("editarEstadoActivo");

    if (elId) elId.value = id || "";
    if (elNombre) elNombre.value = nombre || "";
    if (elApellido) elApellido.value = apellido || "";
    if (elDni) elDni.value = dni || "";
    if (elCelular) elCelular.value = celular || "";
    if (elCorreo) elCorreo.value = correo || "";
    if (elGenero) elGenero.value = genero || "";
    if (elActivo) elActivo.value = estadoActivo || "true";

    const modalEl = document.getElementById("modalEditarCliente");
    if (modalEl && typeof bootstrap !== "undefined") {
        new bootstrap.Modal(modalEl).show();
    }
}

function abrirEliminarCliente(id, nombre) {
    const elId = document.getElementById("eliminarId");
    const elNombre = document.getElementById("eliminarNombre");

    if (elId) elId.value = id || "";
    if (elNombre) elNombre.innerText = nombre || "";

    const modalEl = document.getElementById("modalEliminarCliente");
    if (modalEl && typeof bootstrap !== "undefined") {
        new bootstrap.Modal(modalEl).show();
    }
}

document.addEventListener("DOMContentLoaded", function () {
    // Filtro automático de estado al cambiar select
    const selectEstado = document.getElementById("selectEstadoCliente");
    if (selectEstado) {
        selectEstado.addEventListener("change", function () {
            if (this.form) {
                this.form.submit();
            }
        });
    }

    // Delegación de eventos en tabla de clientes para acciones
    const tabla = document.querySelector(".clientes-table");
    if (tabla) {
        tabla.addEventListener("click", function (e) {
            const btn = e.target.closest("button[data-accion]");
            if (!btn) return;

            const accion = btn.getAttribute("data-accion");
            if (accion === "detalle") {
                abrirDetalleCliente(
                    btn.getAttribute("data-nombre"),
                    btn.getAttribute("data-dni"),
                    btn.getAttribute("data-celular"),
                    btn.getAttribute("data-correo"),
                    btn.getAttribute("data-genero")
                );
            } else if (accion === "editar") {
                abrirEditarCliente(
                    btn.getAttribute("data-id"),
                    btn.getAttribute("data-nombre"),
                    btn.getAttribute("data-apellido"),
                    btn.getAttribute("data-dni"),
                    btn.getAttribute("data-celular"),
                    btn.getAttribute("data-correo"),
                    btn.getAttribute("data-genero"),
                    btn.getAttribute("data-activo")
                );
            } else if (accion === "eliminar") {
                abrirEliminarCliente(
                    btn.getAttribute("data-id"),
                    btn.getAttribute("data-nombre")
                );
            }
        });
    }

    // AJAX submit para eliminar cliente
    const formEliminar = document.getElementById("formEliminarCliente");
    if (formEliminar) {
        formEliminar.addEventListener("submit", async function (e) {
            e.preventDefault();

            const form = e.target;
            const data = new FormData(form);

            try {
                const response = await fetch(form.action, {
                    method: "POST",
                    body: data
                });

                const result = await response.json();

                const modalEliminarEl = document.getElementById("modalEliminarCliente");
                if (modalEliminarEl && typeof bootstrap !== "undefined") {
                    const inst = bootstrap.Modal.getInstance(modalEliminarEl);
                    if (inst) inst.hide();
                }

                const iconoEl = document.getElementById("mensajeIcono");
                const tituloEl = document.getElementById("mensajeTitulo");
                const textoEl = document.getElementById("mensajeTexto");

                if (iconoEl) iconoEl.innerText = result.ok ? "✅" : "⚠️";
                if (tituloEl) tituloEl.innerText = result.ok ? "Operación exitosa" : "No se pudo eliminar";
                if (textoEl) textoEl.innerText = result.mensaje || "";

                const modalMensajeEl = document.getElementById("modalMensajeCliente");
                if (modalMensajeEl && typeof bootstrap !== "undefined") {
                    new bootstrap.Modal(modalMensajeEl).show();
                }
            } catch (err) {
                console.error("Error al eliminar cliente:", err);
            }
        });
    }

    // Botón Aceptar en modal de mensaje de cliente
    const btnAceptar = document.getElementById("btnAceptarMensajeCliente");
    if (btnAceptar) {
        btnAceptar.addEventListener("click", function () {
            location.reload();
        });
    }
});
