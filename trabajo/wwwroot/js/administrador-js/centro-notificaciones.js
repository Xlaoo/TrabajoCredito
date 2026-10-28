/**
 * Script para Centro de Notificaciones - Administrador CrediPlus
 */
document.addEventListener('DOMContentLoaded', function () {
    const rolDestino = document.getElementById("rolDestino");
    const destinoSelect = document.getElementById("idUsuarioDestino");

    function cambiarDestinatarios() {
        if (!rolDestino || !destinoSelect) return;
        const rol = rolDestino.value;
        const opcionesCliente = document.querySelectorAll(".opcion-cliente");
        const opcionesAnalista = document.querySelectorAll(".opcion-analista");

        destinoSelect.value = "";

        opcionesCliente.forEach(function (x) {
            x.hidden = (rol !== "Cliente");
        });
        opcionesAnalista.forEach(function (x) {
            x.hidden = (rol !== "Analista");
        });
    }

    if (rolDestino) {
        rolDestino.addEventListener('change', cambiarDestinatarios);
        cambiarDestinatarios();
    }

    // Modal Nueva Plantilla
    const modalPlantilla = document.getElementById("modalPlantilla");
    const btnAbrirPlantilla = document.getElementById("btnAbrirModalPlantilla");

    if (btnAbrirPlantilla && modalPlantilla) {
        btnAbrirPlantilla.addEventListener('click', function () {
            modalPlantilla.classList.add("show");
        });
    }

    document.querySelectorAll(".btn-cerrar-modal-plantilla").forEach(function (btn) {
        btn.addEventListener('click', function () {
            if (modalPlantilla) modalPlantilla.classList.remove("show");
        });
    });

    // Modal Editar Plantilla
    const modalEditarPlantilla = document.getElementById("modalEditarPlantilla");

    document.querySelectorAll(".btn-cerrar-editar-plantilla").forEach(function (btn) {
        btn.addEventListener('click', function () {
            if (modalEditarPlantilla) modalEditarPlantilla.classList.remove("show");
        });
    });

    // Modal Detalle Notificación
    const modalDetalleNotificacion = document.getElementById("modalDetalleNotificacion");

    document.querySelectorAll(".btn-cerrar-detalle-notificacion").forEach(function (btn) {
        btn.addEventListener('click', function () {
            if (modalDetalleNotificacion) modalDetalleNotificacion.classList.remove("show");
        });
    });

    // Delegación de eventos para botones de plantillas y notificaciones
    document.addEventListener('click', function (e) {
        // Editar plantilla
        const btnEditar = e.target.closest('.btn-editar-plantilla');
        if (btnEditar) {
            const id = btnEditar.getAttribute('data-id') || '';
            const nombre = btnEditar.getAttribute('data-nombre') || '';
            const asunto = btnEditar.getAttribute('data-asunto') || '';
            const descripcion = btnEditar.getAttribute('data-descripcion') || '';
            const mensaje = btnEditar.getAttribute('data-mensaje') || '';
            const icono = btnEditar.getAttribute('data-icono') || '';
            const color = btnEditar.getAttribute('data-color') || '';

            const editId = document.getElementById("editIdPlantilla");
            const editNom = document.getElementById("editNombre");
            const editAsu = document.getElementById("editAsunto");
            const editDesc = document.getElementById("editDescripcion");
            const editMen = document.getElementById("editMensaje");
            const editIco = document.getElementById("editIcono");
            const editCol = document.getElementById("editColor");

            if (editId) editId.value = id;
            if (editNom) editNom.value = nombre;
            if (editAsu) editAsu.value = asunto;
            if (editDesc) editDesc.value = descripcion;
            if (editMen) editMen.value = mensaje;
            if (editIco) editIco.value = icono;
            if (editCol) editCol.value = color;

            if (modalEditarPlantilla) modalEditarPlantilla.classList.add("show");
            return;
        }

        // Eliminar plantilla confirmación
        const btnEliminar = e.target.closest('.btn-eliminar-plantilla');
        if (btnEliminar) {
            if (!confirm('¿Seguro que deseas eliminar esta plantilla?')) {
                e.preventDefault();
            }
            return;
        }

        // Ver detalle notificación
        const btnVer = e.target.closest('.btn-ver-notificacion');
        if (btnVer) {
            const id = btnVer.getAttribute('data-id') || '';
            const asunto = btnVer.getAttribute('data-asunto') || '';
            const tipo = btnVer.getAttribute('data-tipo') || '';
            const destino = btnVer.getAttribute('data-destino') || '';
            const canal = btnVer.getAttribute('data-canal') || '';
            const fecha = btnVer.getAttribute('data-fecha') || '';
            const estado = btnVer.getAttribute('data-estado') || '';
            const mensaje = btnVer.getAttribute('data-mensaje') || '';

            const detId = document.getElementById("detId");
            const detAsunto = document.getElementById("detAsunto");
            const detTipo = document.getElementById("detTipo");
            const detDestino = document.getElementById("detDestino");
            const detCanal = document.getElementById("detCanal");
            const detFecha = document.getElementById("detFecha");
            const detEstado = document.getElementById("detEstado");
            const detMensaje = document.getElementById("detMensaje");

            if (detId) detId.textContent = "NTF-" + String(id).padStart(5, "0");
            if (detAsunto) detAsunto.textContent = asunto;
            if (detTipo) detTipo.textContent = tipo;
            if (detDestino) detDestino.textContent = destino;
            if (detCanal) detCanal.textContent = canal;
            if (detFecha) detFecha.textContent = fecha;
            if (detEstado) detEstado.textContent = estado;
            if (detMensaje) detMensaje.textContent = mensaje;

            if (modalDetalleNotificacion) modalDetalleNotificacion.classList.add("show");
            return;
        }
    });

    // Cerrar modales al hacer clic fuera del card
    window.addEventListener('click', function (e) {
        if (e.target === modalPlantilla) {
            modalPlantilla.classList.remove("show");
        }
        if (e.target === modalEditarPlantilla) {
            modalEditarPlantilla.classList.remove("show");
        }
        if (e.target === modalDetalleNotificacion) {
            modalDetalleNotificacion.classList.remove("show");
        }
    });
});
