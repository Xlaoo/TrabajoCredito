// ====================================================================
// CREDIPLUS - PANTALLA PRINCIPAL (CARRUSEL DE CLIENTES)
// Script modular externo sin dependencias inline para CSP estricta
// ====================================================================

(function () {
    "use strict";

    let clientes = [];
    let inicio = 0;
    const visibles = 3;

    function cargarClientesDesdeDOM() {
        const scriptData = document.getElementById("clientesData");
        if (scriptData && scriptData.textContent) {
            try {
                clientes = JSON.parse(scriptData.textContent);
            } catch (e) {
                console.error("Error al parsear los datos de clientes:", e);
                clientes = [];
            }
        }
    }

    function renderClientes() {
        const clientGrid = document.getElementById("clientGrid");
        const btnLeft = document.getElementById("btnLeft");
        const btnRight = document.getElementById("btnRight");

        if (!clientGrid) return;

        let html = "";

        for (let i = inicio; i < inicio + visibles && i < clientes.length; i++) {
            let estrellas = "";
            const numEstrellas = clientes[i].estrellas ?? clientes[i].Calificacion ?? 0;
            for (let j = 0; j < numEstrellas; j++) {
                estrellas += "⭐";
            }

            const nombre = clientes[i].nombre ?? clientes[i].NombreCompleto ?? "";
            const comentario = clientes[i].comentario ?? clientes[i].Comentario ?? "";

            html += `
                <div class="client">
                    <div class="client-icon">
                        <i class="fas fa-user"></i>
                    </div>
                    <div>
                        <b>${nombre}</b>
                        <p>${comentario}</p>
                        ${estrellas}
                    </div>
                </div>`;
        }

        clientGrid.innerHTML = html;

        if (btnLeft) {
            btnLeft.disabled = (inicio === 0);
        }
        if (btnRight) {
            btnRight.disabled = (inicio >= clientes.length - visibles);
        }
    }

    function moverClientes(direccion) {
        inicio += direccion;

        if (inicio < 0) {
            inicio = 0;
        }

        if (inicio > clientes.length - visibles) {
            inicio = Math.max(0, clientes.length - visibles);
        }

        renderClientes();
    }

    function inicializarPantallaPrincipal() {
        cargarClientesDesdeDOM();

        const btnLeft = document.getElementById("btnLeft");
        const btnRight = document.getElementById("btnRight");

        if (btnLeft && !btnLeft.dataset.ppBound) {
            btnLeft.dataset.ppBound = "true";
            btnLeft.addEventListener("click", function () {
                moverClientes(-1);
            });
        }

        if (btnRight && !btnRight.dataset.ppBound) {
            btnRight.dataset.ppBound = "true";
            btnRight.addEventListener("click", function () {
                moverClientes(1);
            });
        }

        renderClientes();
    }

    // Exponer en window para compatibilidad hacia atrás
    window.renderClientes = renderClientes;
    window.moverClientes = moverClientes;

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", inicializarPantallaPrincipal);
    } else {
        inicializarPantallaPrincipal();
    }
})();
