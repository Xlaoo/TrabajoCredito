document.addEventListener("DOMContentLoaded", function () {
    const inputMonto = document.getElementById("montoPrestamo");
    const btnSimular = document.getElementById("btnSimularCredito");

    if (inputMonto) {
        inputMonto.addEventListener("input", function () {
            this.value = this.value.replace(/[^0-9]/g, "");
        });

        inputMonto.addEventListener("keydown", function (e) {
            if (e.key === "Enter") {
                e.preventDefault();
                simularCredito();
            }
        });
    }

    if (btnSimular) {
        btnSimular.addEventListener("click", simularCredito);
    }
});

function simularCredito() {
    const inputMonto = document.getElementById("montoPrestamo");
    const selectPlazo = document.getElementById("plazoMeses");
    const errorEl = document.getElementById("mensajeError");
    const resultadoEl = document.getElementById("resultado");

    if (!inputMonto || !selectPlazo || !errorEl || !resultadoEl) return;

    let monto = Number(inputMonto.value);
    let plazo = Number(selectPlazo.value);

    errorEl.style.display = "none";
    errorEl.innerText = "";

    if (!monto) {
        mostrarError("Ingrese un monto para simular.");
        return;
    }

    if (monto < 1000) {
        mostrarError("El monto mínimo permitido es S/ 1,000.");
        return;
    }

    if (monto > 50000) {
        mostrarError("El monto máximo permitido es S/ 50,000.");
        return;
    }

    const interesPorcentaje = 0.10;
    const interesTotal = monto * interesPorcentaje;
    const totalPagar = monto + interesTotal;
    const cuota = totalPagar / plazo;
    const fechaPago = calcularPrimerPago();

    const resMonto = document.getElementById("resMonto");
    const resPlazo = document.getElementById("resPlazo");
    const resCuota = document.getElementById("resCuota");
    const resFecha = document.getElementById("resFecha");

    if (resMonto) resMonto.innerText = formatoSoles(monto);
    if (resPlazo) resPlazo.innerText = plazo + " meses";
    if (resCuota) resCuota.innerText = formatoSoles(cuota);
    if (resFecha) resFecha.innerText = fechaPago;

    resultadoEl.style.display = "block";
}

function calcularPrimerPago() {
    let fecha = new Date();
    fecha.setMonth(fecha.getMonth() + 1);

    let dia = String(fecha.getDate()).padStart(2, "0");
    let mes = String(fecha.getMonth() + 1).padStart(2, "0");
    let anio = fecha.getFullYear();

    return dia + "/" + mes + "/" + anio;
}

function formatoSoles(valor) {
    return "S/ " + valor.toLocaleString("es-PE", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });
}

function mostrarError(mensaje) {
    let error = document.getElementById("mensajeError");
    let resultado = document.getElementById("resultado");
    if (error) {
        error.innerText = mensaje;
        error.style.display = "block";
    }
    if (resultado) {
        resultado.style.display = "none";
    }
}
