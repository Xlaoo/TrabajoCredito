// Reportes de Administración - Panel Administrador CrediPlus
document.addEventListener("DOMContentLoaded", () => {
    // Aplicar anchos de barras de progreso vía CSSOM
    document.querySelectorAll("[data-progress]").forEach(el => {
        const val = el.getAttribute("data-progress");
        if (val !== null && val !== "") {
            el.style.width = val + "%";
        }
    });

    const datosEl = document.getElementById("reportesAdminDatos");
    if (!datosEl) return;

    let meses = [];
    let totales = [];
    let tiposCredito = [];
    let cantidadesTipo = [];

    try {
        meses = JSON.parse(datosEl.dataset.meses || "[]");
        totales = JSON.parse(datosEl.dataset.totales || "[]");
        tiposCredito = JSON.parse(datosEl.dataset.tipos || "[]");
        cantidadesTipo = JSON.parse(datosEl.dataset.cantidades || "[]");
    } catch (e) {
        console.error("Error al parsear datos de reportes:", e);
    }

    // Gráfico de Líneas: Solicitudes por Mes
    const canvasSolicitudes = document.getElementById("chartSolicitudes");
    if (canvasSolicitudes && typeof Chart !== "undefined") {
        new Chart(canvasSolicitudes, {
            type: "line",
            data: {
                labels: meses,
                datasets: [{
                    data: totales,
                    borderColor: "#6d28d9",
                    backgroundColor: "rgba(109,40,217,.08)",
                    fill: true,
                    tension: 0.4,
                    pointBackgroundColor: "#6d28d9",
                    pointRadius: 5
                }]
            },
            options: {
                plugins: { legend: { display: false } },
                scales: {
                    y: { beginAtZero: true, grid: { color: "#f3f4f6" } },
                    x: { grid: { display: false } }
                }
            }
        });
    }

    // Gráfico de Dona: Créditos por Tipo
    const canvasTipo = document.getElementById("chartTipo");
    if (canvasTipo && typeof Chart !== "undefined") {
        const coloresPorTipo = {
            "Estudios": "#6d28d9",
            "Salud": "#3b82f6",
            "Negocio": "#10b981",
            "Emergencia": "#f59e0b",
            "Compra personal": "#ef4444",
            "Vivienda": "#14b8a6",
            "Otros": "#8b5cf6",
            "Sin motivo": "#8b5cf6"
        };
        const coloresTipo = tiposCredito.map(t => coloresPorTipo[t] || "#8b5cf6");

        new Chart(canvasTipo, {
            type: "doughnut",
            data: {
                labels: tiposCredito,
                datasets: [{
                    data: cantidadesTipo,
                    backgroundColor: coloresTipo,
                    borderWidth: 0
                }]
            },
            options: {
                cutout: "70%",
                plugins: { legend: { display: false } }
            }
        });
    }

    // Selector de Año
    const selectAnio = document.getElementById("selectAnioReporte");
    if (selectAnio) {
        selectAnio.addEventListener("change", (e) => {
            const valor = e.target.value;
            const hoy = new Date();
            let anio = hoy.getFullYear();

            if (valor === "anterior") {
                anio = anio - 1;
            }

            const inicio = anio + "-01-01";
            const fin = anio + "-12-31";

            const url = new URL(window.location.href);
            url.searchParams.set("fechaInicio", inicio);
            url.searchParams.set("fechaFin", fin);

            window.location.href = url.toString();
        });
    }
});
