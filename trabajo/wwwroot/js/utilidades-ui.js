
function togglePassword(inputId, boton) {
    const input = document.getElementById(inputId);
    const icono = boton.querySelector("i");

    if (input.type === "password") {
        input.type = "text";
        icono.classList.remove("fa-eye");
        icono.classList.add("fa-eye-slash");
    } else {
        input.type = "password";
        icono.classList.remove("fa-eye-slash");
        icono.classList.add("fa-eye");
    }
}

function clearInput(inputId) {
    const input = document.getElementById(inputId);
    const wrap = input.closest(".input-util-wrap");


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

        if (!input || !btnClear) return;

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
    });
});