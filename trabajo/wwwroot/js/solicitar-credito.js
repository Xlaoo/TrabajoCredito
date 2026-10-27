document.addEventListener("DOMContentLoaded", function () {
    const container = document.getElementById("solicitarCreditoContainer");
    if (container) {
        const mostrarConfirmacion = container.dataset.mostrarConfirmacion;
        const pendiente = container.dataset.pendiente;
        if (mostrarConfirmacion === "true" || pendiente === "True") {
            mostrarPaso(4);
        }
    }

    const rangoMonto = document.getElementById("rangoMonto");
    if (rangoMonto) {
        rangoMonto.addEventListener("input", actualizarMonto);
    }

    const btnPaso1 = document.getElementById("btnContinuarPaso1");
    if (btnPaso1) {
        btnPaso1.addEventListener("click", validarPaso1);
    }

    const ingresoInput = document.getElementById("ingresoMensual");
    if (ingresoInput) {
        ingresoInput.addEventListener("input", function () {
            formatearDinero(this);
        });
    }

    const egresoInput = document.getElementById("egresoMensual");
    if (egresoInput) {
        egresoInput.addEventListener("input", function () {
            formatearDinero(this);
        });
    }

    const opcionesCreditos = document.querySelectorAll(".opciones-creditos .opcion-card");
    opcionesCreditos.forEach(function (card) {
        card.addEventListener("click", function () {
            const valor = this.dataset.creditoValor;
            seleccionarCredito(valor, this);
        });
    });

    const motivosCards = document.querySelectorAll("#pantalla2 .motivos .motivo-card");
    motivosCards.forEach(function (card) {
        card.addEventListener("click", function () {
            const motivo = this.dataset.motivo;
            seleccionarMotivo(motivo, this);
        });
    });

    const btnOtros = document.querySelector(".motivo-otros");
    if (btnOtros) {
        btnOtros.addEventListener("click", function () {
            activarOtroMotivo(this);
        });
    }

    const btnAtras1 = document.getElementById("btnAtrasPaso1");
    if (btnAtras1) {
        btnAtras1.addEventListener("click", function () {
            mostrarPaso(1);
        });
    }

    const btnPaso2 = document.getElementById("btnContinuarPaso2");
    if (btnPaso2) {
        btnPaso2.addEventListener("click", validarPaso2);
    }

    const metodosPagoCards = document.querySelectorAll("#pantalla3 .motivos .motivo-card");
    metodosPagoCards.forEach(function (card) {
        card.addEventListener("click", function () {
            const metodo = this.dataset.metodoPago;
            seleccionarMetodoPago(metodo, this);
        });
    });

    const numeroCuentaInput = document.getElementById("numeroCuentaPago");
    if (numeroCuentaInput) {
        numeroCuentaInput.addEventListener("input", function () {
            this.value = this.value.replace(/[^0-9]/g, "");
        });
    }

    const btnAtras2 = document.getElementById("btnAtrasPaso2");
    if (btnAtras2) {
        btnAtras2.addEventListener("click", function () {
            mostrarPaso(2);
        });
    }

    const btnFinalizar = document.getElementById("btnFinalizarSolicitud");
    if (btnFinalizar) {
        btnFinalizar.addEventListener("click", validarPaso3);
    }
});

function actualizarMonto() {
    const rango = document.getElementById("rangoMonto");
    const textoMonto = document.getElementById("textoMonto");
    const montoSolicitadoReal = document.getElementById("montoSolicitadoReal");

    if (!rango || !textoMonto || !montoSolicitadoReal) return;

    const monto = rango.value;
    textoMonto.innerText = "S/ " + Number(monto).toLocaleString("es-PE");
    montoSolicitadoReal.value = monto;
}

function validarPaso1() {
    const plazoSelect = document.querySelector("[name='plazoMeses']");
    if (!plazoSelect || plazoSelect.value === "") {
        alert("Selecciona el plazo en meses.");
        return;
    }
    mostrarPaso(2);
}

function seleccionarCredito(valor, elemento) {
    const input = document.getElementById("tieneOtrosCreditos");
    if (input) input.value = valor;

    const opciones = document.querySelectorAll(".opciones-creditos .opcion-card");
    opciones.forEach(function (x) {
        x.classList.remove("activo");
    });

    if (elemento) elemento.classList.add("activo");
}

function seleccionarMotivo(valor, elemento) {
    const inputMotivo = document.getElementById("motivoPrestamo");
    if (inputMotivo) inputMotivo.value = valor;

    const motivos = document.querySelectorAll("#pantalla2 .motivos .motivo-card");
    motivos.forEach(function (x) {
        x.classList.remove("activo");
    });

    const botonOtros = document.querySelector(".motivo-otros");
    if (botonOtros) botonOtros.classList.remove("activo");

    if (elemento) elemento.classList.add("activo");

    const inputOtro = document.getElementById("motivoOtro");
    if (inputOtro) {
        inputOtro.disabled = true;
        inputOtro.required = false;
        inputOtro.value = "";
    }
}

function activarOtroMotivo(elemento) {
    const inputMotivo = document.getElementById("motivoPrestamo");
    if (inputMotivo) inputMotivo.value = "Otros";

    const motivos = document.querySelectorAll("#pantalla2 .motivos .motivo-card");
    motivos.forEach(function (x) {
        x.classList.remove("activo");
    });

    if (elemento) elemento.classList.add("activo");

    const inputOtro = document.getElementById("motivoOtro");
    if (inputOtro) {
        inputOtro.disabled = false;
        inputOtro.required = true;
        inputOtro.focus();
    }
}

function validarPaso2() {
    const ingreso = document.getElementById("ingresoMensual")?.value.trim() || "";
    const egreso = document.getElementById("egresoMensual")?.value.trim() || "";
    const credito = document.getElementById("tieneOtrosCreditos")?.value.trim() || "";
    const motivo = document.getElementById("motivoPrestamo")?.value.trim() || "";
    const ocupacion = document.getElementById("ocupacion")?.value.trim() || "";
    const motivoOtro = document.getElementById("motivoOtro")?.value.trim() || "";

    if (ingreso === "") {
        alert("Ingresa tu ingreso mensual.");
        return;
    }

    if (egreso === "") {
        alert("Ingresa tu egreso mensual.");
        return;
    }

    if (credito === "") {
        alert("Selecciona si tienes créditos activos.");
        return;
    }

    if (motivo === "") {
        alert("Selecciona el motivo del préstamo.");
        return;
    }

    if (ocupacion === "") {
        alert("Escribe tu ocupación.");
        return;
    }

    if (motivo === "Otros" && motivoOtro === "") {
        alert("Escribe el motivo del préstamo.");
        return;
    }

    mostrarPaso(3);
}

function formatearDinero(input) {
    if (!input) return;
    let valor = input.value.replace(/\D/g, "");
    if (valor === "") {
        input.value = "";
        return;
    }
    input.value = Number(valor).toLocaleString("es-PE");
}

function seleccionarMetodoPago(metodo, elemento) {
    const metodoInput = document.getElementById("metodoPago");
    if (metodoInput) metodoInput.value = metodo;

    document.querySelectorAll("#pantalla3 .motivos .motivo-card").forEach(function (x) {
        x.classList.remove("activo");
    });

    if (elemento) elemento.classList.add("activo");

    const entidad = document.getElementById("entidadPago");
    const numero = document.getElementById("numeroCuentaPago");

    if (!entidad || !numero) return;

    entidad.innerHTML = '<option value="">Seleccione entidad</option>';
    numero.value = "";

    if (metodo === "Transferencia bancaria") {
        ["BCP", "Interbank", "BBVA", "Scotiabank", "Caja Arequipa"].forEach(function (x) {
            const opt = document.createElement("option");
            opt.value = x;
            opt.textContent = x;
            entidad.appendChild(opt);
        });
        numero.placeholder = "Ingrese número de cuenta";
        numero.maxLength = 20;
    } else if (metodo === "Yape") {
        ["Yape", "BCP"].forEach(function (x) {
            const opt = document.createElement("option");
            opt.value = x;
            opt.textContent = x;
            entidad.appendChild(opt);
        });
        numero.placeholder = "Ingrese celular Yape";
        numero.maxLength = 9;
    } else if (metodo === "Plin") {
        ["Plin", "Interbank", "BBVA", "Scotiabank", "Caja Arequipa"].forEach(function (x) {
            const opt = document.createElement("option");
            opt.value = x;
            opt.textContent = x;
            entidad.appendChild(opt);
        });
        numero.placeholder = "Ingrese celular Plin";
        numero.maxLength = 9;
    }
}

function validarPaso3() {
    const metodo = document.getElementById("metodoPago")?.value.trim() || "";
    const entidad = document.getElementById("entidadPago")?.value.trim() || "";
    const numero = document.getElementById("numeroCuentaPago")?.value.trim() || "";
    const titular = document.getElementById("titularCuenta")?.value.trim() || "";

    if (metodo === "") {
        alert("Selecciona un método de pago.");
        return;
    }

    if (entidad === "") {
        alert("Ingresa la entidad de pago.");
        return;
    }

    if (numero === "") {
        alert("Ingresa el número de cuenta, CCI o celular.");
        return;
    }

    if (titular === "") {
        alert("Ingresa el nombre del titular.");
        return;
    }

    const form = document.getElementById("formSolicitarCredito");
    if (form) {
        form.submit();
    }
}

function mostrarPaso(numero) {
    const p1 = document.getElementById("pantalla1");
    const p2 = document.getElementById("pantalla2");
    const p3 = document.getElementById("pantalla3");
    const p4 = document.getElementById("pantalla4");

    if (p1) p1.classList.add("oculto");
    if (p2) p2.classList.add("oculto");
    if (p3) p3.classList.add("oculto");
    if (p4) p4.classList.add("oculto");

    const target = document.getElementById("pantalla" + numero);
    if (target) target.classList.remove("oculto");

    for (let i = 1; i <= 4; i++) {
        const step = document.getElementById("step" + i);
        if (step) {
            if (i === numero) {
                step.classList.add("active");
            } else {
                step.classList.remove("active");
            }
        }
    }

    const btnVolver = document.getElementById("btnVolver");
    if (btnVolver) {
        if (numero === 1) {
            btnVolver.classList.remove("oculto");
        } else {
            btnVolver.classList.add("oculto");
        }
    }
}
