let paginaActual = 1;
const filasPorPagina = 5;

function filtrarPagos() {
    paginaActual = 1;
    paginarPagos();
}

function paginarPagos() {
    let input = document.getElementById("buscarPago");
    let texto = input ? input.value.toLowerCase().trim() : "";

    let filasTodas = Array.from(document.querySelectorAll(".fila-pago"));

    let filas = filasTodas.filter(fila => {
        let busqueda = fila.dataset.busqueda || "";
        return busqueda.includes(texto);
    });

    filasTodas.forEach(fila => fila.style.display = "none");
    let totalFilas = filas.length;
    let totalPaginas = Math.ceil(totalFilas / filasPorPagina);

    filas.forEach((fila, index) => {
        let inicio = (paginaActual - 1) * filasPorPagina;
        let fin = inicio + filasPorPagina;

        fila.style.display =
            index >= inicio && index < fin ? "" : "none";
    });

    let textoPaginacion = document.getElementById("textoPaginacion");
    if (textoPaginacion) {
        let count = Math.min(
            filasPorPagina,
            totalFilas - ((paginaActual - 1) * filasPorPagina)
        );
        if (count < 0) count = 0;
        textoPaginacion.innerText = `Mostrando ${count} de ${totalFilas} resultados`;
    }

    let contenedor = document.getElementById("paginacionPagos");
    if (!contenedor) return;
    contenedor.innerHTML = "";

    let btnAnterior = document.createElement("button");
    btnAnterior.innerText = "‹";
    btnAnterior.onclick = function () {
        if (paginaActual > 1) {
            paginaActual--;
            paginarPagos();
        }
    };
    if (paginaActual === 1) {
        btnAnterior.disabled = true;
        btnAnterior.style.opacity = "0.5";
        btnAnterior.style.cursor = "not-allowed";
    } else {
        btnAnterior.style.cursor = "pointer";
    }
    contenedor.appendChild(btnAnterior);

    for (let i = 1; i <= totalPaginas; i++) {
        let btn = document.createElement("button");
        btn.innerText = i;

        if (i === paginaActual) {
            btn.classList.add("active");
        }

        btn.onclick = function () {
            paginaActual = i;
            paginarPagos();
        };

        contenedor.appendChild(btn);
    }

    let btnSiguiente = document.createElement("button");
    btnSiguiente.innerText = "›";

    btnSiguiente.onclick = function () {
        if (paginaActual < totalPaginas) {
            paginaActual++;
            paginarPagos();
        }
    };
    if (paginaActual === totalPaginas || totalPaginas === 0) {
        btnSiguiente.disabled = true;
        btnSiguiente.style.opacity = "0.5";
        btnSiguiente.style.cursor = "not-allowed";
    } else {
        btnSiguiente.style.cursor = "pointer";
    }
    contenedor.appendChild(btnSiguiente);
}

function toggleHistorial() {
    let lista = document.getElementById("listaActividad");
    let boton = document.getElementById("btnToggleHistorial") || document.querySelector(".btn-historial");
    if (!lista || !boton) return;

    lista.classList.toggle("expandido");

    if (lista.classList.contains("expandido")) {
        boton.innerText = "Ver menos historial ↑";
    } else {
        boton.innerText = "Ver todo el historial →";
    }
}

function aplicarEstilosDinamicos() {
    document.querySelectorAll(".donut-resumen[data-pendientes]").forEach(function (donut) {
        var porc = parseFloat(donut.getAttribute("data-pendientes")) || 0;
        donut.style.background = "conic-gradient(#f59e0b 0 " + porc + "%, #22c55e " + porc + "% 100%)";
    });

    document.querySelectorAll(".barra-fill[data-width]").forEach(function (barra) {
        var w = barra.getAttribute("data-width") || "0";
        barra.style.width = w + "%";
    });
}

document.addEventListener("DOMContentLoaded", function () {
    aplicarEstilosDinamicos();
    paginarPagos();

    var inputBuscar = document.getElementById("buscarPago");
    if (inputBuscar) {
        inputBuscar.addEventListener("input", filtrarPagos);
        inputBuscar.addEventListener("keyup", filtrarPagos);
    }

    var btnHistorial = document.getElementById("btnToggleHistorial");
    if (btnHistorial) {
        btnHistorial.addEventListener("click", toggleHistorial);
    }
});
