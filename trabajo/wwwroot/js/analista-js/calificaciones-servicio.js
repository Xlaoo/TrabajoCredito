var todosLosDatos = [];
var filtroPuntaje = "all";
var STAR_COLORS = { 5: "#22c55e", 4: "#eab308", 3: "#f97316", 2: "#ef4444", 1: "#dc2626" };

async function cargarDatos() {
    try {
        var resp = await fetch("/api/calificaciones");
        if (!resp.ok) throw new Error("Error " + resp.status);
        var data = await resp.json();
        todosLosDatos = data.calificaciones || [];

        var elPromedio = document.getElementById("metricPromedio");
        if (elPromedio) elPromedio.textContent = data.puntajePromedio.toFixed(1);

        var elEstrellas = document.getElementById("metricEstrellas");
        if (elEstrellas) {
            elEstrellas.innerHTML = renderStarsHtml(Math.round(data.puntajePromedio), 20)
                + "<br>Últimas " + data.totalCalificaciones + " valoraciones";
        }

        var elTotal = document.getElementById("metricTotal");
        if (elTotal) elTotal.textContent = data.totalCalificaciones;

        var elCrecimiento = document.getElementById("metricCrecimiento");
        if (elCrecimiento) elCrecimiento.textContent = "+15% vs. mes anterior";

        var elNps = document.getElementById("metricNps");
        if (elNps) elNps.textContent = data.nps;

        var elNpsLabel = document.getElementById("metricNpsLabel");
        if (elNpsLabel) elNpsLabel.textContent = "Clasificación: " + data.clasificacionNps;

        renderBarChart(data.distribucionEstrellas);
        renderTabla();
    } catch (err) {
        var body = document.getElementById("tablaBody");
        if (body) {
            body.innerHTML =
                '<tr><td colspan="5"><div class="estado-error"><strong>Error:</strong> ' + escHtml(err.message) + '</div></td></tr>';
        }
    }
}

function setFiltro(valor, btn) {
    filtroPuntaje = valor;
    document.querySelectorAll("#filterPuntaje .btn-filter").forEach(function (b) {
        b.classList.remove("active");
    });
    if (btn) btn.classList.add("active");
    renderTabla();
}

function renderTabla() {
    var searchEl = document.getElementById("searchInput");
    var busqueda = (searchEl ? searchEl.value : "").toLowerCase();
    var filtrados = todosLosDatos.filter(function (c) {
        var okPuntaje = filtroPuntaje === "all" || c.calificacion === parseInt(filtroPuntaje);
        var nombre = (c.nombreCliente + " " + c.apellidoCliente).toLowerCase();
        var okBusq = busqueda === "" || nombre.includes(busqueda) || c.comentario.toLowerCase().includes(busqueda);
        return okPuntaje && okBusq;
    });

    var tbody = document.getElementById("tablaBody");
    var footer = document.getElementById("tableFooter");

    if (filtrados.length === 0) {
        if (tbody) tbody.innerHTML = '<tr><td colspan="5" class="estado-msg">No hay calificaciones que coincidan.</td></tr>';
        if (footer) footer.textContent = "";
        return;
    }

    var filas = filtrados.map(function (c) {
        return '<tr>'
            + '<td><span class="badge-stars badge-' + c.calificacion + '">' + renderStarsHtml(c.calificacion, 14) + '</span></td>'
            + '<td class="cal-comentario">' + escHtml(c.comentario) + '</td>'
            + '<td><strong>' + escHtml(c.nombreCliente) + ' ' + escHtml(c.apellidoCliente) + '</strong></td>'
            + '<td class="fecha-td">' + formatFecha(c.fechaComentario) + '</td>'
            + '<td><span class="analista-badge">' + escHtml(c.analista) + '</span></td>'
            + '</tr>';
    }).join("");

    if (tbody) tbody.innerHTML = filas;
    if (footer) footer.textContent = "Mostrando " + filtrados.length + " de " + todosLosDatos.length + " calificaciones";
}

function renderBarChart(dist) {
    var container = document.getElementById("barChart");
    if (!container) return;
    if (!dist) return;
    var vals = [dist[5] || 0, dist[4] || 0, dist[3] || 0, dist[2] || 0, dist[1] || 0];
    var maxV = Math.max.apply(null, vals.concat([1]));

    container.innerHTML = "";
    [5, 4, 3, 2, 1].forEach(function (s) {
        var count = dist[s] || 0;
        var height = Math.round((count / maxV) * 95);

        var group = document.createElement("div");
        group.className = "bar-group";

        var countSpan = document.createElement("span");
        countSpan.className = "bar-count star-color-" + s;
        countSpan.textContent = count;

        var fillDiv = document.createElement("div");
        fillDiv.className = "bar-fill star-bg-" + s;
        fillDiv.style.height = height + "px";

        var labelSpan = document.createElement("span");
        labelSpan.className = "bar-label";
        labelSpan.textContent = s + " ★";

        group.appendChild(countSpan);
        group.appendChild(fillDiv);
        group.appendChild(labelSpan);
        container.appendChild(group);
    });
}

function renderStarsHtml(puntaje, size) {
    return [1, 2, 3, 4, 5].map(function (s) {
        var color = s <= puntaje ? STAR_COLORS[puntaje] : "#d1d5db";
        return '<svg width="' + size + '" height="' + size + '" viewBox="0 0 20 20" fill="' + color + '" class="star-svg">'
            + '<path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z"/>'
            + '</svg>';
    }).join("");
}

function formatFecha(fechaStr) {
    var date = new Date(fechaStr);
    var now = new Date();
    var diff = (now - date) / 1000;
    if (diff < 60) return "Ahora";
    if (diff < 3600) return "Hace " + Math.floor(diff / 60) + " min";
    if (diff < 86400) return "Hace " + Math.floor(diff / 3600) + " horas";
    var ayer = new Date(now);
    ayer.setDate(ayer.getDate() - 1);
    if (date.toDateString() === ayer.toDateString()) {
        return "Ayer " + date.toLocaleTimeString("es-PE", { hour: "2-digit", minute: "2-digit" });
    }
    return date.toLocaleDateString("es-PE", { day: "2-digit", month: "2-digit", year: "2-digit" })
        + " " + date.toLocaleTimeString("es-PE", { hour: "2-digit", minute: "2-digit" });
}

function escHtml(str) {
    return String(str).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

document.addEventListener("DOMContentLoaded", function () {
    cargarDatos();

    var btnActualizar = document.getElementById("btnActualizarCalificaciones");
    if (btnActualizar) {
        btnActualizar.addEventListener("click", cargarDatos);
    }

    var filterContainer = document.getElementById("filterPuntaje");
    if (filterContainer) {
        filterContainer.addEventListener("click", function (e) {
            var btn = e.target.closest(".btn-filter");
            if (!btn) return;
            var filterVal = btn.getAttribute("data-filter");
            setFiltro(filterVal, btn);
        });
    }

    var searchInput = document.getElementById("searchInput");
    if (searchInput) {
        searchInput.addEventListener("input", renderTabla);
    }
});
