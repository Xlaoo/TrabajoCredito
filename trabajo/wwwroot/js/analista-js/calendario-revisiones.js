document.addEventListener('DOMContentLoaded', function () {
    inicializarCalendario();
});

function inicializarCalendario() {
    var calendarEl = document.getElementById('calendar');
    if (!calendarEl) return;

    if (typeof FullCalendar === 'undefined' || !FullCalendar.Calendar) {
        console.warn('FullCalendar no disponible de inmediato, reintentando...');
        setTimeout(inicializarCalendario, 50);
        return;
    }

    var eventosDataEl = document.getElementById('calendarioEventosData');
    var eventos = [];
    if (eventosDataEl) {
        try {
            eventos = JSON.parse(eventosDataEl.textContent || '[]');
        } catch (e) {
            console.error('Error al analizar eventos del calendario:', e);
        }
    }

    var calendar = new FullCalendar.Calendar(calendarEl, {
        initialView: 'dayGridMonth',
        locale: 'es',
        showNonCurrentDates: false,
        fixedWeekCount: false,
        datesSet: function (info) {
            actualizarAprobacionesMensuales(info.start, info.end);
        },
        events: eventos
    });

    function actualizarAprobacionesMensuales(inicio, fin) {
        let eventosActuales = calendar.getEvents();
        let mesActual = calendar.getDate().getMonth();
        let anioActual = calendar.getDate().getFullYear();

        let total = eventosActuales.filter(e => {
            return e.start &&
                   e.start.getMonth() === mesActual &&
                   e.start.getFullYear() === anioActual;
        }).length;

        var elAprobaciones = document.getElementById("aprobacionesMensuales");
        if (elAprobaciones) {
            elAprobaciones.innerText = total;
        }
    }

    calendar.render();

    var inputBuscar = document.getElementById("buscarCliente");
    if (inputBuscar) {
        inputBuscar.addEventListener("input", filtrarRevisiones);
        inputBuscar.addEventListener("keyup", filtrarRevisiones);
    }

    document.addEventListener("click", function (e) {
        var btn = e.target.closest(".btn-eye");
        if (btn) {
            var id = btn.getAttribute("data-id");
            if (id) {
                obtenerDetalle(id);
            }
        }
    });
}

function obtenerDetalle(id) {
    fetch('/Analista/ObtenerDetallePrestamo?id=' + encodeURIComponent(id))
        .then(response => response.json())
        .then(data => {
            var contenedor = document.getElementById("detalleContenido");
            if (!contenedor) return;

            contenedor.innerHTML = `
                <div class="prestamo-card">
                    <div class="prestamo-titulo">
                        🛡️ Detalle del Préstamo
                    </div>
                    <div class="prestamo-grid">
                        <div>
                            <h5>Información del Cliente</h5>
                            👤 Nombre<br>
                            <strong>${data.cliente}</strong><br><br>
                            🪪 DNI<br>
                            <strong>${data.dni}</strong><br><br>
                            📧 Correo<br>
                            <strong>${data.correo}</strong>
                        </div>
                        <div>
                            <h5>Detalles del Financiamiento</h5>
                            💰 Monto<br>
                            <strong>S/ ${data.monto}</strong><br><br>
                            📊 Cuotas<br>
                            <strong>${data.cuotas} meses</strong><br><br>
                            📈 Tasa<br>
                            <strong>${data.interes}%</strong><br><br>
                            📅 Fecha (Perú)<br>
                            <strong>${data.fecha}</strong><br><br>
                            🕒 Hora (Perú)<br>
                            <strong>${data.hora}</strong><br><br>
                            ${data.fechaAprobacion && data.fechaAprobacion !== 'N/D' ? `<small class="fecha-aprobacion-texto">✅ Aprobado: ${data.fechaAprobacion}</small>` : ''}
                            ${data.fechaSolicitud ? `<small class="fecha-solicitud-texto">📋 Solicitud: ${data.fechaSolicitud}</small>` : ''}
                            <br>
                            <div class="estado-box">
                                <span>Estado actual</span>
                                <strong class="estado-pill">✔ ${data.estado}</strong>
                            </div>
                        </div>
                    </div>
                </div>
            `;

            var modalEl = document.getElementById('detalleModal');
            if (modalEl && window.bootstrap) {
                var modal = bootstrap.Modal.getInstance(modalEl) || new bootstrap.Modal(modalEl);
                modal.show();
            }
        })
        .catch(err => console.error("Error al obtener detalle del préstamo:", err));
}

function filtrarRevisiones() {
    var inputBuscar = document.getElementById("buscarCliente");
    if (!inputBuscar) return;
    var texto = inputBuscar.value.toLowerCase();
    var items = document.querySelectorAll(".revision-item");

    items.forEach(function (item) {
        var nombre = item.dataset.nombre || "";
        var dni = item.dataset.dni || "";

        if (nombre.includes(texto) || dni.includes(texto)) {
            item.style.display = "grid";
        } else {
            item.style.display = "none";
        }
    });
}
