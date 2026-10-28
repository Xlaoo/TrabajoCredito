// Exportaciones - Panel Administrador CrediPlus
document.addEventListener("DOMContentLoaded", function () {
    // Calcular degradado de donut formato de manera compatible con CSP
    document.querySelectorAll(".donut-formato").forEach(el => {
        const excel = parseFloat(el.dataset.excel || "0");
        const total = parseFloat(el.dataset.total || "0");
        const pct = total > 0 ? (excel / total) * 100 : 0;
        el.style.background = `conic-gradient(#22c55e 0 ${pct}%, #ef4444 ${pct}% 100%)`;
    });

    // Paginación de tabla de exportaciones
    const filas = document.querySelectorAll(".fila-exportacion");
    const filasPorPagina = 3;
    let paginaActual = 1;

    const totalFilas = filas.length;
    const totalPaginas = Math.ceil(totalFilas / filasPorPagina) || 1;

    const paginationText = document.getElementById("paginationText");
    const pageNumbers = document.getElementById("pageNumbers");
    const prevPage = document.getElementById("prevPage");
    const nextPage = document.getElementById("nextPage");

    function mostrarPagina(pagina) {
        paginaActual = pagina;

        const inicio = (pagina - 1) * filasPorPagina;
        const fin = inicio + filasPorPagina;

        filas.forEach((fila, index) => {
            fila.style.display = index >= inicio && index < fin ? "" : "none";
        });

        const desde = totalFilas === 0 ? 0 : inicio + 1;
        const hasta = Math.min(fin, totalFilas);

        if (paginationText) {
            paginationText.textContent = `Mostrando ${desde} a ${hasta} de ${totalFilas} exportaciones`;
        }

        renderBotones();
    }

    function renderBotones() {
        if (!pageNumbers) return;
        pageNumbers.innerHTML = "";

        for (let i = 1; i <= totalPaginas; i++) {
            const btn = document.createElement("button");
            btn.type = "button";
            btn.textContent = i.toString();

            if (i === paginaActual) {
                btn.classList.add("active");
            }

            btn.addEventListener("click", function () {
                mostrarPagina(i);
            });

            pageNumbers.appendChild(btn);
        }

        if (prevPage) prevPage.disabled = (paginaActual === 1);
        if (nextPage) nextPage.disabled = (paginaActual === totalPaginas || totalPaginas === 0);
    }

    if (prevPage) {
        prevPage.addEventListener("click", function () {
            if (paginaActual > 1) {
                mostrarPagina(paginaActual - 1);
            }
        });
    }

    if (nextPage) {
        nextPage.addEventListener("click", function () {
            if (paginaActual < totalPaginas) {
                mostrarPagina(paginaActual + 1);
            }
        });
    }

    mostrarPagina(1);

    // Confirmación al eliminar reporte (delegación de eventos)
    document.addEventListener("click", function (e) {
        const btnDelete = e.target.closest("a.delete");
        if (btnDelete) {
            if (!confirm("¿Seguro que deseas eliminar este reporte?")) {
                e.preventDefault();
            }
        }
    });

    // Gestión de notificaciones de exportaciones
    const datosEl = document.getElementById("exportacionesAdminDatos");
    const totalCount = datosEl ? datosEl.dataset.total : "0";

    if (typeof detectarCambio === "function") {
        detectarCambio(
            "ultimoTotalExportacionesSistema",
            totalCount,
            "notificacionesExportaciones",
            "exportacionesVistos"
        );
    }

    try {
        localStorage.setItem("notificacionesExportaciones", "0");
        localStorage.setItem("exportacionesVistos", "true");
    } catch (e) {
        // Ignorar si localStorage está deshabilitado
    }

    const badge = document.getElementById("badgeExportacionesMenu");
    if (badge) {
        badge.style.display = "none";
        badge.innerText = "";
    }
});
