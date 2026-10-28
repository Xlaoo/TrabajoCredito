// programa-administrador.js
function detectarCambio(claveStorage, valorActual, claveNotif, claveVisto) {
    if (typeof valorActual === "undefined" || valorActual === null) return;
    const anterior = localStorage.getItem(claveStorage);
    if (anterior !== null && anterior !== String(valorActual)) {
        let cantidad = parseInt(localStorage.getItem(claveNotif) || "0", 10);
        cantidad++;
        localStorage.setItem(claveNotif, cantidad.toString());
        localStorage.setItem(claveVisto, "false");
    }
    localStorage.setItem(claveStorage, String(valorActual));
}

document.addEventListener("DOMContentLoaded", function () {
    // Aplicar anchos dinámicos de progreso
    document.querySelectorAll("[data-progress]").forEach(function (el) {
        const val = el.getAttribute("data-progress");
        if (val !== null && val !== "") {
            el.style.width = val + "%";
        }
    });

    // Notificaciones de cambio en métricas del inicio
    const datosEl = document.getElementById("programaAdminDatos");
    if (datosEl) {
        const totalClientes = datosEl.getAttribute("data-clientes");
        const creditosAprobados = datosEl.getAttribute("data-creditos");
        const cuotasMora = datosEl.getAttribute("data-mora");
        const reportesGenerados = datosEl.getAttribute("data-reportes");
        const pagosCompletados = datosEl.getAttribute("data-pagos");

        if (localStorage.getItem("ultimoTotalInicioClientes") === null) {
            localStorage.setItem("ultimoTotalInicioClientes", totalClientes || "0");
        }

        detectarCambio("ultimoTotalInicioClientes", totalClientes, "notificacionesInicio", "inicioVisto");
        detectarCambio("ultimoTotalInicioCreditos", creditosAprobados, "notificacionesInicio", "inicioVisto");
        detectarCambio("ultimoTotalInicioMora", cuotasMora, "notificacionesInicio", "inicioVisto");
        detectarCambio("ultimoTotalInicioReportes", reportesGenerados, "notificacionesInicio", "inicioVisto");
        detectarCambio("ultimoTotalInicioPagos", pagosCompletados, "notificacionesInicio", "inicioVisto");
    }

    // Toggle de actividades recientes
    const btnToggle = document.getElementById("btnToggleActividades");
    const hiddenActivities = document.querySelectorAll(".activity-hidden");
    const container = document.getElementById("contenedorActividades");

    let mostrandoTodo = false;

    if (btnToggle) {
        btnToggle.addEventListener("click", function () {
            mostrandoTodo = !mostrandoTodo;

            hiddenActivities.forEach(function (item) {
                if (mostrandoTodo) {
                    item.classList.add("mostrar-actividad");
                } else {
                    item.classList.remove("mostrar-actividad");
                }
            });

            if (mostrandoTodo) {
                if (container) container.classList.add("expandido");
                btnToggle.innerHTML = "← Ver menos actividades";
            } else {
                if (container) container.classList.remove("expandido");
                btnToggle.innerHTML = 'Ver todas las actividades <i class="fas fa-arrow-right"></i>';
            }
        });
    }

    // Campana de notificaciones del topbar si existe
    const cantidad = parseInt(localStorage.getItem("notificacionesInicio") || "0", 10);
    const badgeSuperior = document.querySelector(".navbar-notifications .badge-count");

    if (badgeSuperior) {
        badgeSuperior.innerText = cantidad;
        if (cantidad > 0) {
            badgeSuperior.style.display = "inline-flex";
        } else {
            badgeSuperior.style.display = "none";
        }
    }

    const campana = document.querySelector(".navbar-notifications");
    if (campana) {
        campana.addEventListener("click", function () {
            localStorage.setItem("notificacionesInicio", "0");
            localStorage.setItem("inicioVisto", "true");

            if (badgeSuperior) {
                badgeSuperior.innerText = "0";
                badgeSuperior.style.display = "none";
            }

            const badgeMenu = document.getElementById("badgeInicioMenu");
            if (badgeMenu) {
                badgeMenu.style.display = "none";
                badgeMenu.innerText = "";
            }
        });
    }
});
