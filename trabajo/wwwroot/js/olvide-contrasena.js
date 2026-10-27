// ====================================================================
// CREDIPLUS - RECUPERACIÓN DE CONTRASEÑA
// Script modular externo sin dependencias inline para CSP estricta
// ====================================================================

(function () {
    "use strict";

    let intervaloCodigoRecuperacion = null;
    let segundosCodigoRecuperacion = 0;

    function mostrarMensaje(texto, correcto) {
        const mensaje = document.getElementById("mensajeRecuperacion");
        if (!mensaje) return;

        mensaje.style.display = "block";
        mensaje.innerHTML = correcto ? "✅ " + texto : "❌ " + texto;

        if (correcto) {
            mensaje.className = "mensaje mensaje-ok";
        } else {
            mensaje.className = "mensaje mensaje-error";
        }

        mensaje.scrollIntoView({
            behavior: "smooth",
            block: "center"
        });
    }

    function iniciarContadorRecuperacion(segundos) {
        if (intervaloCodigoRecuperacion) {
            clearInterval(intervaloCodigoRecuperacion);
        }

        segundosCodigoRecuperacion = segundos;

        const contador = document.getElementById("contadorCodigo");
        const botonReenviar = document.getElementById("btnReenviarCodigo");
        const botonVerificar = document.getElementById("btnVerificarCodigo");

        if (botonReenviar) {
            botonReenviar.classList.add("elemento-oculto");
            botonReenviar.style.display = "none";
        }
        if (botonVerificar) botonVerificar.disabled = false;

        function actualizar() {
            const minutos = Math.floor(segundosCodigoRecuperacion / 60);
            const segundosRestantes = segundosCodigoRecuperacion % 60;

            if (contador) {
                contador.innerHTML = "Código válido por " +
                    String(minutos).padStart(2, "0") + ":" +
                    String(segundosRestantes).padStart(2, "0");
            }

            if (segundosCodigoRecuperacion <= 0) {
                clearInterval(intervaloCodigoRecuperacion);
                intervaloCodigoRecuperacion = null;

                if (contador) {
                    contador.innerHTML = "Código expirado";
                    contador.style.color = "#dc2626";
                }
                if (botonVerificar) botonVerificar.disabled = true;
                if (botonReenviar) botonReenviar.style.display = "block";
                return;
            }

            segundosCodigoRecuperacion--;
        }

        if (contador) {
            contador.classList.remove("contador-codigo-expirado");
            contador.style.color = "#4f46e5";
        }
        actualizar();
        intervaloCodigoRecuperacion = setInterval(actualizar, 1000);
    }

    async function enviarRecuperacion() {
        const inputDni = document.getElementById("dniRecuperar");
        const inputCorreo = document.getElementById("correoRecuperar");
        const boton = document.getElementById("btnEnviarRecuperacion");

        if (!inputDni || !inputCorreo || !boton) return;

        const dni = inputDni.value.trim();
        const correo = inputCorreo.value.trim();

        if (!/^\d{8}$/.test(dni)) {
            mostrarMensaje("El DNI debe tener exactamente 8 números.", false);
            return;
        }

        if (!/^[A-Za-z0-9._%+-]+@gmail\.com$/i.test(correo)) {
            mostrarMensaje("Ingresa un correo Gmail válido.", false);
            return;
        }

        const turnstileInput = document.querySelector('[name="cf-turnstile-response"]');
        const turnstileToken = turnstileInput ? turnstileInput.value : "";
        const turnstileDiv = document.getElementById("cf-turnstile-recuperacion");
        if (turnstileDiv && !turnstileToken) {
            mostrarMensaje("Verificación de seguridad pendiente. Por favor espera un momento...", false);
            return;
        }

        const textoOriginal = boton.innerHTML;
        boton.disabled = true;
        boton.innerHTML = '<span class="spinner"></span>' + 'Enviando correo...';

        try {
            const datos = new URLSearchParams();
            datos.append("dni", dni);
            datos.append("correo", correo);

            if (turnstileToken) {
                datos.append("cf-turnstile-response", turnstileToken);
            }

            const respuesta = await fetch("/Login/SolicitarRecuperacion", {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded"
                },
                body: datos.toString()
            });

            const resultado = await respuesta.json();

            if (!resultado.ok) {
                if (typeof turnstile !== "undefined") {
                    try { turnstile.reset(); } catch (e) {}
                }

                mostrarMensaje(resultado.mensaje, false);
                boton.disabled = false;
                boton.innerHTML = textoOriginal;
                return;
            }

            mostrarMensaje(resultado.mensaje, true);

            inputDni.readOnly = true;
            inputCorreo.readOnly = true;

            boton.innerHTML = "✓ Código enviado";

            const btnOtra = document.getElementById("btnOtraCuenta");
            if (btnOtra) btnOtra.style.display = "block";

            const zonaCodigo = document.getElementById("zonaCodigoRecuperacion");
            if (zonaCodigo) zonaCodigo.style.display = "block";

            const inputCodigo = document.getElementById("codigoRecuperacion");
            if (inputCodigo) inputCodigo.value = "";

            iniciarContadorRecuperacion(resultado.segundos || 60);

            setTimeout(function () {
                if (inputCodigo) inputCodigo.focus();
            }, 200);
        } catch (error) {
            console.error(error);
            mostrarMensaje("No se pudo enviar la solicitud. Inténtalo nuevamente.", false);
            boton.disabled = false;
            boton.innerHTML = textoOriginal;
        }
    }

    async function verificarCodigoRecuperacion() {
        const inputDni = document.getElementById("dniRecuperar");
        const inputCorreo = document.getElementById("correoRecuperar");
        const inputCodigo = document.getElementById("codigoRecuperacion");
        const boton = document.getElementById("btnVerificarCodigo");

        if (!inputDni || !inputCorreo || !inputCodigo || !boton) return;

        const dni = inputDni.value.trim();
        const correo = inputCorreo.value.trim();
        const codigo = inputCodigo.value.trim().toUpperCase();

        if (!/^[A-Z0-9]{6}$/.test(codigo)) {
            mostrarMensaje("Ingresa el código completo de 6 caracteres.", false);
            return;
        }

        const textoOriginal = boton.innerHTML;
        boton.disabled = true;
        boton.innerHTML = '<span class="spinner"></span>' + 'Verificando...';

        try {
            const datos = new URLSearchParams();
            datos.append("dni", dni);
            datos.append("correo", correo);
            datos.append("codigo", codigo);

            const respuesta = await fetch("/Login/VerificarCodigoRecuperacion", {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded"
                },
                body: datos.toString()
            });

            const resultado = await respuesta.json();

            if (!resultado.ok) {
                mostrarMensaje(resultado.mensaje, false);
                boton.innerHTML = textoOriginal;

                if (resultado.expirado) {
                    boton.disabled = true;
                    const contador = document.getElementById("contadorCodigo");
                    if (contador) {
                        contador.innerHTML = "Código expirado";
                        contador.style.color = "#dc2626";
                    }
                    const btnReenviar = document.getElementById("btnReenviarCodigo");
                    if (btnReenviar) btnReenviar.style.display = "block";
                } else {
                    boton.disabled = false;
                }
                return;
            }

            mostrarMensaje(resultado.mensaje, true);
            boton.innerHTML = "✓ Identidad verificada";

            setTimeout(function () {
                window.location.href = resultado.redirectUrl;
            }, 900);
        } catch (error) {
            console.error(error);
            mostrarMensaje("No se pudo verificar el código.", false);
            boton.disabled = false;
            boton.innerHTML = textoOriginal;
        }
    }

    async function reenviarCodigoRecuperacion() {
        const inputDni = document.getElementById("dniRecuperar");
        const inputCorreo = document.getElementById("correoRecuperar");
        const boton = document.getElementById("btnReenviarCodigo");
        const inputCodigo = document.getElementById("codigoRecuperacion");

        if (!inputDni || !inputCorreo || !boton) return;

        const dni = inputDni.value.trim();
        const correo = inputCorreo.value.trim();

        boton.disabled = true;
        boton.innerHTML = '<span class="spinner"></span>' + 'Enviando nuevo código...';

        try {
            const datos = new URLSearchParams();
            datos.append("dni", dni);
            datos.append("correo", correo);

            const respuesta = await fetch("/Login/SolicitarRecuperacion", {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded"
                },
                body: datos.toString()
            });

            const resultado = await respuesta.json();

            if (!resultado.ok) {
                mostrarMensaje(resultado.mensaje, false);

                if (resultado.esperando && resultado.segundosRestantes) {
                    iniciarContadorRecuperacion(resultado.segundosRestantes);
                }

                boton.disabled = false;
                boton.innerHTML = "🔄 Reenviar código";
                return;
            }

            if (inputCodigo) {
                inputCodigo.value = "";
                inputCodigo.disabled = false;
            }

            mostrarMensaje("Te enviamos un nuevo código de seguridad.", true);

            boton.classList.add("elemento-oculto");
            boton.style.display = "none";
            boton.disabled = false;
            boton.innerHTML = "🔄 Reenviar código";

            const btnVerificar = document.getElementById("btnVerificarCodigo");
            if (btnVerificar) btnVerificar.disabled = false;

            iniciarContadorRecuperacion(resultado.segundos || 60);

            if (inputCodigo) inputCodigo.focus();
        } catch (error) {
            console.error(error);
            mostrarMensaje("No se pudo reenviar el código.", false);
            boton.disabled = false;
            boton.innerHTML = "🔄 Reenviar código";
        }
    }

    function usarOtraCuenta() {
        const dni = document.getElementById("dniRecuperar");
        const correo = document.getElementById("correoRecuperar");
        const botonEnviar = document.getElementById("btnEnviarRecuperacion");
        const botonOtraCuenta = document.getElementById("btnOtraCuenta");
        const mensaje = document.getElementById("mensajeRecuperacion");

        if (dni) {
            dni.readOnly = false;
            dni.value = "";
        }
        if (correo) {
            correo.readOnly = false;
            correo.value = "";
        }

        if (botonEnviar) {
            botonEnviar.disabled = false;
            botonEnviar.innerHTML = "📩 Enviar código de recuperación";
        }

        if (botonOtraCuenta) {
            botonOtraCuenta.classList.add("elemento-oculto");
            botonOtraCuenta.style.display = "none";
        }

        if (mensaje) {
            mensaje.classList.add("elemento-oculto");
            mensaje.style.display = "none";
            mensaje.innerHTML = "";
        }

        const zonaCodigo = document.getElementById("zonaCodigoRecuperacion");
        const codigo = document.getElementById("codigoRecuperacion");

        if (zonaCodigo) {
            zonaCodigo.classList.add("elemento-oculto");
            zonaCodigo.style.display = "none";
        }
        if (codigo) codigo.value = "";

        if (dni) dni.focus();
    }

    async function cambiarContrasena() {
        const inputNueva = document.getElementById("nuevaClave");
        const inputConfirmar = document.getElementById("confirmarClave");
        const boton = document.getElementById("btnCambiarClave");

        if (!inputNueva || !inputConfirmar || !boton) return;

        const nuevaClave = inputNueva.value;
        const confirmarClave = inputConfirmar.value;

        const tieneMayus = /[A-Z]/.test(nuevaClave);
        const tieneMinus = /[a-z]/.test(nuevaClave);
        const tieneNumero = /[0-9]/.test(nuevaClave);
        const tieneSimbolo = /[^a-zA-Z0-9\s]/.test(nuevaClave);
        const tieneLongitud = nuevaClave.length >= 12;

        if (!tieneMayus || !tieneMinus || !tieneNumero || !tieneSimbolo || !tieneLongitud) {
            mostrarMensaje("La contraseña debe tener mínimo 12 caracteres, al menos una mayúscula, una minúscula, un número y un símbolo especial.", false);
            return;
        }

        if (nuevaClave !== confirmarClave) {
            mostrarMensaje("Las contraseñas no coinciden.", false);
            return;
        }

        const textoOriginal = boton.innerHTML;
        boton.disabled = true;
        boton.innerHTML = '<span class="spinner"></span>' + 'Actualizando...';

        try {
            const datos = new URLSearchParams();
            datos.append("nuevaClave", nuevaClave);
            datos.append("confirmarClave", confirmarClave);

            const respuesta = await fetch("/Login/CambiarContrasenaRecuperada", {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded"
                },
                body: datos.toString()
            });

            const resultado = await respuesta.json();

            if (!resultado.ok) {
                mostrarMensaje(resultado.mensaje, false);
                boton.disabled = false;
                boton.innerHTML = textoOriginal;
                return;
            }

            mostrarMensaje(resultado.mensaje, true);
            boton.innerHTML = "✓ Contraseña actualizada";

            setTimeout(function () {
                window.location.href = resultado.redirectUrl;
            }, 1800);
        } catch (error) {
            console.error(error);
            mostrarMensaje("No se pudo cambiar la contraseña. Inténtalo nuevamente.", false);
            boton.disabled = false;
            boton.innerHTML = textoOriginal;
        }
    }

    function cerrarModalRecuperacion() {
        const modal = document.getElementById("modalRecuperacionOk");
        if (modal) {
            modal.remove();
        }
    }

    function inicializarOlvideContrasena() {
        // Restricción DNI números
        const inputDni = document.getElementById("dniRecuperar");
        if (inputDni && !inputDni.dataset.recBound) {
            inputDni.dataset.recBound = "true";
            inputDni.addEventListener("input", function () {
                this.value = this.value.replace(/[^0-9]/g, "");
            });
        }

        // Restricción Código alfanumérico mayúsculas
        const inputCodigo = document.getElementById("codigoRecuperacion");
        if (inputCodigo && !inputCodigo.dataset.recBound) {
            inputCodigo.dataset.recBound = "true";
            inputCodigo.addEventListener("input", function () {
                this.value = this.value.toUpperCase().replace(/[^A-Z0-9]/g, "");
            });
        }

        // Botón Enviar Recuperación
        const btnEnviar = document.getElementById("btnEnviarRecuperacion");
        if (btnEnviar && !btnEnviar.dataset.recBound) {
            btnEnviar.dataset.recBound = "true";
            btnEnviar.addEventListener("click", function (e) {
                e.preventDefault();
                enviarRecuperacion();
            });
        }

        // Botón Usar Otra Cuenta
        const btnOtra = document.getElementById("btnOtraCuenta");
        if (btnOtra && !btnOtra.dataset.recBound) {
            btnOtra.dataset.recBound = "true";
            btnOtra.addEventListener("click", function (e) {
                e.preventDefault();
                usarOtraCuenta();
            });
        }

        // Botón Verificar Código
        const btnVerificar = document.getElementById("btnVerificarCodigo");
        if (btnVerificar && !btnVerificar.dataset.recBound) {
            btnVerificar.dataset.recBound = "true";
            btnVerificar.addEventListener("click", function (e) {
                e.preventDefault();
                verificarCodigoRecuperacion();
            });
        }

        // Botón Reenviar Código
        const btnReenviar = document.getElementById("btnReenviarCodigo");
        if (btnReenviar && !btnReenviar.dataset.recBound) {
            btnReenviar.dataset.recBound = "true";
            btnReenviar.addEventListener("click", function (e) {
                e.preventDefault();
                reenviarCodigoRecuperacion();
            });
        }

        // Formulario Cambiar Clave
        const formCambiar = document.getElementById("formCambiarClave");
        if (formCambiar && !formCambiar.dataset.recBound) {
            formCambiar.dataset.recBound = "true";
            formCambiar.addEventListener("submit", function (e) {
                e.preventDefault();
                cambiarContrasena();
            });
        }

        // Botón Cerrar Modal Exito
        const btnCerrarModal = document.getElementById("btnCerrarModalRecuperacion");
        if (btnCerrarModal && !btnCerrarModal.dataset.recBound) {
            btnCerrarModal.dataset.recBound = "true";
            btnCerrarModal.addEventListener("click", function (e) {
                e.preventDefault();
                cerrarModalRecuperacion();
            });
        }

        // Reglas visuales de contraseña
        const inputClave = document.getElementById("nuevaClave");
        const mayus = document.getElementById("mayus");
        const minus = document.getElementById("minus");
        const numero = document.getElementById("numero");
        const simbolo = document.getElementById("simbolo");
        const longitud = document.getElementById("longitud");

        if (inputClave && !inputClave.dataset.recRulesBound) {
            inputClave.dataset.recRulesBound = "true";
            inputClave.addEventListener("input", function () {
                const valor = inputClave.value;

                if (mayus) {
                    if (/[A-Z]/.test(valor)) {
                        mayus.innerHTML = "✔ Una letra mayúscula";
                        mayus.classList.add("regla-valida");
                        mayus.classList.remove("regla-invalida");
                        mayus.style.color = "green";
                    } else {
                        mayus.innerHTML = "○ Una letra mayúscula";
                        mayus.classList.add("regla-invalida");
                        mayus.classList.remove("regla-valida");
                        mayus.style.color = "#64748b";
                    }
                }

                if (minus) {
                    if (/[a-z]/.test(valor)) {
                        minus.innerHTML = "✔ Una letra minúscula";
                        minus.classList.add("regla-valida");
                        minus.classList.remove("regla-invalida");
                        minus.style.color = "green";
                    } else {
                        minus.innerHTML = "○ Una letra minúscula";
                        minus.classList.add("regla-invalida");
                        minus.classList.remove("regla-valida");
                        minus.style.color = "#64748b";
                    }
                }

                if (numero) {
                    if (/[0-9]/.test(valor)) {
                        numero.innerHTML = "✔ Un número";
                        numero.classList.add("regla-valida");
                        numero.classList.remove("regla-invalida");
                        numero.style.color = "green";
                    } else {
                        numero.innerHTML = "○ Un número";
                        numero.classList.add("regla-invalida");
                        numero.classList.remove("regla-valida");
                        numero.style.color = "#64748b";
                    }
                }

                if (simbolo) {
                    if (/[^a-zA-Z0-9\s]/.test(valor)) {
                        simbolo.innerHTML = "✔ Un símbolo especial (!@#$%&*...)";
                        simbolo.classList.add("regla-valida");
                        simbolo.classList.remove("regla-invalida");
                        simbolo.style.color = "green";
                    } else {
                        simbolo.innerHTML = "○ Un símbolo especial (!@#$%&*...)";
                        simbolo.classList.add("regla-invalida");
                        simbolo.classList.remove("regla-valida");
                        simbolo.style.color = "#64748b";
                    }
                }

                if (longitud) {
                    if (valor.length >= 12) {
                        longitud.innerHTML = "✔ Mínimo 12 caracteres";
                        longitud.classList.add("regla-valida");
                        longitud.classList.remove("regla-invalida");
                        longitud.style.color = "green";
                    } else {
                        longitud.innerHTML = "○ Mínimo 12 caracteres";
                        longitud.classList.add("regla-invalida");
                        longitud.classList.remove("regla-valida");
                        longitud.style.color = "#64748b";
                    }
                }
            });
        }
    }

    // Exponer funciones en window para compatibilidad
    window.mostrarMensaje = mostrarMensaje;
    window.iniciarContadorRecuperacion = iniciarContadorRecuperacion;
    window.enviarRecuperacion = enviarRecuperacion;
    window.verificarCodigoRecuperacion = verificarCodigoRecuperacion;
    window.reenviarCodigoRecuperacion = reenviarCodigoRecuperacion;
    window.usarOtraCuenta = usarOtraCuenta;
    window.cambiarContrasena = cambiarContrasena;
    window.cerrarModalRecuperacion = cerrarModalRecuperacion;

    window.addEventListener("pageshow", function (event) {
        if (event.persisted) {
            window.location.reload();
        }
    });

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", inicializarOlvideContrasena);
    } else {
        inicializarOlvideContrasena();
    }
})();
