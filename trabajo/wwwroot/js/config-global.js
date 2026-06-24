window.addEventListener("load", function () {
    let guardado = localStorage.getItem("configCrediPlus");

    if (guardado) {
        let config = JSON.parse(guardado);
        cargarValores(config);
        aplicarConfiguracion(config);
    }
});

function guardarConfiguracion() {
    let config = {
        modoVisual: document.getElementById("modoVisual")?.value || "claro",
        colorPrincipal: document.getElementById("colorPrincipal")?.value || "morado",
        tamanoTexto: document.getElementById("tamanoTexto")?.value || "normal",
        redondeado: document.getElementById("redondeado")?.value || "normal",
        espaciado: document.getElementById("espaciado")?.value || "normal",

        vistaCompacta: document.getElementById("vistaCompacta")?.classList.contains("active") || false,
        animaciones: document.getElementById("animaciones")?.classList.contains("active") || false,
        altoContraste: document.getElementById("altoContraste")?.classList.contains("active") || false,
        botonesGrandes: document.getElementById("botonesGrandes")?.classList.contains("active") || false,
        ayudaRapida: document.getElementById("ayudaRapida")?.classList.contains("active") || false,
        recordatoriosPago: document.getElementById("recordatoriosPago")?.classList.contains("active") || false,
        alertasCambios: document.getElementById("alertasCambios")?.classList.contains("active") || false,
        sonidoAlertas: document.getElementById("sonidoAlertas")?.classList.contains("active") || false,
        mensajeBienvenida: document.getElementById("mensajeBienvenida")?.classList.contains("active") || false,
        mostrarFaq: document.getElementById("mostrarFaq")?.classList.contains("active") || false,
        resaltarBotones: document.getElementById("resaltarBotones")?.classList.contains("active") || false
    };

    localStorage.setItem("configCrediPlus", JSON.stringify(config));
    aplicarConfiguracion(config);

    let mensaje = document.getElementById("mensajeOk");
    if (mensaje) {
        mensaje.style.display = "block";
        mensaje.innerText = "Configuración guardada correctamente.";

        setTimeout(() => {
            mensaje.style.display = "none";
        }, 3000);
    } else {
        alert("Configuración guardada correctamente.");
    }
}

function aplicarConfiguracion(config) {
    if (config.modoVisual === "oscuro") {
        document.body.style.background = "#0f172a";
        document.body.style.color = "white";
    } else {
        document.body.style.background = "#faf8ff";
        document.body.style.color = "#1f1235";
    }

    let color = "#7c3aed";

    if (config.colorPrincipal === "azul") color = "#2563eb";
    if (config.colorPrincipal === "verde") color = "#16a34a";
    if (config.colorPrincipal === "rojo") color = "#dc2626";
    if (config.colorPrincipal === "negro") color = "#111827";

    document.querySelectorAll("h1, h2, h3, .section-title").forEach(x => {
        x.style.color = color;
    });

    document.querySelectorAll(".tab-btn").forEach(x => {
        x.style.color = "#6b7280";
        x.style.borderBottom = "4px solid transparent";
        x.style.boxShadow = "none";
        x.style.background = "transparent";
    });

    document.querySelectorAll(".tab-btn.active").forEach(x => {
        x.style.color = color;
        x.style.borderBottom = "4px solid " + color;
        x.style.boxShadow = "none";
        x.style.background = "transparent";
    });

    document.querySelectorAll(".btn-save").forEach(x => {
        x.style.background = color;
    });

    if (config.tamanoTexto === "grande") {
        document.body.style.fontSize = "18px";
    } else if (config.tamanoTexto === "muyGrande") {
        document.body.style.fontSize = "21px";
    } else {
        document.body.style.fontSize = "16px";
    }

    document.querySelectorAll(".setting-box, .profile-card, .request-card, .loan-card, .card").forEach(x => {
        x.style.padding = config.vistaCompacta ? "14px" : "24px";

        if (config.redondeado === "grande") x.style.borderRadius = "32px";
        if (config.redondeado === "normal") x.style.borderRadius = "20px";
        if (config.redondeado === "cuadrado") x.style.borderRadius = "8px";

        x.style.border = config.altoContraste ? "3px solid #4c1d95" : "1px solid #ede9fe";
    });

    document.querySelectorAll("button").forEach(x => {
        x.style.transition = config.animaciones ? ".3s" : "none";

        if (config.botonesGrandes) {
            x.style.fontSize = "20px";
            x.style.padding = "18px 32px";
        }
    });

    document.querySelectorAll(".faq").forEach(x => {
        x.style.display = config.mostrarFaq ? "block" : "none";
    });
    document.querySelectorAll(".content").forEach(x => {

        if (config.espaciado === "amplio") {
            x.style.padding = "60px 80px";
        }

        else if (config.espaciado === "compacto") {
            x.style.padding = "20px 30px";
        }

        else {
            x.style.padding = "42px 58px";
        }
    });
    document.querySelectorAll("button:not(.tab-btn), .btn-save, .btn-edit, .btn-cancel").forEach(x => {

        x.style.boxShadow = config.resaltarBotones
            ? "0 10px 22px rgba(124,58,237,.25)"
            : "none";
    });
}

function toggleSwitch(elemento) {
    elemento.classList.toggle("active");
}

function mostrarTab(tabId, boton) {
    document.querySelectorAll(".tab-content").forEach(tab => {
        tab.classList.remove("active");
    });

    document.querySelectorAll(".tab-btn").forEach(btn => {
        btn.classList.remove("active");
        btn.style.borderBottom = "4px solid transparent";
    });

    document.getElementById(tabId).classList.add("active");
    boton.classList.add("active");

    let config = JSON.parse(localStorage.getItem("configCrediPlus")) || {};
    aplicarConfiguracion(config);
}

function restablecerConfiguracion() {
    let confirmar = confirm("¿Deseas restablecer la configuración por defecto?");

    if (confirmar) {
        localStorage.removeItem("configCrediPlus");
        location.reload();
    }
}

function cargarValores(config) {
    if (document.getElementById("modoVisual")) document.getElementById("modoVisual").value = config.modoVisual;
    if (document.getElementById("colorPrincipal")) document.getElementById("colorPrincipal").value = config.colorPrincipal;
    if (document.getElementById("tamanoTexto")) document.getElementById("tamanoTexto").value = config.tamanoTexto;
    if (document.getElementById("redondeado")) document.getElementById("redondeado").value = config.redondeado;
    if (document.getElementById("espaciado")) document.getElementById("espaciado").value = config.espaciado;

    cargarSwitch("vistaCompacta", config.vistaCompacta);
    cargarSwitch("animaciones", config.animaciones);
    cargarSwitch("altoContraste", config.altoContraste);
    cargarSwitch("botonesGrandes", config.botonesGrandes);
    cargarSwitch("ayudaRapida", config.ayudaRapida);
    cargarSwitch("recordatoriosPago", config.recordatoriosPago);
    cargarSwitch("alertasCambios", config.alertasCambios);
    cargarSwitch("sonidoAlertas", config.sonidoAlertas);
    cargarSwitch("mensajeBienvenida", config.mensajeBienvenida);
    cargarSwitch("mostrarFaq", config.mostrarFaq);
    cargarSwitch("resaltarBotones", config.resaltarBotones);
}

function cargarSwitch(id, activo) {
    let elemento = document.getElementById(id);

    if (!elemento) return;

    if (activo) {
        elemento.classList.add("active");
    } else {
        elemento.classList.remove("active");
    }
}