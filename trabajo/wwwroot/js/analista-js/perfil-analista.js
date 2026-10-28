let correoOriginal = "";
let cambioRealizado = false;
let urlEnviarCodigo = "/Analista/EnviarCodigoPerfilAnalista";
let urlCancelar = "/Analista/PerfilAnalista";

function activarEdicion() {
    document.querySelectorAll(".texto").forEach(function (x) {
        x.classList.add("hidden-row");
    });
    document.querySelectorAll(".edit-field").forEach(function (x) {
        x.classList.remove("hidden-row");
        x.style.display = "block";
    });

    const nuevaClaveRow = document.getElementById("nuevaClaveRow");
    if (nuevaClaveRow) {
        nuevaClaveRow.classList.remove("hidden-row");
        nuevaClaveRow.style.display = "grid";
    }
    const confirmarClaveRow = document.getElementById("confirmarClaveRow");
    if (confirmarClaveRow) {
        confirmarClaveRow.classList.remove("hidden-row");
        confirmarClaveRow.style.display = "grid";
    }
    const reglasClave = document.getElementById("reglasClave");
    if (reglasClave) {
        reglasClave.classList.remove("hidden-row");
        reglasClave.style.display = "block";
    }

    const btnEditar = document.getElementById("btnEditar");
    if (btnEditar) btnEditar.style.display = "none";
    const btnGuardar = document.getElementById("btnGuardar");
    if (btnGuardar) btnGuardar.style.display = "inline-block";
    const btnCancelar = document.getElementById("btnCancelar");
    if (btnCancelar) {
        btnCancelar.classList.remove("hidden-row");
        btnCancelar.style.display = "inline-block";
    }
    const btnRegresar = document.getElementById("btnRegresar");
    if (btnRegresar) btnRegresar.style.display = "none";
}

function cancelarEdicion() {
    if (cambioRealizado) {
        let salir = confirm("¿Estás seguro que quieres salir sin guardar los cambios?");
        if (!salir) return;
    }
    window.location.href = urlCancelar;
}

function validarClave() {
    let input = document.getElementById("claveInput");
    let clave = input ? input.value : "";

    cambiarRegla("mayus", /[A-Z]/.test(clave), "Una letra mayúscula");
    cambiarRegla("minus", /[a-z]/.test(clave), "Una letra minúscula");
    cambiarRegla("num", /[0-9]/.test(clave), "Un número");
    cambiarRegla("simbolo", /[^a-zA-Z0-9\s]/.test(clave), "Un símbolo especial (!@#$%&*...)");
    cambiarRegla("minimo", clave.length >= 12, "Mínimo 12 caracteres");
}

function cambiarRegla(id, cumple, texto) {
    let item = document.getElementById(id);
    if (!item) return;
    if (cumple) {
        item.className = "ok";
        item.innerText = "✔ " + texto;
    } else {
        item.className = "bad";
        item.innerText = "✖ " + texto;
    }
}

function mostrarCodigoCorreo() {
    let correoInput = document.getElementById("correoInput");
    let correoNuevo = correoInput ? correoInput.value : "";
    let codigoBox = document.getElementById("codigoBox");
    if (!codigoBox) return;

    if (correoNuevo !== correoOriginal) {
        codigoBox.style.display = "block";
    } else {
        codigoBox.style.display = "none";
    }
}

function enviarCodigo() {
    let correoInput = document.getElementById("correoInput");
    let correo = correoInput ? correoInput.value : "";

    fetch(urlEnviarCodigo, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded'
        },
        body: 'correo=' + encodeURIComponent(correo)
    })
    .then(response => response.json())
    .then(data => {
        alert(data.mensaje);
    })
    .catch(error => {
        alert("No se pudo enviar el código.");
    });
}

function togglePassword(inputId, boton) {
    const input = document.getElementById(inputId);
    if (!input) return;
    const icono = boton ? boton.querySelector("i") : null;

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

document.addEventListener("DOMContentLoaded", function () {
    const form = document.getElementById("formPerfilAnalista");
    if (form) {
        correoOriginal = form.getAttribute("data-correo-original") || "";
        urlEnviarCodigo = form.getAttribute("data-url-enviar-codigo") || "/Analista/EnviarCodigoPerfilAnalista";
        urlCancelar = form.getAttribute("data-url-cancelar") || "/Analista/PerfilAnalista";
        const modoEditar = form.getAttribute("data-modo-editar") === "true";
        const correoIntentado = form.getAttribute("data-correo-intentado") || "";

        if (modoEditar) {
            activarEdicion();
        }
        if (correoIntentado) {
            const cInput = document.getElementById("correoInput");
            if (cInput) {
                cInput.value = correoIntentado;
                mostrarCodigoCorreo();
            }
        }
    }

    const dniInput = document.getElementById("Dni");
    if (dniInput) {
        dniInput.addEventListener("input", function () {
            this.value = this.value.replace(/[^0-9]/g, "");
        });
    }

    const celInput = document.getElementById("Celular");
    if (celInput) {
        celInput.addEventListener("input", function () {
            this.value = this.value.replace(/[^0-9]/g, "");
        });
    }

    const correoInput = document.getElementById("correoInput");
    if (correoInput) {
        correoInput.addEventListener("input", function () {
            mostrarCodigoCorreo();
        });
    }

    const btnCodigo = document.getElementById("btnEnviarCodigoCorreo");
    if (btnCodigo) {
        btnCodigo.addEventListener("click", enviarCodigo);
    }

    const claveInput = document.getElementById("claveInput");
    if (claveInput) {
        claveInput.addEventListener("input", function () {
            validarClave();
            cambioRealizado = true;
        });
    }

    const confirmarClaveInput = document.getElementById("confirmarClaveInput");
    if (confirmarClaveInput) {
        confirmarClaveInput.addEventListener("input", function () {
            cambioRealizado = true;
        });
    }

    document.querySelectorAll(".btn-eye").forEach(function (btn) {
        btn.addEventListener("click", function () {
            const target = this.getAttribute("data-target");
            if (target) togglePassword(target, this);
        });
    });

    const btnEditar = document.getElementById("btnEditar");
    if (btnEditar) {
        btnEditar.addEventListener("click", activarEdicion);
    }

    const btnCancelar = document.getElementById("btnCancelar");
    if (btnCancelar) {
        btnCancelar.addEventListener("click", cancelarEdicion);
    }

    document.querySelectorAll(".edit-field").forEach(function (input) {
        input.addEventListener("input", function () {
            cambioRealizado = true;
        });
        input.addEventListener("change", function () {
            cambioRealizado = true;
        });
    });
});
