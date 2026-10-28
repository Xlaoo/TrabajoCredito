document.addEventListener("DOMContentLoaded", function () {
    const filtro = document.getElementById("filtroSelectAlertas");
    if (filtro) {
        filtro.addEventListener("change", function () {
            filtrarTabla(this.value);
        });
    }

    const tablaAlertas = document.querySelector(".tabla-alertas");
    if (tablaAlertas) {
        tablaAlertas.addEventListener("click", function (e) {
            const btn = e.target.closest(".btn-marcar-alerta");
            if (!btn) return;
            const tipo = btn.getAttribute("data-tipo");
            const idOrigen = btn.getAttribute("data-id-origen");
            marcarAlertaVista(tipo, idOrigen, btn);
        });
    }

    const toggleBtn = document.getElementById("toggleAlertasRapidas");
    if (toggleBtn) {
        toggleBtn.addEventListener("click", function () {
            const scrollBox = document.querySelector(".alertas-rapidas-scroll");
            if (scrollBox) {
                if (scrollBox.style.display === "none") {
                    scrollBox.style.display = "";
                    toggleBtn.innerHTML = "&#8743;";
                } else {
                    scrollBox.style.display = "none";
                    toggleBtn.innerHTML = "&#8744;";
                }
            }
        });
    }
});

function filtrarTabla(valor) {
    document.querySelectorAll(".tabla-alertas tbody tr").forEach(function (fila) {
        const prioridad = fila.getAttribute("data-prioridad");
        fila.style.display = (valor === "todos" || prioridad === valor) ? "" : "none";
    });
}

function marcarAlertaVista(tipo, idOrigen, boton) {
    fetch("/Analista/MarcarAlertaVista", {
        method: "POST",
        headers: {
            "Content-Type": "application/x-www-form-urlencoded"
        },
        body: "tipo=" + encodeURIComponent(tipo) + "&idOrigen=" + encodeURIComponent(idOrigen)
    })
    .then(r => r.json())
    .then(data => {
        if (data.ok) {
            const fila = boton.closest("tr");
            if (fila) fila.remove();
        }
    })
    .catch(err => {
        console.error("Error al marcar alerta:", err);
    });
}
