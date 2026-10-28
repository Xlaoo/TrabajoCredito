// creditos.js
function abrirDetalleCredito(solicitud, cliente, dni, monto, plazo, interes, estado, fecha, motivo) {
    const elSolicitud = document.getElementById("detalleSolicitud");
    const elCliente = document.getElementById("detalleCliente");
    const elDni = document.getElementById("detalleDni");
    const elMonto = document.getElementById("detalleMonto");
    const elPlazo = document.getElementById("detallePlazo");
    const elInteres = document.getElementById("detalleInteres");
    const elEstado = document.getElementById("detalleEstado");
    const elFecha = document.getElementById("detalleFecha");
    const elMotivo = document.getElementById("detalleMotivo");

    if (elSolicitud) elSolicitud.innerText = solicitud || "";
    if (elCliente) elCliente.innerText = cliente || "";
    if (elDni) elDni.innerText = dni || "";
    if (elMonto) elMonto.innerText = monto || "";
    if (elPlazo) elPlazo.innerText = plazo || "";
    if (elInteres) elInteres.innerText = interes || "";
    if (elEstado) elEstado.innerText = estado || "";
    if (elFecha) elFecha.innerText = fecha || "";
    if (elMotivo) elMotivo.innerText = motivo || "No especificado";

    const modalEl = document.getElementById("modalDetalleCredito");
    if (modalEl && typeof bootstrap !== "undefined") {
        new bootstrap.Modal(modalEl).show();
    }
}

document.addEventListener("DOMContentLoaded", function () {
    // Aplicar gradientes de gráficos donut
    document.querySelectorAll(".donut[data-gradient]").forEach(function (el) {
        const gradient = el.getAttribute("data-gradient");
        if (gradient) {
            el.style.background = gradient;
        }
    });

    // Aplicar colores de dots de leyendas
    document.querySelectorAll(".dot[data-bg]").forEach(function (el) {
        const bg = el.getAttribute("data-bg");
        if (bg) {
            el.style.backgroundColor = bg;
        }
    });

    // Envío automático al cambiar estado
    const selectEstado = document.getElementById("selectEstadoCredito");
    if (selectEstado) {
        selectEstado.addEventListener("change", function () {
            if (this.form) {
                this.form.submit();
            }
        });
    }

    // Delegación de clic en tabla para ver detalle
    const tabla = document.querySelector(".creditos-table");
    if (tabla) {
        tabla.addEventListener("click", function (e) {
            const btn = e.target.closest("button[data-accion='detalle']");
            if (!btn) return;

            abrirDetalleCredito(
                btn.getAttribute("data-solicitud"),
                btn.getAttribute("data-cliente"),
                btn.getAttribute("data-dni"),
                btn.getAttribute("data-monto"),
                btn.getAttribute("data-plazo"),
                btn.getAttribute("data-interes"),
                btn.getAttribute("data-estado"),
                btn.getAttribute("data-fecha"),
                btn.getAttribute("data-motivo")
            );
        });
    }
});
