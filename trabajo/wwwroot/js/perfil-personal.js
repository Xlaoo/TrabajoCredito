document.addEventListener("DOMContentLoaded", function () {
    const form = document.querySelector("form.profile-card") || document.querySelector("form");
    if (!form) return;

    const correoOriginal = form.dataset.correoOriginal || "";
    const urlPerfil = form.dataset.urlPerfil || window.location.href;
    const urlEnviarCodigo = form.dataset.urlEnviarCodigo || "";

    let editando = false;
    let cambioRealizado = false;

    function activarEdicion() {
        editando = true;

        document.querySelectorAll(".texto").forEach(x => x.style.display = "none");
        document.querySelectorAll(".edit-field").forEach(x => x.style.display = "block");

        const nuevaClaveRow = document.getElementById("nuevaClaveRow");
        if (nuevaClaveRow) nuevaClaveRow.style.display = "flex";

        const reglasClave = document.getElementById("reglasClave");
        if (reglasClave) reglasClave.style.display = "block";

        const btnEditar = document.getElementById("btnEditar");
        if (btnEditar) btnEditar.style.display = "none";

        const btnGuardar = document.getElementById("btnGuardar");
        if (btnGuardar) btnGuardar.style.display = "inline-block";

        const btnCancelar = document.getElementById("btnCancelar");
        if (btnCancelar) btnCancelar.style.display = "inline-block";
    }

    function marcarCambio() {
        cambioRealizado = true;
    }

    function cancelarEdicion() {
        if (cambioRealizado) {
            let salir = confirm("¿Estás seguro que quieres salir sin guardar los datos?");
            if (salir) {
                window.location.href = urlPerfil;
            } else {
                alert("Sigues editando tus datos.");
                return;
            }
        } else {
            window.location.href = urlPerfil;
        }
    }

    function mostrarCodigoCorreo() {
        const correoInput = document.getElementById("correoInput");
        const codigoBox = document.getElementById("codigoBox");
        if (!correoInput || !codigoBox) return;

        let correoNuevo = correoInput.value;
        if (correoNuevo !== correoOriginal) {
            codigoBox.style.display = "block";
        } else {
            codigoBox.style.display = "none";
        }
    }

    function enviarCodigo() {
        const correoInput = document.getElementById("correoInput");
        if (!correoInput || !urlEnviarCodigo) return;

        let correo = correoInput.value;

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

    function validarClave() {
        const claveInput = document.getElementById("claveInput");
        if (!claveInput) return;

        let clave = claveInput.value;

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

    // Filtros de solo números para DNI y Celular
    const dniInput = document.querySelector("input[name='Dni']");
    if (dniInput) {
        dniInput.addEventListener("input", function () {
            this.value = this.value.replace(/[^0-9]/g, '');
        });
    }

    const celularInput = document.querySelector("input[name='Celular']");
    if (celularInput) {
        celularInput.addEventListener("input", function () {
            this.value = this.value.replace(/[^0-9]/g, '');
        });
    }

    // Seguimiento de cambios en campos
    document.querySelectorAll(".edit-field").forEach(input => {
        input.addEventListener("input", marcarCambio);
    });

    const correoInput = document.getElementById("correoInput");
    if (correoInput) {
        correoInput.addEventListener("input", mostrarCodigoCorreo);
    }

    const claveInput = document.getElementById("claveInput");
    if (claveInput) {
        claveInput.addEventListener("input", function () {
            validarClave();
            marcarCambio();
        });
    }

    const btnEditar = document.getElementById("btnEditar");
    if (btnEditar) {
        btnEditar.addEventListener("click", activarEdicion);
    }

    const btnCancelar = document.getElementById("btnCancelar");
    if (btnCancelar) {
        btnCancelar.addEventListener("click", cancelarEdicion);
    }

    const btnObtenerCodigo = document.getElementById("btnObtenerCodigo");
    if (btnObtenerCodigo) {
        btnObtenerCodigo.addEventListener("click", enviarCodigo);
    }

    // Manejo de advertencia al navegar con cambios sin guardar
    document.querySelectorAll(".menu-link").forEach(link => {
        link.addEventListener("click", function (e) {
            if (editando && cambioRealizado) {
                let guardar = confirm("Tienes cambios sin guardar. ¿Deseas guardarlos?");
                if (guardar) {
                    e.preventDefault();
                    form.submit();
                } else {
                    e.preventDefault();
                    alert("Sigues editando tus datos.");
                }
            }
        });
    });

    // Estados preconfigurados desde el servidor
    if (form.dataset.modoEditar === "true") {
        activarEdicion();
    }

    if (form.dataset.correoIntentado && form.dataset.correoIntentado.length > 0) {
        if (correoInput) {
            correoInput.value = form.dataset.correoIntentado;
            mostrarCodigoCorreo();
        }
    }

    if (form.dataset.limpiarCorreo === "true") {
        setTimeout(function () {
            activarEdicion();
            if (correoInput) correoInput.value = "";
            const codigoCorreo = document.getElementById("codigoCorreo");
            if (codigoCorreo) codigoCorreo.value = "";
            const codigoBox = document.getElementById("codigoBox");
            if (codigoBox) codigoBox.style.display = "none";
        }, 100);
    }
});
