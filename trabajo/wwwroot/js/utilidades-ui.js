
function togglePassword(inputId, boton) {
    if (!inputId || !boton) return;
    const input = document.getElementById(inputId);
    if (!input) return;
    const icono = boton.querySelector("i");

    if (input.type === "password") {
        input.type = "text";
        if (icono) {
            icono.classList.remove("fa-eye");
            icono.classList.add("fa-eye-slash");
        }
    } else {
        input.type = "password";
        if (icono) {
            icono.classList.remove("fa-eye-slash");
            icono.classList.add("fa-eye");
        }
    }
}

function clearInput(inputId) {
    const input = document.getElementById(inputId);
    const wrap = input ? input.closest(".input-util-wrap") : null;

    if (!input) return;

    input.value = "";
    input.focus();

    // Si estaba mostrando la contraseña, volver a ocultarla
    if (input.type === "text" && wrap && wrap.querySelector(".btn-eye")) {
        input.type = "password";
        const icono = wrap.querySelector(".btn-eye i");
        if (icono) {
            icono.classList.remove("fa-eye-slash");
            icono.classList.add("fa-eye");
        }
    }

    // Disparar evento para ocultar solo el botón X
    input.dispatchEvent(new Event("input"));
}

document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll(".input-util-wrap").forEach(function (wrap) {
        const input = wrap.querySelector("input");
        const btnClear = wrap.querySelector(".btn-clear");
        const btnEye = wrap.querySelector(".btn-eye");

        if (!input) return;

        // Solo controla la X — el ojo siempre visible via CSS
        function actualizarBotones() {
            const tieneTexto = input.value.length > 0;
            if (btnClear) { btnClear.style.display = tieneTexto ? "flex" : "none"; }
            if (btnEye) { btnEye.style.display = tieneTexto ? "flex" : "none"; }
        }

        // Estado inicial
        actualizarBotones();

        // Actualizar al escribir
        input.addEventListener("input", actualizarBotones);

        // Vinculación segura de eventos para compatibilidad CSP (addEventListener)
        // Se remueve el atributo onclick inline para evitar ejecuciones duplicadas
        // y asegurar que el listener addEventListener sea el único controlador activo.
        if (btnEye && !btnEye.dataset.cspBound) {
            btnEye.dataset.cspBound = "true";
            btnEye.removeAttribute("onclick");
            btnEye.addEventListener("click", function (e) {
                e.preventDefault();
                togglePassword(input.id, btnEye);
            });
        }

        if (btnClear && !btnClear.dataset.cspBound) {
            btnClear.dataset.cspBound = "true";
            btnClear.removeAttribute("onclick");
            btnClear.addEventListener("click", function (e) {
                e.preventDefault();
                clearInput(input.id);
            });
        }
    });
});