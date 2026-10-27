/**
 * Configuracion - CrediPlus
 * Manejadores de eventos no intrusivos compatibles con CSP estricto
 */
document.addEventListener("DOMContentLoaded", function () {
    // 1. Pestañas de configuración
    document.querySelectorAll(".tab-btn").forEach(function (btn) {
        btn.addEventListener("click", function () {
            var tabId = this.getAttribute("data-tab");
            if (tabId && typeof mostrarTab === "function") {
                mostrarTab(tabId, this);
            }
        });
    });

    // 2. Interruptores (switches)
    document.querySelectorAll(".switch").forEach(function (sw) {
        sw.addEventListener("click", function () {
            if (typeof toggleSwitch === "function") {
                toggleSwitch(this);
            }
        });
    });

    // 3. Botón Guardar configuración
    var btnGuardar = document.getElementById("btnGuardarConfiguracion");
    if (btnGuardar) {
        btnGuardar.addEventListener("click", function () {
            if (typeof guardarConfiguracion === "function") {
                guardarConfiguracion();
            }
        });
    }

    // 4. Botón Restablecer configuración
    var btnReset = document.getElementById("btnRestablecerConfiguracion");
    if (btnReset) {
        btnReset.addEventListener("click", function () {
            if (typeof restablecerConfiguracion === "function") {
                restablecerConfiguracion();
            }
        });
    }
});
