document.addEventListener("DOMContentLoaded", function () {
    // Dynamic chart styles via datasets (CSP compliant)
    var estadoChart = document.querySelector(".estado-chart[data-gradient]");
    if (estadoChart) {
        estadoChart.style.background = estadoChart.getAttribute("data-gradient");
    }

    var medidorExterno = document.querySelector(".medidor-externo[data-gradient]");
    if (medidorExterno) {
        medidorExterno.style.background = medidorExterno.getAttribute("data-gradient");
    }

    document.querySelectorAll(".bar[data-height]").forEach(function (el) {
        el.style.height = el.getAttribute("data-height") + "px";
    });

    // Event listeners for pagination buttons
    var btnVerMas = document.getElementById("btnVerMas");
    if (btnVerMas) {
        btnVerMas.addEventListener("click", mostrarMas);
    }

    var btnVerMenos = document.getElementById("btnVerMenos");
    if (btnVerMenos) {
        btnVerMenos.addEventListener("click", mostrarMenos);
    }

    var btnVerMasHistorial = document.getElementById("btnVerMasHistorial");
    if (btnVerMasHistorial) {
        btnVerMasHistorial.addEventListener("click", mostrarMasHistorial);
    }

    var btnVerMenosHistorial = document.getElementById("btnVerMenosHistorial");
    if (btnVerMenosHistorial) {
        btnVerMenosHistorial.addEventListener("click", mostrarMenosHistorial);
    }

    var btnAceptarDetalle = document.getElementById("btnAceptarDetallePrograma");
    if (btnAceptarDetalle) {
        btnAceptarDetalle.addEventListener("click", cerrarDetallePrograma);
    }

    document.addEventListener("click", function (e) {
        var btn = e.target.closest(".btn-detalle-programa");
        if (btn) {
            abrirDetallePrograma(
                btn.dataset.cliente || "",
                btn.dataset.dni || "",
                btn.dataset.celular || "",
                btn.dataset.correo || "",
                btn.dataset.monto || "",
                btn.dataset.plazo || "",
                btn.dataset.motivo || "",
                btn.dataset.ocupacion || "",
                btn.dataset.ingreso || "",
                btn.dataset.egreso || "",
                btn.dataset.estado || "",
                btn.dataset.observacion || "",
                btn.dataset.solicitud || ""
            );
        }
    });
});

function mostrarMas() {
    document.querySelectorAll('.fila-extra').forEach(function (f) {
        f.classList.remove('fila-oculta');
        f.style.display = '';
    });

    var btnMas = document.getElementById('btnVerMas');
    if (btnMas) btnMas.style.display = 'none';

    var btnMenos = document.getElementById('btnVerMenos');
    if (btnMenos) btnMenos.style.display = 'inline';

    var contador = document.getElementById('contadorSolicitudes');
    if (contador) contador.style.display = 'none';
}

function mostrarMenos() {
    document.querySelectorAll('.fila-extra').forEach(function (f) {
        f.classList.add('fila-oculta');
        f.style.display = 'none';
    });

    var btnMas = document.getElementById('btnVerMas');
    if (btnMas) btnMas.style.display = 'inline';

    var btnMenos = document.getElementById('btnVerMenos');
    if (btnMenos) btnMenos.style.display = 'none';

    var contador = document.getElementById('contadorSolicitudes');
    if (contador) contador.style.display = 'block';
}

function abrirDetallePrograma(cliente, dni, celular, correo, monto, plazo, motivo, ocupacion, ingreso, egreso, estado, observacion, solicitud) {
    var pSolicitud = document.getElementById("pSolicitud");
    if (pSolicitud) pSolicitud.innerText = "Detalle de Evaluación - " + solicitud;

    var pCliente = document.getElementById("pCliente");
    if (pCliente) pCliente.innerText = cliente;

    var pDni = document.getElementById("pDni");
    if (pDni) pDni.innerText = dni;

    var pCelular = document.getElementById("pCelular");
    if (pCelular) pCelular.innerText = celular;

    var pCorreo = document.getElementById("pCorreo");
    if (pCorreo) pCorreo.innerText = correo;

    var pMonto = document.getElementById("pMonto");
    if (pMonto) pMonto.innerText = "S/ " + monto;

    var pPlazo = document.getElementById("pPlazo");
    if (pPlazo) pPlazo.innerText = plazo + " meses";

    var pMotivo = document.getElementById("pMotivo");
    if (pMotivo) pMotivo.innerText = motivo;

    var pOcupacion = document.getElementById("pOcupacion");
    if (pOcupacion) pOcupacion.innerText = ocupacion;

    var pIngreso = document.getElementById("pIngreso");
    if (pIngreso) pIngreso.innerText = "S/ " + ingreso;

    var pEgreso = document.getElementById("pEgreso");
    if (pEgreso) pEgreso.innerText = "S/ " + egreso;

    var pEstado = document.getElementById("pEstado");
    if (pEstado) pEstado.innerText = estado;

    var pObservacion = document.getElementById("pObservacion");
    if (pObservacion) pObservacion.innerText = observacion;

    var modal = document.getElementById("modalDetallePrograma");
    if (modal) {
        modal.style.display = "flex";
    }
}

function cerrarDetallePrograma() {
    var modal = document.getElementById("modalDetallePrograma");
    if (modal) {
        modal.style.display = "none";
    }
}

function mostrarMasHistorial() {
    document.querySelectorAll('.historial-extra').forEach(function (x) {
        x.classList.remove('fila-oculta');
        x.style.display = '';
    });

    var btnMas = document.getElementById('btnVerMasHistorial');
    if (btnMas) btnMas.style.display = 'none';

    var btnMenos = document.getElementById('btnVerMenosHistorial');
    if (btnMenos) btnMenos.style.display = 'block';

    var contador = document.getElementById('contadorHistorial');
    if (contador) contador.style.display = 'none';
}

function mostrarMenosHistorial() {
    document.querySelectorAll('.historial-extra').forEach(function (x) {
        x.classList.add('fila-oculta');
        x.style.display = 'none';
    });

    var btnMas = document.getElementById('btnVerMasHistorial');
    if (btnMas) btnMas.style.display = 'block';

    var btnMenos = document.getElementById('btnVerMenosHistorial');
    if (btnMenos) btnMenos.style.display = 'none';

    var contador = document.getElementById('contadorHistorial');
    if (contador) contador.style.display = 'block';
}
