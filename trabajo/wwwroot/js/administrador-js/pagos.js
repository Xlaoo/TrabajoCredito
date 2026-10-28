// Pagos - Panel Administrador CrediPlus
document.addEventListener("DOMContentLoaded", () => {
    // Aplicar degradados a los gráficos donut vía CSSOM (cumpliendo CSP)
    document.querySelectorAll("[data-gradient]").forEach(el => {
        const gradient = el.getAttribute("data-gradient");
        if (gradient) {
            el.style.background = gradient;
        }
    });

    // Paginación de la tabla
    const filas = document.querySelectorAll(".fila-tabla-pago");
    const filasPorPagina = 5;
    let paginaActual = 1;

    function mostrarPagina(pagina) {
        const totalPaginas = Math.ceil(filas.length / filasPorPagina) || 1;
        if (pagina < 1) pagina = 1;
        if (pagina > totalPaginas) pagina = totalPaginas;
        paginaActual = pagina;

        filas.forEach((fila, index) => {
            const inicio = (paginaActual - 1) * filasPorPagina;
            const fin = inicio + filasPorPagina;
            if (index >= inicio && index < fin) {
                fila.style.display = "table-row";
            } else {
                fila.style.display = "none";
            }
        });

        const desde = filas.length === 0 ? 0 : ((paginaActual - 1) * filasPorPagina) + 1;
        const hasta = Math.min(paginaActual * filasPorPagina, filas.length);
        const textoPaginacion = document.getElementById("textoPaginacion");
        if (textoPaginacion) {
            textoPaginacion.innerHTML = `Mostrando ${desde} a ${hasta} de ${filas.length} pagos`;
        }

        crearBotonesPaginacion(totalPaginas);
    }

    function crearBotonesPaginacion(totalPaginas) {
        const contenedor = document.getElementById("botonesPaginacion");
        if (!contenedor) return;
        contenedor.innerHTML = "";

        const anterior = document.createElement("button");
        anterior.type = "button";
        anterior.innerHTML = "← Anterior";
        anterior.disabled = (paginaActual === 1);
        anterior.addEventListener("click", () => mostrarPagina(paginaActual - 1));
        contenedor.appendChild(anterior);

        for (let i = 1; i <= totalPaginas; i++) {
            const boton = document.createElement("button");
            boton.type = "button";
            boton.innerHTML = i.toString();
            if (i === paginaActual) {
                boton.classList.add("activo");
            }
            boton.addEventListener("click", () => mostrarPagina(i));
            contenedor.appendChild(boton);
        }

        const siguiente = document.createElement("button");
        siguiente.type = "button";
        siguiente.innerHTML = "Siguiente →";
        siguiente.disabled = (paginaActual === totalPaginas);
        siguiente.addEventListener("click", () => mostrarPagina(paginaActual + 1));
        contenedor.appendChild(siguiente);
    }

    // Modal de Detalle de Pago
    function abrirDetallePago(cliente, dni, monto, metodo, referencia, fecha, estado) {
        const setVal = (id, val) => {
            const el = document.getElementById(id);
            if (el) el.textContent = val;
        };
        setVal("detalleCliente", cliente);
        setVal("detalleDni", dni);
        setVal("detalleMonto", monto);
        setVal("detalleMetodo", metodo);
        setVal("detalleReferencia", referencia);
        setVal("detalleFecha", fecha);
        setVal("detalleEstado", estado);

        const modal = document.getElementById("modalPago");
        if (modal) modal.style.display = "flex";
    }

    function cerrarDetallePago() {
        const modal = document.getElementById("modalPago");
        if (modal) modal.style.display = "none";
    }

    const btnCerrarModal = document.getElementById("btnCerrarModalPago");
    if (btnCerrarModal) {
        btnCerrarModal.addEventListener("click", cerrarDetallePago);
    }

    // Menú de Exportación
    const btnExportar = document.getElementById("btnExportar");
    function toggleExportMenu() {
        const menu = document.getElementById("exportMenu");
        const flecha = document.querySelector("#btnExportar .fa-chevron-down");
        if (menu) menu.classList.toggle("show");
        if (flecha) flecha.classList.toggle("rotate");
    }

    if (btnExportar) {
        btnExportar.addEventListener("click", (e) => {
            e.stopPropagation();
            toggleExportMenu();
        });
    }

    // Toggle de Últimos Pagos
    let mostrandoTodos = false;
    const btnVerPagos = document.getElementById("btnVerPagos");

    function togglePagos() {
        mostrandoTodos = !mostrandoTodos;
        document.querySelectorAll(".ultimo-extra").forEach(pago => {
            pago.style.display = mostrandoTodos ? "flex" : "none";
        });

        if (btnVerPagos) {
            if (mostrandoTodos) {
                btnVerPagos.innerHTML = 'Ver menos pagos <i class="fa-solid fa-angle-up"></i>';
            } else {
                btnVerPagos.innerHTML = 'Ver todos los pagos <i class="fa-solid fa-angle-down"></i>';
            }
        }
    }

    if (btnVerPagos) {
        btnVerPagos.addEventListener("click", togglePagos);
    }

    // Click global para modales y dropdowns
    document.addEventListener("click", (e) => {
        // Delegación de botón de detalle de pago
        const btnDetalle = e.target.closest(".btn-detalle-pago");
        if (btnDetalle) {
            abrirDetallePago(
                btnDetalle.dataset.cliente || "",
                btnDetalle.dataset.dni || "",
                btnDetalle.dataset.monto || "",
                btnDetalle.dataset.metodo || "",
                btnDetalle.dataset.referencia || "",
                btnDetalle.dataset.fecha || "",
                btnDetalle.dataset.estado || ""
            );
            return;
        }

        // Clic fuera del contenido del modal
        const modal = document.getElementById("modalPago");
        if (e.target === modal) {
            cerrarDetallePago();
        }

        // Clic fuera del menú de exportación
        if (!e.target.closest(".export-dropdown")) {
            const menu = document.getElementById("exportMenu");
            if (menu) menu.classList.remove("show");
            const flecha = document.querySelector("#btnExportar .fa-chevron-down");
            if (flecha) flecha.classList.remove("rotate");
        }
    });

    // Inicialización al cargar la página
    document.querySelectorAll(".ultimo-extra").forEach(pago => {
        pago.style.display = "none";
    });
    if (btnVerPagos) {
        btnVerPagos.innerHTML = "Ver todos los pagos ▼";
    }
    mostrarPagina(1);
});
