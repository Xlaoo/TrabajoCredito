/**
 * Script para pantalla Generar Reportes - Administrador CrediPlus
 */
document.addEventListener('DOMContentLoaded', function () {
    const gridTipos = document.querySelector('.gr-tipos-grid');
    const tipoReporteHidden = document.getElementById('tipoReporteHidden');

    if (gridTipos) {
        gridTipos.addEventListener('click', function (e) {
            const card = e.target.closest('.gr-tipo-card');
            if (!card) return;

            const tipo = card.getAttribute('data-tipo');
            if (tipo) {
                seleccionarTipo(card, tipo);
            }
        });
    }

    function seleccionarTipo(card, valor) {
        document.querySelectorAll('.gr-tipo-card').forEach(function (c) {
            c.classList.remove('seleccionado');
        });
        card.classList.add('seleccionado');
        const radio = card.querySelector('input[type=radio]');
        if (radio) {
            radio.checked = true;
        }
        if (tipoReporteHidden) {
            tipoReporteHidden.value = valor;
        }
    }

    // Delegación para confirmación de eliminación de reporte
    document.addEventListener('click', function (e) {
        const btnEliminar = e.target.closest('.gr-btn-eliminar');
        if (btnEliminar) {
            if (!confirm('¿Seguro que deseas eliminar este reporte?')) {
                e.preventDefault();
            }
        }
    });
});
