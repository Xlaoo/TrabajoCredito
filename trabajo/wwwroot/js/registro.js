// ====================================================================
// CREDIPLUS - REGISTRO DE USUARIOS
// Script modular externo sin dependencias inline para CSP estricta
// ====================================================================

(function () {
    "use strict";

    // ==========================================
    // ESTADO DE VERIFICACIÓN DE CORREO (OTP 60s)
    // ==========================================
    let correoVerificado = false;
    let correoVerificadoTexto = "";
    let intervaloOtpTimer = null;
    let segundosOtpTimer = 60;
    let enviandoOtp = false;
    let enviandoRegistro = false;

    const regexDominiosPermitidos = /^[A-Za-z0-9._%+-]+@(gmail\.com|hotmail\.com|hotmail\.es|outlook\.com|outlook\.es|live\.com)$/i;

    function obtenerCodigoOtpIngresado() {
        let codigo = "";
        for (let i = 0; i < 6; i++) {
            const box = document.getElementById(`otp-${i}`);
            codigo += box ? box.value.trim() : "";
        }
        return codigo;
    }

    function limpiarCasillasOtp() {
        for (let i = 0; i < 6; i++) {
            const box = document.getElementById(`otp-${i}`);
            if (box) {
                box.value = "";
                box.disabled = false;
            }
        }
        const msg = document.getElementById("otpMensajeModal");
        if (msg) {
            msg.style.display = "none";
            msg.innerText = "";
        }
    }

    function configurarInputsOtp() {
        for (let i = 0; i < 6; i++) {
            const box = document.getElementById(`otp-${i}`);
            if (!box || box.dataset.otpBound) continue;
            box.dataset.otpBound = "true";

            box.addEventListener("input", function () {
                this.value = this.value.replace(/[^0-9]/g, "");
                if (this.value.length >= 1) {
                    this.value = this.value.charAt(0);
                    if (i < 5) {
                        const next = document.getElementById(`otp-${i + 1}`);
                        if (next) next.focus();
                    } else {
                        confirmarOtpRegistro();
                    }
                }
            });

            box.addEventListener("keydown", function (e) {
                if (e.key === "Backspace" && this.value === "" && i > 0) {
                    const prev = document.getElementById(`otp-${i - 1}`);
                    if (prev) {
                        prev.focus();
                        prev.value = "";
                    }
                } else if (e.key === "Enter") {
                    confirmarOtpRegistro();
                }
            });

            box.addEventListener("paste", function (e) {
                e.preventDefault();
                const pegado = (e.clipboardData || window.clipboardData).getData("text").replace(/[^0-9]/g, "");
                if (pegado.length >= 6) {
                    for (let j = 0; j < 6; j++) {
                        const b = document.getElementById(`otp-${j}`);
                        if (b) b.value = pegado.charAt(j);
                    }
                    const ultimo = document.getElementById("otp-5");
                    if (ultimo) ultimo.focus();
                    confirmarOtpRegistro();
                }
            });
        }
    }

    async function iniciarVerificacionCorreo() {
        const inputCorreo = document.getElementById("correoRegistro");
        if (!inputCorreo) return;
        const correo = inputCorreo.value.trim().toLowerCase();

        if (correo === "") {
            mostrarMensajeRegistro("Primero escribe tu correo electrónico.", true);
            inputCorreo.focus();
            return;
        }

        if (!regexDominiosPermitidos.test(correo)) {
            mostrarMensajeRegistro("Ingresa un correo válido (Gmail, Hotmail o Outlook).", true);
            inputCorreo.focus();
            return;
        }

        if (enviandoOtp) return;
        enviandoOtp = true;

        const btn = document.getElementById("btnVerificarCorreo");
        if (btn) {
            btn.disabled = true;
            btn.innerText = "Enviando...";
        }

        try {
            const respuesta = await fetch("/Login/EnviarCodigo", {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded"
                },
                body: "correo=" + encodeURIComponent(correo)
            });

            const data = await respuesta.json();
            enviandoOtp = false;

            if (!data.ok) {
                if (btn) {
                    btn.disabled = false;
                    btn.innerText = "✉️ VERIFICAR CORREO";
                }
                mostrarMensajeRegistro(data.mensaje || "No se pudo enviar el código.", true);
                return;
            }

            abrirModalOtp(correo);
        } catch (err) {
            enviandoOtp = false;
            if (btn) {
                btn.disabled = false;
                btn.innerText = "✉️ VERIFICAR CORREO";
            }
            mostrarMensajeRegistro("Error de conexión al enviar el código de verificación.", true);
        }
    }

    function abrirModalOtp(correo) {
        const destino = document.getElementById("modalCorreoDestino");
        if (destino) destino.innerText = correo;

        limpiarCasillasOtp();
        iniciarCuentaRegresivaOtp();

        const modal = document.getElementById("modalOtpCorreo");
        if (modal) modal.style.display = "flex";

        setTimeout(() => {
            const primero = document.getElementById("otp-0");
            if (primero) primero.focus();
        }, 150);
    }

    function cerrarModalOtp() {
        detenerCuentaRegresivaOtp();
        const modal = document.getElementById("modalOtpCorreo");
        if (modal) modal.style.display = "none";

        const btn = document.getElementById("btnVerificarCorreo");
        if (!correoVerificado && btn) {
            btn.disabled = false;
            btn.innerText = "✉️ VERIFICAR CORREO";
        }
    }

    function iniciarCuentaRegresivaOtp() {
        detenerCuentaRegresivaOtp();
        segundosOtpTimer = 60;

        const timerDisplay = document.getElementById("otpTimerDisplay");
        const contadorSpan = document.getElementById("otpTimerContador");
        const btnReenviar = document.getElementById("btnReenviarOtp");
        const btnConfirmar = document.getElementById("btnConfirmarOtp");

        if (timerDisplay) timerDisplay.className = "otp-timer activo";
        if (contadorSpan) contadorSpan.innerText = "01:00";
        if (btnReenviar) btnReenviar.disabled = true;
        if (btnConfirmar) btnConfirmar.disabled = false;

        for (let i = 0; i < 6; i++) {
            const box = document.getElementById(`otp-${i}`);
            if (box) box.disabled = false;
        }

        intervaloOtpTimer = setInterval(function () {
            segundosOtpTimer--;

            if (segundosOtpTimer > 0) {
                const segStr = segundosOtpTimer < 10 ? "0" + segundosOtpTimer : segundosOtpTimer;
                if (contadorSpan) contadorSpan.innerText = "00:" + segStr;
            } else {
                detenerCuentaRegresivaOtp();
                if (timerDisplay) {
                    timerDisplay.className = "otp-timer expirado";
                    timerDisplay.innerText = "⏰ El código ha expirado (límite 60s).";
                }
                if (btnReenviar) btnReenviar.disabled = false;
                if (btnConfirmar) btnConfirmar.disabled = true;

                for (let i = 0; i < 6; i++) {
                    const box = document.getElementById(`otp-${i}`);
                    if (box) box.disabled = true;
                }
            }
        }, 1000);
    }

    function detenerCuentaRegresivaOtp() {
        if (intervaloOtpTimer) {
            clearInterval(intervaloOtpTimer);
            intervaloOtpTimer = null;
        }
    }

    async function reenviarOtpRegistro() {
        const inputCorreo = document.getElementById("correoRegistro");
        if (!inputCorreo) return;
        const correo = inputCorreo.value.trim().toLowerCase();

        const btnReenviar = document.getElementById("btnReenviarOtp");
        if (btnReenviar) {
            btnReenviar.disabled = true;
            btnReenviar.innerText = "Enviando...";
        }

        try {
            const respuesta = await fetch("/Login/EnviarCodigo", {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded"
                },
                body: "correo=" + encodeURIComponent(correo)
            });

            const data = await respuesta.json();
            if (btnReenviar) {
                btnReenviar.innerText = "🔄 Reenviar nuevo código";
            }

            if (!data.ok) {
                mostrarMensajeModalOtp(data.mensaje || "No se pudo reenviar el código.", true);
                if (btnReenviar) btnReenviar.disabled = false;
                return;
            }

            limpiarCasillasOtp();
            iniciarCuentaRegresivaOtp();
            mostrarMensajeModalOtp("Nuevo código de 6 dígitos enviado.", false);

            const primero = document.getElementById("otp-0");
            if (primero) primero.focus();
        } catch (err) {
            if (btnReenviar) {
                btnReenviar.disabled = false;
                btnReenviar.innerText = "🔄 Reenviar nuevo código";
            }
            mostrarMensajeModalOtp("Error de conexión al reenviar.", true);
        }
    }

    function mostrarMensajeModalOtp(mensaje, esError) {
        const msg = document.getElementById("otpMensajeModal");
        if (!msg) return;

        msg.innerText = mensaje;
        msg.style.display = "block";
        msg.style.backgroundColor = esError ? "#fee2e2" : "#dcfce7";
        msg.style.color = esError ? "#991b1b" : "#166534";
        msg.style.border = esError ? "1px solid #fca5a5" : "1px solid #86efac";
    }

    async function confirmarOtpRegistro() {
        const inputCorreo = document.getElementById("correoRegistro");
        if (!inputCorreo) return;
        const correo = inputCorreo.value.trim().toLowerCase();
        const codigo = obtenerCodigoOtpIngresado();

        if (codigo.length !== 6) {
            mostrarMensajeModalOtp("Ingresa los 6 dígitos del código.", true);
            return;
        }

        const btnConfirmar = document.getElementById("btnConfirmarOtp");
        if (btnConfirmar) {
            btnConfirmar.disabled = true;
            btnConfirmar.innerText = "Verificando...";
        }

        try {
            const respuesta = await fetch("/Login/ConfirmarCodigoRegistro", {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded"
                },
                body: `correo=${encodeURIComponent(correo)}&codigo=${encodeURIComponent(codigo)}`
            });

            const data = await respuesta.json();
            if (btnConfirmar) {
                btnConfirmar.disabled = false;
                btnConfirmar.innerText = "Verificar";
            }

            if (!data.ok) {
                mostrarMensajeModalOtp(data.mensaje || "Código incorrecto.", true);
                if (data.expirado) {
                    detenerCuentaRegresivaOtp();
                    const btnRe = document.getElementById("btnReenviarOtp");
                    if (btnRe) btnRe.disabled = false;
                    if (btnConfirmar) btnConfirmar.disabled = true;
                }
                return;
            }

            // Verificación exitosa
            correoVerificado = true;
            correoVerificadoTexto = correo;
            const codReg = document.getElementById("codigoRegistro");
            if (codReg) codReg.value = codigo;

            detenerCuentaRegresivaOtp();
            cerrarModalOtp();

            // Actualizar interfaz del formulario
            const badge = document.getElementById("badgeCorreoVerificado");
            if (badge) badge.style.display = "inline-flex";

            const btnVerificar = document.getElementById("btnVerificarCorreo");
            if (btnVerificar) {
                btnVerificar.innerText = "✅ VERIFICADO";
                btnVerificar.disabled = true;
            }

            mostrarMensajeRegistro("¡Correo electrónico verificado exitosamente!", false);
        } catch (err) {
            if (btnConfirmar) {
                btnConfirmar.disabled = false;
                btnConfirmar.innerText = "Verificar";
            }
            mostrarMensajeModalOtp("Error de conexión al verificar el código.", true);
        }
    }

    function mostrarMensajeRegistro(mensaje, esError = true) {
        const modal = document.getElementById("modalMensajeRegistro");
        const texto = document.getElementById("textoMensajeRegistro");
        const icono = document.getElementById("iconoMensajeRegistro");
        const titulo = document.getElementById("tituloMensajeRegistro");

        if (!modal) return;

        if (texto) texto.innerText = mensaje;

        if (esError) {
            if (icono) icono.innerText = "⚠️";
            if (titulo) titulo.innerText = "Revisa tus datos";
        } else {
            if (icono) icono.innerText = "✅";
            if (titulo) titulo.innerText = "CrediPlus";
        }

        modal.style.display = "flex";
    }

    function cerrarMensajeRegistro() {
        const modal = document.getElementById("modalMensajeRegistro");
        if (modal) modal.style.display = "none";
    }

    function inicializarRegistro() {
        configurarInputsOtp();

        // Validación en tiempo real del correo para invalidar OTP si se modifica
        const inputCorreoReg = document.getElementById("correoRegistro");
        if (inputCorreoReg && !inputCorreoReg.dataset.regBound) {
            inputCorreoReg.dataset.regBound = "true";
            inputCorreoReg.addEventListener("input", function () {
                const correoActual = this.value.trim().toLowerCase();
                if (correoVerificado && correoActual !== correoVerificadoTexto) {
                    correoVerificado = false;
                    correoVerificadoTexto = "";
                    const badge = document.getElementById("badgeCorreoVerificado");
                    if (badge) badge.style.display = "none";
                    const cod = document.getElementById("codigoRegistro");
                    if (cod) cod.value = "";
                    const btn = document.getElementById("btnVerificarCorreo");
                    if (btn) {
                        btn.disabled = false;
                        btn.innerText = "✉️ VERIFICAR CORREO";
                    }
                }
            });
        }

        // Restricción numérica para DNI y celular
        const dniReg = document.getElementById("dniRegistro");
        if (dniReg && !dniReg.dataset.numBound) {
            dniReg.dataset.numBound = "true";
            dniReg.addEventListener("input", function () {
                this.value = this.value.replace(/[^0-9]/g, "");
            });
        }

        const celReg = document.getElementById("celularRegistro");
        if (celReg && !celReg.dataset.numBound) {
            celReg.dataset.numBound = "true";
            celReg.addEventListener("input", function () {
                this.value = this.value.replace(/[^0-9]/g, "");
            });
        }

        // Botón verificar correo
        const btnVerificarCorreo = document.getElementById("btnVerificarCorreo");
        if (btnVerificarCorreo && !btnVerificarCorreo.dataset.regBound) {
            btnVerificarCorreo.dataset.regBound = "true";
            btnVerificarCorreo.addEventListener("click", function (e) {
                e.preventDefault();
                iniciarVerificacionCorreo();
            });
        }

        // Botones del modal OTP
        const btnConfirmarOtp = document.getElementById("btnConfirmarOtp");
        if (btnConfirmarOtp && !btnConfirmarOtp.dataset.regBound) {
            btnConfirmarOtp.dataset.regBound = "true";
            btnConfirmarOtp.addEventListener("click", function (e) {
                e.preventDefault();
                confirmarOtpRegistro();
            });
        }

        const btnReenviarOtp = document.getElementById("btnReenviarOtp");
        if (btnReenviarOtp && !btnReenviarOtp.dataset.regBound) {
            btnReenviarOtp.dataset.regBound = "true";
            btnReenviarOtp.addEventListener("click", function (e) {
                e.preventDefault();
                reenviarOtpRegistro();
            });
        }

        const btnCancelarModalOtp = document.getElementById("btnCancelarModalOtp");
        if (btnCancelarModalOtp && !btnCancelarModalOtp.dataset.regBound) {
            btnCancelarModalOtp.dataset.regBound = "true";
            btnCancelarModalOtp.addEventListener("click", function (e) {
                e.preventDefault();
                cerrarModalOtp();
            });
        }

        // Botón cerrar mensaje registro
        const btnCerrarMensajeRegistro = document.getElementById("btnCerrarMensajeRegistro");
        if (btnCerrarMensajeRegistro && !btnCerrarMensajeRegistro.dataset.regBound) {
            btnCerrarMensajeRegistro.dataset.regBound = "true";
            btnCerrarMensajeRegistro.addEventListener("click", function (e) {
                e.preventDefault();
                cerrarMensajeRegistro();
            });
        }

        // Criterios de fortaleza de contraseña en tiempo real
        const clave = document.getElementById("clave");
        const mayus = document.getElementById("mayus");
        const minus = document.getElementById("minus");
        const numero = document.getElementById("numero");
        const simbolo = document.getElementById("simbolo");
        const minimo = document.getElementById("minimo");

        if (clave && !clave.dataset.rulesBound) {
            clave.dataset.rulesBound = "true";
            clave.addEventListener("input", function () {
                const valor = clave.value;

                if (mayus) {
                    if (/[A-Z]/.test(valor)) {
                        mayus.innerHTML = "✔ Una letra mayúscula";
                        mayus.style.color = "green";
                    } else {
                        mayus.innerHTML = "❌ Una letra mayúscula";
                        mayus.style.color = "#64748b";
                    }
                }

                if (minus) {
                    if (/[a-z]/.test(valor)) {
                        minus.innerHTML = "✔ Una letra minúscula";
                        minus.style.color = "green";
                    } else {
                        minus.innerHTML = "❌ Una letra minúscula";
                        minus.style.color = "#64748b";
                    }
                }

                if (numero) {
                    if (/[0-9]/.test(valor)) {
                        numero.innerHTML = "✔ Un número";
                        numero.style.color = "green";
                    } else {
                        numero.innerHTML = "❌ Un número";
                        numero.style.color = "#64748b";
                    }
                }

                if (simbolo) {
                    if (/[^a-zA-Z0-9\s]/.test(valor)) {
                        simbolo.innerHTML = "✔ Un símbolo especial (!@#$%&*...)";
                        simbolo.style.color = "green";
                    } else {
                        simbolo.innerHTML = "❌ Un símbolo especial (!@#$%&*...)";
                        simbolo.style.color = "#64748b";
                    }
                }

                if (minimo) {
                    if (valor.length >= 12) {
                        minimo.innerHTML = "✔ Mínimo 12 caracteres";
                        minimo.style.color = "green";
                    } else {
                        minimo.innerHTML = "❌ Mínimo 12 caracteres";
                        minimo.style.color = "#64748b";
                    }
                }
            });
        }

        // Envío seguro del formulario con protección anti-doble clic
        const formRegistro = document.getElementById("formRegistro");
        if (formRegistro && !formRegistro.dataset.regBound) {
            formRegistro.dataset.regBound = "true";

            formRegistro.addEventListener("submit", async function (event) {
                event.preventDefault();

                if (enviandoRegistro) return;
                enviandoRegistro = true;

                const formulario = this;
                const botonRegistro = formulario.querySelector('button[type="submit"]');

                if (botonRegistro) {
                    botonRegistro.disabled = true;
                    botonRegistro.innerText = "Registrando...";
                }

                if (!correoVerificado) {
                    mostrarMensajeRegistro("Debes verificar tu correo electrónico antes de registrarte.", true);
                    enviandoRegistro = false;

                    if (botonRegistro) {
                        botonRegistro.disabled = false;
                        botonRegistro.innerText = "Registrate";
                    }

                    const btnVerificar = document.getElementById("btnVerificarCorreo");
                    if (btnVerificar) btnVerificar.focus();
                    return;
                }

                const turnstileInput = formulario.querySelector('[name="cf-turnstile-response"]');
                const turnstileToken = turnstileInput ? turnstileInput.value : "";
                const turnstileWidget = formulario.querySelector('.cf-turnstile');
                if (turnstileWidget && !turnstileToken) {
                    mostrarMensajeRegistro("Verificación de seguridad pendiente. Por favor espera un momento...", true);
                    enviandoRegistro = false;
                    if (botonRegistro) {
                        botonRegistro.disabled = false;
                        botonRegistro.innerText = "Registrate";
                    }
                    return;
                }

                const formData = new FormData(formulario);
                formData.delete("audio");

                try {
                    const response = await fetch(formulario.action, {
                        method: "POST",
                        body: formData
                    });

                    const contentType = response.headers.get("content-type") || "";

                    if (contentType.includes("application/json")) {
                        const resultado = await response.json();

                        if (!resultado.ok) {
                            mostrarMensajeRegistro(resultado.mensaje || "No se pudo registrar el usuario.");

                            if (resultado.campo) {
                                const campo = document.getElementById(resultado.campo);
                                if (campo) campo.focus();
                            }

                            enviandoRegistro = false;

                            if (typeof turnstile !== "undefined") {
                                try { turnstile.reset(); } catch (e) {}
                            }

                            if (botonRegistro) {
                                botonRegistro.disabled = false;
                                botonRegistro.innerText = "Registrate";
                            }
                            return;
                        }

                        const estado = document.getElementById("estadoVoz");
                        if (estado) {
                            estado.innerText = "✅ Usuario registrado exitosamente.";
                            estado.style.color = "#16a34a";
                        }

                        if (botonRegistro) {
                            botonRegistro.innerText = "✅ Registrado correctamente";
                            botonRegistro.disabled = true;
                        }

                        setTimeout(function () {
                            window.location.href = resultado.redirectUrl;
                        }, 1200);
                        return;
                    }
                } catch (error) {
                    console.error("Error al registrar:", error);
                    alert("Ocurrió un error al registrar el usuario.");
                    enviandoRegistro = false;

                    if (botonRegistro) {
                        botonRegistro.disabled = false;
                        botonRegistro.innerText = "Registrate";
                    }
                }
            });
        }
    }

    // Exponer funciones en window para compatibilidad
    window.iniciarVerificacionCorreo = iniciarVerificacionCorreo;
    window.abrirModalOtp = abrirModalOtp;
    window.cerrarModalOtp = cerrarModalOtp;
    window.confirmarOtpRegistro = confirmarOtpRegistro;
    window.reenviarOtpRegistro = reenviarOtpRegistro;
    window.mostrarMensajeRegistro = mostrarMensajeRegistro;
    window.cerrarMensajeRegistro = cerrarMensajeRegistro;

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", inicializarRegistro);
    } else {
        inicializarRegistro();
    }
})();
