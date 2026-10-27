document.addEventListener("DOMContentLoaded", function () {
    const btnsPagar = document.querySelectorAll(".btn-abrir-modal-pago");
    const metodoPagoSelect = document.getElementById("metodoPago");
    const entidadPagoSelect = document.getElementById("entidadPago");
    const btnCerrar = document.getElementById("btnCerrarModalPago");
    const btnConfirmar = document.getElementById("btnConfirmarPago");
    const datosPagoDiv = document.getElementById("datosPago");

    btnsPagar.forEach(function (btn) {
        btn.addEventListener("click", function () {
            const idCuota = this.dataset.idCuota;
            const monto = this.dataset.monto;
            const metodo = this.dataset.metodo || "";
            const entidad = this.dataset.entidad || "";
            const numero = this.dataset.numero || "";
            const titular = this.dataset.titular || "";
            abrirModalPago(idCuota, monto, metodo, entidad, numero, titular);
        });
    });

    if (metodoPagoSelect) {
        metodoPagoSelect.addEventListener("change", mostrarDatosPago);
    }

    if (entidadPagoSelect) {
        entidadPagoSelect.addEventListener("change", mostrarDatosPago);
    }

    if (btnCerrar) {
        btnCerrar.addEventListener("click", cerrarModalPago);
    }

    if (btnConfirmar) {
        btnConfirmar.addEventListener("click", confirmarPago);
    }

    if (datosPagoDiv) {
        datosPagoDiv.addEventListener("input", function (e) {
            if (e.target && e.target.id === "codigoOperacion") {
                e.target.value = e.target.value.replace(/[^0-9]/g, "").slice(0, 8);
            }
        });
    }
});

function abrirModalPago(idCuota, monto, metodo, entidad, numero, titular) {
    const idCuotaInput = document.getElementById("idCuota");
    const montoPagadoInput = document.getElementById("montoPagado");
    const montoTextoModal = document.getElementById("montoTextoModal");
    const metodoPagoSelect = document.getElementById("metodoPago");
    const entidadPagoSelect = document.getElementById("entidadPago");
    const datosPagoDiv = document.getElementById("datosPago");
    const modal = document.getElementById("modalPago");

    if (idCuotaInput) idCuotaInput.value = idCuota;
    if (montoPagadoInput) montoPagadoInput.value = monto;
    if (montoTextoModal) montoTextoModal.value = "S/ " + Number(monto).toFixed(2);

    if (metodoPagoSelect) metodoPagoSelect.value = "";
    if (entidadPagoSelect) {
        entidadPagoSelect.innerHTML = '<option value="">Seleccione entidad</option>';
        entidadPagoSelect.dataset.metodoActual = "";
    }
    if (datosPagoDiv) {
        datosPagoDiv.classList.add("oculto");
        datosPagoDiv.innerHTML = "";
    }

    if (modal) {
        modal.classList.add("activo");
    }
}

function cerrarModalPago() {
    const modal = document.getElementById("modalPago");
    if (modal) {
        modal.classList.remove("activo");
    }
}

function mostrarDatosPago() {
    const metodoSelect = document.getElementById("metodoPago");
    const entidadSelect = document.getElementById("entidadPago");
    const datos = document.getElementById("datosPago");

    if (!metodoSelect || !entidadSelect || !datos) return;

    const metodo = metodoSelect.value;
    let entidad = entidadSelect.value;

    datos.classList.add("oculto");
    datos.innerHTML = "";

    if (entidadSelect.dataset.metodoActual !== metodo) {
        entidadSelect.dataset.metodoActual = metodo;
        entidadSelect.innerHTML = '<option value="">Seleccione entidad</option>';

        if (metodo === "Transferencia bancaria") {
            ["BCP", "Interbank", "BBVA", "Scotiabank", "Caja Arequipa"].forEach(function (x) {
                const opt = document.createElement("option");
                opt.value = x;
                opt.textContent = x;
                entidadSelect.appendChild(opt);
            });
        } else if (metodo === "Yape") {
            ["Yape", "BCP"].forEach(function (x) {
                const opt = document.createElement("option");
                opt.value = x;
                opt.textContent = x;
                entidadSelect.appendChild(opt);
            });
        } else if (metodo === "Plin") {
            ["Plin", "Interbank", "BBVA", "Scotiabank", "Caja Arequipa"].forEach(function (x) {
                const opt = document.createElement("option");
                opt.value = x;
                opt.textContent = x;
                entidadSelect.appendChild(opt);
            });
        }

        return;
    }

    entidad = entidadSelect.value;

    if (metodo === "" || entidad === "") {
        return;
    }

    let titularPago = "CrediPlus Financiera";
    let numeroPago = "";

    if (metodo === "Yape") {
        titularPago = "CrediPlus Yape";
        numeroPago = "987 654 321";
    } else if (metodo === "Plin") {
        titularPago = "CrediPlus Plin";
        numeroPago = "912 345 678";
    } else if (metodo === "Transferencia bancaria") {
        titularPago = "CrediPlus Financiera";
        if (entidad === "BCP") numeroPago = "191-23568974-0-11";
        if (entidad === "Interbank") numeroPago = "200-300456789123";
        if (entidad === "BBVA") numeroPago = "0011-0345-0201234567";
        if (entidad === "Scotiabank") numeroPago = "000-8457621";
        if (entidad === "Caja Arequipa") numeroPago = "305-789456123";
    }

    datos.classList.remove("oculto");

    const tituloH3 = document.createElement("h3");
    tituloH3.textContent = "Pago por " + metodo;

    const pEntidad = document.createElement("p");
    pEntidad.innerHTML = "<b>Entidad:</b> " + entidad;

    const pTitular = document.createElement("p");
    pTitular.innerHTML = "<b>Titular:</b> " + titularPago;

    const pNumero = document.createElement("p");
    pNumero.innerHTML = "<b>Número para pagar:</b> " + numeroPago;

    const inputCodigo = document.createElement("input");
    inputCodigo.id = "codigoOperacion";
    inputCodigo.maxLength = 8;
    inputCodigo.placeholder = "Código de operación de 8 dígitos";

    datos.appendChild(tituloH3);
    datos.appendChild(pEntidad);
    datos.appendChild(pTitular);
    datos.appendChild(pNumero);
    datos.appendChild(inputCodigo);
}

function confirmarPago() {
    const metodoSelect = document.getElementById("metodoPago");
    const entidadSelect = document.getElementById("entidadPago");
    const codigoInput = document.getElementById("codigoOperacion");
    const idCuotaInput = document.getElementById("idCuota");
    const montoInput = document.getElementById("montoPagado");

    const metodo = metodoSelect ? metodoSelect.value : "";
    const entidad = entidadSelect ? entidadSelect.value : "";
    const codigo = codigoInput ? codigoInput.value : "";
    const idCuota = idCuotaInput ? idCuotaInput.value : "";
    const montoPagado = montoInput ? montoInput.value : "";

    if (!metodo) {
        alert("Seleccione un método de pago.");
        return;
    }

    if (!entidad) {
        alert("Seleccione una entidad.");
        return;
    }

    if (!codigo || !/^\d{8}$/.test(codigo)) {
        alert("Ingrese un código de operación de 8 dígitos.");
        return;
    }

    const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";

    const params = new URLSearchParams();
    params.append("idCuota", idCuota);
    params.append("montoPagado", montoPagado);
    params.append("metodoPago", metodo);
    params.append("entidadPago", entidad);
    params.append("codigoOperacion", codigo);
    if (csrfToken) {
        params.append("__RequestVerificationToken", csrfToken);
    }

    const headers = {
        "Content-Type": "application/x-www-form-urlencoded"
    };
    if (csrfToken) {
        headers["RequestVerificationToken"] = csrfToken;
    }

    fetch("/Login/RegistrarPagoCuota", {
        method: "POST",
        headers: headers,
        body: params.toString()
    })
    .then(function (response) {
        return response.json();
    })
    .then(function (data) {
        alert(data.mensaje);
        if (data.ok) {
            location.reload();
        }
    })
    .catch(function () {
        alert("No se pudo registrar el pago.");
    });
}
