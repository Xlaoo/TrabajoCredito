
            // Método seleccionado por defecto
            let metodoVerificacion = "correo";
            let intervaloCodigo = null;
            let fraseVozBD = "";
            let segundosCodigo = 60;
        let solicitudAuthenticatorActual = null;

        let intervaloSolicitudAuthenticator = null;

        let solicitudAuthenticatorTerminada = false;
            // NUEVO
            let vozHabilitadaLogin = false;
            // NUEVO
            let tieneRegistroVozLogin = false;
        let correoAuthenticatorOculto = "";

        // RENOVACIÓN DE BIOMETRÍA DE VOZ Y PALABRA SECRETA
        let modoRenovacionVoz = false;
        let tokenRenovacionVozActivo = null;
        let intervaloOtpRenovacion = null;
        let segundosOtpRenovacion = 60;
                // Temporizador de vinculación CrediPlus Authenticator
        let intervaloAuthenticator = null;
        let segundosAuthenticator = 60;
        let correoUsuarioOcultoRenovacion = "";
            // ==========================================
            // CONTADOR DEL CÓDIGO
            // ==========================================

            function iniciarContadorCodigo() {

                // Detener contador anterior
                if (intervaloCodigo) {
                    clearInterval(intervaloCodigo);
                }

                segundosCodigo = 60;

                const tiempo =
                    document.getElementById("tiempoCodigo");

                const texto =
                    document.getElementById("textoContador");

                const botonReenviar =
                    document.getElementById("btnReenviarCodigo");

                if (!tiempo || !texto || !botonReenviar) {
                    return;
                }

                // Reiniciar interfaz

                botonReenviar.disabled = true;

                tiempo.classList.remove("contador-expirado");

                texto.innerText =
                    "El código expira en";

                actualizarTiempoCodigo();


                intervaloCodigo = setInterval(function() {

                    segundosCodigo--;

                    actualizarTiempoCodigo();


                    // ======================================
                    // CÓDIGO EXPIRADO
                    // ======================================

                    if (segundosCodigo <= 0) {

                        clearInterval(intervaloCodigo);

                        intervaloCodigo = null;

                        segundosCodigo = 0;

                        tiempo.innerText = "00:00";

                        tiempo.classList.add(
                            "contador-expirado"
                        );

                        texto.innerText =
                            "El código ha expirado";

                        // Habilitar reenviar

                        botonReenviar.disabled = false;

                    }

                }, 1000);
            }
            function actualizarTiempoCodigo() {

                const tiempo =
                    document.getElementById("tiempoCodigo");

                if (!tiempo) {
                    return;
                }

                const minutos =
                    Math.floor(segundosCodigo / 60);

                const segundos =
                    segundosCodigo % 60;

                tiempo.innerText =
                    String(minutos).padStart(2, "0") +
                    ":" +
                    String(segundos).padStart(2, "0");
            }
            // ==========================================
            // SELECCIONAR MÉTODO
            // ==========================================

            function seleccionarMetodo(metodo) {
                if (typeof liberarRecursosVoz === "function" && metodo !== "voz") {
                    liberarRecursosVoz();
                }

                const opcionCorreo =
                    document.getElementById("opcionCorreo");

                const opcionAplicacion =
                    document.getElementById("opcionAplicacion");

                const opcionVoz =
                    document.getElementById("opcionVoz");

                const radioCorreo =
                    document.getElementById("radioCorreo");

                const radioAplicacion =
                    document.getElementById("radioAplicacion");

                const radioVoz =
                    document.getElementById("radioVoz");


                const bloqueRegistroVoz =
                    document.getElementById("bloqueRegistroVoz");

                const bloqueVerificacionVoz =
                    document.getElementById("bloqueVerificacionVoz");


                // CORREO
                if (metodo === "correo") {

                    metodoVerificacion = "correo";

                    opcionCorreo.classList.add("seleccionada");
                    opcionAplicacion.classList.remove("seleccionada");
                    opcionVoz.classList.remove("seleccionada");

                    radioCorreo.style.display = "block";
                    radioAplicacion.style.display = "none";
                    radioVoz.style.display = "none";


                    bloqueRegistroVoz.style.display = "none";
                    bloqueVerificacionVoz.style.display = "none";

                    const contenedorCambiar = document.getElementById("contenedorCambiarBiometria");
                    if (contenedorCambiar) contenedorCambiar.style.display = "none";
                }


                // APLICACIÓN
                if (metodo === "aplicacion") {

                    metodoVerificacion = "aplicacion";

                    opcionCorreo.classList.remove("seleccionada");
                    opcionAplicacion.classList.add("seleccionada");
                    opcionVoz.classList.remove("seleccionada");

                    radioCorreo.style.display = "none";
                    radioAplicacion.style.display = "block";
                    radioVoz.style.display = "none";


                    bloqueRegistroVoz.style.display = "none";
                    bloqueVerificacionVoz.style.display = "none";

                    const contenedorCambiar = document.getElementById("contenedorCambiarBiometria");
                    if (contenedorCambiar) contenedorCambiar.style.display = "none";
                }


                if (metodo === "voz") {

                    // Mostrar u ocultar enlace de renovar biometría únicamente si tiene biometría previa
                    const contenedorCambiar = document.getElementById("contenedorCambiarBiometria");
                    if (contenedorCambiar) {
                        if (tieneRegistroVozLogin) {
                            contenedorCambiar.style.display = "block";
                        } else {
                            contenedorCambiar.style.display = "none";
                        }
                    }

                    // ==========================================
                    // TIENE VOZ REGISTRADA PERO TODAVÍA
                    // NO ESTÁ AUTORIZADA PARA LOGIN POR VOZ
                    // ==========================================

                    if (
                        tieneRegistroVozLogin &&
                        !vozHabilitadaLogin
                    ) {

                        const modalElemento =
                            document.getElementById(
                                "modalVozNoHabilitada"
                            );

                        const modal =
                            bootstrap.Modal
                                .getOrCreateInstance(
                                    modalElemento
                                );

                        modal.show();

                        return;
                    }


                    // ==========================================
                    // PERMITIR SELECCIONAR VOZ
                    // ==========================================

                    metodoVerificacion = "voz";

                    opcionCorreo.classList.remove(
                        "seleccionada"
                    );

                    opcionAplicacion.classList.remove(
                        "seleccionada"
                    );

                    opcionVoz.classList.add(
                        "seleccionada"
                    );

                    radioCorreo.style.display =
                        "none";

                    radioAplicacion.style.display =
                        "none";

                    radioVoz.style.display =
                        "block";


                    bloqueRegistroVoz.style.display =
                        "none";

                    bloqueVerificacionVoz.style.display =
                        "none";
                }
            }
                    function abrirFiltroAuthenticator(correoOculto) {

            correoAuthenticatorOculto =
                correoOculto || "Correo registrado";

            document.getElementById(
                "correoOcultoAuthenticator"
            ).innerText =
                correoAuthenticatorOculto;

            document.getElementById(
                "correoOcultoAuthenticator2"
            ).innerText =
                correoAuthenticatorOculto;


            // Reiniciar pasos
            document.getElementById(
                "authPaso1"
            ).style.display = "block";

            document.getElementById(
                "authPaso2"
            ).style.display = "none";


            document.getElementById(
                "btnEnviarCodigoAuthenticator"
            ).style.display = "inline-block";

            document.getElementById(
                "btnVerificarAuthenticator"
            ).style.display = "none";


            document.getElementById(
                "authPasoIndicador1"
            ).classList.add("activo");

            document.getElementById(
                "authPasoIndicador2"
            ).classList.remove("activo");


            document.getElementById(
                "codigoAuthenticatorCorreo"
            ).value = "";


            // Cerrar modal anterior
            const modalSeguridad =
                bootstrap.Modal.getInstance(
                    document.getElementById(
                        "modalVerificacionSeguridad"
                    )
                );

            if (modalSeguridad) {
                modalSeguridad.hide();
            }


            setTimeout(function () {

                const modal =
                    bootstrap.Modal.getOrCreateInstance(
                        document.getElementById(
                            "modalFiltroAuthenticator"
                        )
                    );

                modal.show();

            }, 250);
        }


        async function enviarCodigoAuthenticator() {

            const boton =
                document.getElementById(
                    "btnEnviarCodigoAuthenticator"
                );

            if (boton) {
                boton.disabled = true;

                boton.innerHTML =
                    '<span class="spinner-border spinner-border-sm me-1"></span> Enviando...';
            }

            try {

                const respuesta =
                    await fetch(
                        "/Login/EnviarCodigoVinculacionAuthenticator",
                        {
                            method: "POST"
                        }
                    );

                const resultado =
                    await respuesta.json();


                if (!resultado.ok) {

                    mostrarMensajeAuthenticator(
                        "mensajeAuthenticatorPaso1",
                        resultado.mensaje,
                        false
                    );

                    return;
                }


                correoAuthenticatorOculto =
                    resultado.correoOculto;


                document.getElementById(
                    "correoOcultoAuthenticator2"
                ).innerText =
                    correoAuthenticatorOculto;


                // Pasar a PASO 2
                document.getElementById(
                    "authPaso1"
                ).style.display = "none";

                document.getElementById(
                    "authPaso2"
                ).style.display = "block";


                document.getElementById(
                    "btnEnviarCodigoAuthenticator"
                ).style.display = "none";

                document.getElementById(
                    "btnVerificarAuthenticator"
                ).style.display = "inline-block";


                document.getElementById(
                    "authPasoIndicador2"
                ).classList.add("activo");


                iniciarContadorAuthenticator();


                setTimeout(function () {
                    document.getElementById(
                        "codigoAuthenticatorCorreo"
                    ).focus();
                }, 200);

            }
            catch (error) {

                console.error(error);

                mostrarMensajeAuthenticator(
                    "mensajeAuthenticatorPaso1",
                    "No se pudo enviar el código.",
                    false
                );
            }
            finally {

                if (boton) {

                    boton.disabled = false;

                    boton.innerHTML =
                        '<i class="bi bi-send-fill me-1"></i> Enviar código';
                }
            }
        }


        async function verificarCodigoAuthenticator() {

            const input =
                document.getElementById(
                    "codigoAuthenticatorCorreo"
                );

            const codigo =
                input.value
                    .trim()
                    .toUpperCase();


            if (codigo.length !== 6) {

                mostrarMensajeAuthenticator(
                    "mensajeAuthenticatorPaso2",
                    "Ingresa los 6 caracteres del código.",
                    false
                );

                return;
            }


            const boton =
                document.getElementById(
                    "btnVerificarAuthenticator"
                );

            boton.disabled = true;

            boton.innerHTML =
                '<span class="spinner-border spinner-border-sm me-1"></span> Verificando...';


            try {

                const datos =
                    new FormData();

                datos.append(
                    "codigo",
                    codigo
                );


                const respuesta =
                    await fetch(
                        "/Login/VerificarCodigoVinculacionAuthenticator",
                        {
                            method: "POST",
                            body: datos
                        }
                    );


                if (!respuesta.ok) {
                    throw new Error("Error HTTP " + respuesta.status);
                }


                const resultado =
                    await respuesta.json();


                if (!resultado.ok) {

                    mostrarMensajeAuthenticator(
                        "mensajeAuthenticatorPaso2",
                        resultado.mensaje || "El código ingresado no es válido.",
                        false
                    );

                    return;
                }


        if (intervaloAuthenticator) {
            clearInterval(intervaloAuthenticator);
            intervaloAuthenticator = null;
        }

        const mensaje = document.getElementById("mensajeAuthenticatorPaso2");
        if (mensaje) {
            mensaje.textContent = "Identidad confirmada correctamente.";
            mensaje.style.display = "block";
            mensaje.classList.remove("alert-danger");
            mensaje.classList.add("alert-success");
        }

        const modalActualEl =
            document.getElementById("modalFiltroAuthenticator");

        const modalExitoEl =
            document.getElementById("modalIdentidadConfirmadaAuthenticator");

        if (!modalExitoEl) {
            console.error("Falta modalIdentidadConfirmadaAuthenticator");
            return;
        }

        if (modalActualEl) {
            const modalActual =
                bootstrap.Modal.getOrCreateInstance(modalActualEl);

            modalActualEl.addEventListener("hidden.bs.modal", function () {
                bootstrap.Modal.getOrCreateInstance(modalExitoEl).show();
            }, { once: true });

            modalActual.hide();
        } else {
            bootstrap.Modal.getOrCreateInstance(modalExitoEl).show();
        }

            }catch (error) {

                console.error("Error al verificar código Authenticator:", error);

                mostrarMensajeAuthenticator(
                    "mensajeAuthenticatorPaso2",
                    "No se pudo verificar el código. Inténtalo nuevamente.",
                    false
                );
            }
            finally {

                if (boton) {
                    boton.disabled = false;

                    boton.innerHTML =
                        '<i class="bi bi-shield-check me-1"></i> Verificar identidad';
                }
            }
        }


               function iniciarContadorAuthenticator() {
            if (intervaloAuthenticator) {
                clearInterval(intervaloAuthenticator);
                intervaloAuthenticator = null;
            }

            const tiempo = document.getElementById("tiempoAuthenticator");
            const reenviar = document.getElementById("btnReenviarAuthenticator");

            const vencimiento = Date.now() + 60000;

            if (reenviar) {
                reenviar.disabled = true;
                reenviar.style.pointerEvents = "none";
                reenviar.style.opacity = "0.5";
            }

            function actualizar() {
                const restantes = Math.max(
                    0,
                    Math.ceil((vencimiento - Date.now()) / 1000)
                );

                segundosAuthenticator = restantes;

                if (tiempo) {
                    tiempo.textContent =
                        String(Math.floor(restantes / 60)).padStart(2, "0") +
                        ":" +
                        String(restantes % 60).padStart(2, "0");
                }

                if (restantes === 0) {
                    clearInterval(intervaloAuthenticator);
                    intervaloAuthenticator = null;

                    if (reenviar) {
                        reenviar.disabled = false;
                        reenviar.style.pointerEvents = "";
                        reenviar.style.opacity = "";

                    }
                }
            }

            actualizar();
            intervaloAuthenticator = setInterval(actualizar, 250);
        }


        function mostrarMensajeAuthenticator(
            id,
            mensaje,
            correcto
        ) {

            const elemento =
                document.getElementById(id);

            elemento.style.display = "block";

            elemento.className =
                correcto
                    ? "alert alert-success mt-3"
                    : "alert alert-danger mt-3";

            elemento.innerText =
                mensaje;
        }

        // ==========================================
        // RENOVACIÓN DE BIOMETRÍA DE VOZ Y PALABRA SECRETA
        // ==========================================

        function enmascararCorreoCliente(correo) {
            const arr = String.fromCharCode(64);
            if (!correo || !correo.includes(arr)) return "Correo registrado";
            try {
                const partes = correo.split(arr);
                const u = partes[0];
                const d = partes[1];
                if (u.length <= 2) return u[0] + "***" + arr + d;
                return u.substring(0, 2) + "***" + arr + d;
            } catch (e) {
                return "Correo registrado";
            }
        }

        function abrirModalSolicitarRenovacion() {
            if (!tieneRegistroVozLogin) return;

            // Restablecer vistas al Paso 1
            const paso1 = document.getElementById("renovacionPaso1");
            const paso2 = document.getElementById("renovacionPaso2");
            const msgPaso1 = document.getElementById("mensajeRenovacionPaso1");
            const msgPaso2 = document.getElementById("mensajeRenovacionPaso2");

            if (paso1) paso1.style.display = "block";
            if (paso2) paso2.style.display = "none";
            if (msgPaso1) msgPaso1.style.display = "none";
            if (msgPaso2) msgPaso2.style.display = "none";

            const btnEnviar = document.getElementById("btnEnviarCodigoRenovacion");
            if (btnEnviar) {
                btnEnviar.disabled = false;
                btnEnviar.innerHTML = '<i class="bi bi-send-fill me-1"></i> Enviar código de verificación';
            }

            const correoDisplay = correoUsuarioOcultoRenovacion || "Correo registrado";
            const correoEl = document.getElementById("correoOcultoRenovacion");
            if (correoEl) correoEl.innerText = correoDisplay;

            limpiarCasillasOtp();
            detenerContadorRenovacion();

            // Cerrar modal de verificación de seguridad
            const modalSeguridadEl = document.getElementById("modalVerificacionSeguridad");
            if (modalSeguridadEl) {
                const modalSeg = bootstrap.Modal.getInstance(modalSeguridadEl);
                if (modalSeg) modalSeg.hide();
            }

            setTimeout(function () {
                const modalRenovacionEl = document.getElementById("modalRenovacionBiometria");
                if (modalRenovacionEl) {
                    const modalRen = bootstrap.Modal.getOrCreateInstance(modalRenovacionEl);
                    modalRen.show();
                }
            }, 200);
        }

        function cancelarRenovacionBiometria() {
            detenerContadorRenovacion();
            limpiarCasillasOtp();
            modoRenovacionVoz = false;
            tokenRenovacionVozActivo = null;

            const modalRenovacionEl = document.getElementById("modalRenovacionBiometria");
            if (modalRenovacionEl) {
                const modalRen = bootstrap.Modal.getInstance(modalRenovacionEl);
                if (modalRen) modalRen.hide();
            }

            setTimeout(function () {
                const modalSeguridadEl = document.getElementById("modalVerificacionSeguridad");
                if (modalSeguridadEl) {
                    const modalSeg = bootstrap.Modal.getOrCreateInstance(modalSeguridadEl);
                    modalSeg.show();
                    seleccionarMetodo("voz");
                }
            }, 200);
        }

        async function solicitarOtpRenovacion() {
            const btnEnviar = document.getElementById("btnEnviarCodigoRenovacion");
            const msgPaso1 = document.getElementById("mensajeRenovacionPaso1");
            if (msgPaso1) msgPaso1.style.display = "none";

            if (btnEnviar) {
                btnEnviar.disabled = true;
                btnEnviar.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Enviando código...';
            }

            try {
                const respuesta = await fetch("/Login/SolicitarOtpRenovacionVoz", {
                    method: "POST"
                });
                const resultado = await respuesta.json();

                if (!resultado.ok) {
                    if (msgPaso1) {
                        msgPaso1.innerText = resultado.mensaje || "No se pudo enviar el código.";
                        msgPaso1.style.display = "block";
                    }
                    if (btnEnviar) {
                        btnEnviar.disabled = false;
                        btnEnviar.innerHTML = '<i class="bi bi-send-fill me-1"></i> Enviar código de verificación';
                    }
                    return;
                }

                correoUsuarioOcultoRenovacion = resultado.correo || resultado.correoOculto || "Correo registrado";
                const c1 = document.getElementById("correoOcultoRenovacion");
                const c2 = document.getElementById("correoOcultoRenovacion2");
                if (c1) c1.innerText = correoUsuarioOcultoRenovacion;
                if (c2) c2.innerText = correoUsuarioOcultoRenovacion;

                // Pasar a Pantalla 2
                const paso1 = document.getElementById("renovacionPaso1");
                const paso2 = document.getElementById("renovacionPaso2");
                if (paso1) paso1.style.display = "none";
                if (paso2) paso2.style.display = "block";

                limpiarCasillasOtp();
                iniciarContadorRenovacion(resultado.segundos || resultado.segundosVigencia || 60);

                setTimeout(() => {
                    const primera = document.querySelector(".casilla-otp-renovacion[data-index='0']");
                    if (primera) primera.focus();
                }, 100);

            } catch (err) {
                console.error(err);
                if (msgPaso1) {
                    msgPaso1.innerText = "Error de conexión al enviar el código. Intenta nuevamente.";
                    msgPaso1.style.display = "block";
                }
                if (btnEnviar) {
                    btnEnviar.disabled = false;
                    btnEnviar.innerHTML = '<i class="bi bi-send-fill me-1"></i> Enviar código de verificación';
                }
            }
        }

        function iniciarContadorRenovacion(segundos = 60) {
            detenerContadorRenovacion();
            segundosOtpRenovacion = segundos;

            const tiempoEl = document.getElementById("tiempoOtpRenovacion");
            const textoContador = document.getElementById("textoContadorRenovacion");
            const btnReenviar = document.getElementById("btnReenviarOtpRenovacion");
            const btnVerificar = document.getElementById("btnVerificarOtpRenovacion");
            const msgPaso2 = document.getElementById("mensajeRenovacionPaso2");

            if (btnVerificar) btnVerificar.disabled = false;
            if (btnReenviar) btnReenviar.disabled = true;
            if (msgPaso2) msgPaso2.style.display = "none";

            const actualizarVisual = () => {
                const min = Math.floor(segundosOtpRenovacion / 60);
                const seg = segundosOtpRenovacion % 60;
                const formateado = String(min).padStart(2, "0") + ":" + String(seg).padStart(2, "0");
                if (tiempoEl) tiempoEl.innerText = formateado;
                if (textoContador) textoContador.innerHTML = 'El código vence en <strong style="color:#7c3aed;">' + formateado + '</strong>';
            };

            actualizarVisual();

            intervaloOtpRenovacion = setInterval(function () {
                segundosOtpRenovacion--;
                if (segundosOtpRenovacion > 0) {
                    actualizarVisual();
                } else {
                    detenerContadorRenovacion();
                    if (tiempoEl) tiempoEl.innerText = "00:00";
                    if (textoContador) textoContador.innerHTML = '<strong class="text-danger">El código ha expirado</strong>';
                    if (btnVerificar) btnVerificar.disabled = true;
                    if (btnReenviar) btnReenviar.disabled = false;
                    if (msgPaso2) {
                        msgPaso2.className = "alert alert-warning py-2 px-3 mb-3 text-center";
                        msgPaso2.innerText = "El código ha expirado. Pulsa «Reenviar código» para recibir uno nuevo.";
                        msgPaso2.style.display = "block";
                    }
                }
            }, 1000);
        }

        function detenerContadorRenovacion() {
            if (intervaloOtpRenovacion) {
                clearInterval(intervaloOtpRenovacion);
                intervaloOtpRenovacion = null;
            }
        }

        async function reenviarOtpRenovacion() {
            const btnReenviar = document.getElementById("btnReenviarOtpRenovacion");
            const msgPaso2 = document.getElementById("mensajeRenovacionPaso2");

            if (btnReenviar) {
                btnReenviar.disabled = true;
                btnReenviar.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Reenviando...';
            }

            try {
                const respuesta = await fetch("/Login/SolicitarOtpRenovacionVoz", {
                    method: "POST"
                });
                const resultado = await respuesta.json();

                if (!resultado.ok) {
                    if (msgPaso2) {
                        msgPaso2.className = "alert alert-danger py-2 px-3 mb-3 text-center";
                        msgPaso2.innerText = resultado.mensaje || "No se pudo reenviar el código.";
                        msgPaso2.style.display = "block";
                    }
                    if (btnReenviar) {
                        btnReenviar.disabled = false;
                        btnReenviar.innerHTML = '<i class="bi bi-arrow-clockwise me-1"></i> Reenviar código';
                    }
                    return;
                }

                limpiarCasillasOtp();
                if (msgPaso2) {
                    msgPaso2.className = "alert alert-success py-2 px-3 mb-3 text-center";
                    msgPaso2.innerText = "Nuevo código enviado a tu correo.";
                    msgPaso2.style.display = "block";
                }

                iniciarContadorRenovacion(resultado.segundos || resultado.segundosVigencia || 60);

                if (btnReenviar) {
                    btnReenviar.innerHTML = '<i class="bi bi-arrow-clockwise me-1"></i> Reenviar código';
                    btnReenviar.disabled = true;
                }

                const primeraCasilla = document.querySelector(".casilla-otp-renovacion[data-index='0']");
                if (primeraCasilla) primeraCasilla.focus();

            } catch (err) {
                console.error(err);
                if (msgPaso2) {
                    msgPaso2.className = "alert alert-danger py-2 px-3 mb-3 text-center";
                    msgPaso2.innerText = "Error de conexión al reenviar el código.";
                    msgPaso2.style.display = "block";
                }
                if (btnReenviar) {
                    btnReenviar.disabled = false;
                    btnReenviar.innerHTML = '<i class="bi bi-arrow-clockwise me-1"></i> Reenviar código';
                }
            }
        }

        function configurarCasillasOtpRenovacion() {
            const casillas = document.querySelectorAll(".casilla-otp-renovacion");
            casillas.forEach((casilla, idx) => {
                casilla.addEventListener("input", function () {
                    const val = this.value.replace(/\D/g, "");
                    this.value = val ? val[val.length - 1] : "";
                    if (this.value && idx < casillas.length - 1) {
                        casillas[idx + 1].focus();
                    }
                    if (obtenerCodigoOtp().length === 6) {
                        verificarOtpRenovacion();
                    }
                });

                casilla.addEventListener("keydown", function (e) {
                    if (e.key === "Backspace" && !this.value && idx > 0) {
                        casillas[idx - 1].focus();
                    }
                });

                casilla.addEventListener("paste", function (e) {
                    e.preventDefault();
                    const pegado = (e.clipboardData || window.clipboardData).getData("text").replace(/\D/g, "").slice(0, 6);
                    if (!pegado) return;
                    for (let i = 0; i < pegado.length; i++) {
                        if (casillas[i]) casillas[i].value = pegado[i];
                    }
                    const proxIndex = Math.min(pegado.length, casillas.length - 1);
                    if (casillas[proxIndex]) casillas[proxIndex].focus();
                    if (pegado.length === 6) {
                        verificarOtpRenovacion();
                    }
                });

                casilla.addEventListener("focus", function () {
                    this.style.borderColor = "#7c3aed";
                    this.style.boxShadow = "0 0 0 3px rgba(124, 58, 237, 0.15)";
                });

                casilla.addEventListener("blur", function () {
                    this.style.borderColor = "#cbd5e1";
                    this.style.boxShadow = "none";
                });
            });
        }

        function limpiarCasillasOtp() {
            document.querySelectorAll(".casilla-otp-renovacion").forEach(c => c.value = "");
        }

        function obtenerCodigoOtp() {
            let codigo = "";
            document.querySelectorAll(".casilla-otp-renovacion").forEach(c => codigo += (c.value || ""));
            return codigo.trim();
        }

        async function verificarOtpRenovacion() {
            const codigo = obtenerCodigoOtp();
            const msgPaso2 = document.getElementById("mensajeRenovacionPaso2");
            const btnVerificar = document.getElementById("btnVerificarOtpRenovacion");

            if (codigo.length !== 6) {
                if (msgPaso2) {
                    msgPaso2.className = "alert alert-danger py-2 px-3 mb-3 text-center";
                    msgPaso2.innerText = "Ingresa los 6 dígitos del código de seguridad.";
                    msgPaso2.style.display = "block";
                }
                return;
            }

            if (btnVerificar) {
                btnVerificar.disabled = true;
                btnVerificar.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Verificando...';
            }

            try {
                const formData = new FormData();
                formData.append("codigo", codigo);

                const respuesta = await fetch("/Login/VerificarOtpRenovacionVoz", {
                    method: "POST",
                    body: formData
                });

                const resultado = await respuesta.json();

                if (!resultado.ok) {
                    if (msgPaso2) {
                        msgPaso2.className = "alert alert-danger py-2 px-3 mb-3 text-center";
                        msgPaso2.innerText = resultado.mensaje || "El código ingresado es incorrecto.";
                        msgPaso2.style.display = "block";
                    }
                    if (btnVerificar) {
                        btnVerificar.disabled = false;
                        btnVerificar.innerHTML = '<i class="bi bi-check-circle-fill me-1"></i> Verificar código';
                    }
                    if (resultado.expirado) {
                        detenerContadorRenovacion();
                        const textoContador = document.getElementById("textoContadorRenovacion");
                        if (textoContador) textoContador.innerHTML = '<strong class="text-danger">El código ha expirado</strong>';
                        const btnReenviar = document.getElementById("btnReenviarOtpRenovacion");
                        if (btnReenviar) btnReenviar.disabled = false;
                        if (btnVerificar) btnVerificar.disabled = true;
                    } else if (resultado.bloqueado) {
                        detenerContadorRenovacion();
                        if (btnVerificar) btnVerificar.disabled = true;
                        const btnReenviar = document.getElementById("btnReenviarOtpRenovacion");
                        if (btnReenviar) btnReenviar.disabled = false;
                    }
                    return;
                }

                // OTP VÁLIDO: Guardar token de renovación temporal y activar modo renovación
                tokenRenovacionVozActivo = resultado.token || resultado.tokenRenovacion;
                modoRenovacionVoz = true;
                detenerContadorRenovacion();

                // 1. Cerrar el modal de renovación
                const modalRenovacionEl = document.getElementById("modalRenovacionBiometria");
                if (modalRenovacionEl) {
                    const modalRen = bootstrap.Modal.getInstance(modalRenovacionEl);
                    if (modalRen) modalRen.hide();
                }

                // 2. Cerrar modal de confirmación si quedó abierto
                const modalConfirmarEl = document.getElementById("modalConfirmarRegistroVozLogin");
                if (modalConfirmarEl) {
                    const modalConfirmar = bootstrap.Modal.getInstance(modalConfirmarEl);
                    if (modalConfirmar) modalConfirmar.hide();
                }

                // 3. Abrir el formulario de registro de voz en modo renovación
                setTimeout(function () {
                    abrirFormularioRenovacionVoz();
                }, 250);

            } catch (err) {
                console.error(err);
                if (msgPaso2) {
                    msgPaso2.className = "alert alert-danger py-2 px-3 mb-3 text-center";
                    msgPaso2.innerText = "Error de conexión al verificar el código.";
                    msgPaso2.style.display = "block";
                }
                if (btnVerificar) {
                    btnVerificar.disabled = false;
                    btnVerificar.innerHTML = '<i class="bi bi-check-circle-fill me-1"></i> Verificar código';
                }
            }
        }

        function cancelarRenovacionDesdeFormulario() {
            modoRenovacionVoz = false;
            tokenRenovacionVozActivo = null;
            liberarRecursosVoz();
            const btnCancelarForm = document.getElementById("btnCancelarRenovacionFormulario");
            if (btnCancelarForm) btnCancelarForm.style.display = "none";
            restaurarModalMetodosSeguridadTrasRenovacion();
        }

        function abrirFormularioRenovacionVoz() {
            const modalSeguridadElemento = document.getElementById("modalVerificacionSeguridad");
            if (!modalSeguridadElemento) return;

            const opcionCorreo = document.getElementById("opcionCorreo");
            const opcionAplicacion = document.getElementById("opcionAplicacion");
            const opcionVoz = document.getElementById("opcionVoz");
            const contenedorCambiar = document.getElementById("contenedorCambiarBiometria");
            const bloqueRegistroVoz = document.getElementById("bloqueRegistroVoz");
            const bloqueVerificacionVoz = document.getElementById("bloqueVerificacionVoz");
            const botonContinuar = modalSeguridadElemento.querySelector(".btn-continuar-seguridad");

            // Ocultar opciones de selección
            if (opcionCorreo) opcionCorreo.style.display = "none";
            if (opcionAplicacion) opcionAplicacion.style.display = "none";
            if (opcionVoz) opcionVoz.style.display = "none";
            if (contenedorCambiar) contenedorCambiar.style.display = "none";
            if (bloqueVerificacionVoz) bloqueVerificacionVoz.style.display = "none";
            if (botonContinuar) botonContinuar.style.display = "none";

            // Mostrar formulario de registro en modo renovación
            if (bloqueRegistroVoz) {
                bloqueRegistroVoz.style.display = "block";

                const tituloRegistro = bloqueRegistroVoz.querySelector("h5");
                if (tituloRegistro) {
                    tituloRegistro.innerHTML = "🎤 Registra tu nueva biometría de voz";
                }

                const descRegistro = bloqueRegistroVoz.querySelector("p");
                if (descRegistro) {
                    descRegistro.innerText = "Tu identidad fue verificada correctamente. Ahora puedes registrar una nueva voz y una nueva palabra o frase secreta.";
                }
            }

            const modalTitulo = modalSeguridadElemento.querySelector(".modal-title");
            if (modalTitulo) modalTitulo.innerText = "Actualizar biometría de voz";

            const modalTexto = modalSeguridadElemento.querySelector(".texto-seguridad");
            if (modalTexto) modalTexto.innerText = "Graba tu nueva palabra o frase secreta para actualizar tu seguridad.";

            // Reiniciar estado del botón de grabación
            const btnGrabar = document.getElementById("btnGrabarVozLogin");
            const estadoVoz = document.getElementById("estadoVozLogin");
            if (btnGrabar) {
                btnGrabar.disabled = false;
                btnGrabar.classList.remove("grabando", "btn-danger");
                btnGrabar.classList.add("btn-primary");
                btnGrabar.innerHTML = "🎙️ Grabar mi nueva voz";
            }
            if (estadoVoz) {
                estadoVoz.innerText = "⚪ Pulsa para grabar tu nueva voz y palabra secreta";
            }

            // Reset de variables de audio y nueva frase
            audioBlobLogin = null;
            audioChunksLogin = [];
            fraseRegistroVozLogin = "";

            document.querySelectorAll(".textoFraseEsperadaVoz, #textoFraseEsperadaRegistroVoz, #textoFraseEsperadaVoz").forEach(el => {
                el.innerText = "Pulsa «Grabar mi nueva voz» y pronuncia con claridad tu nueva frase secreta";
            });

            const btnCancelarForm = document.getElementById("btnCancelarRenovacionFormulario");
            if (btnCancelarForm) btnCancelarForm.style.display = "block";

            const modalSeguridad = bootstrap.Modal.getOrCreateInstance(modalSeguridadElemento);
            modalSeguridad.show();
        }

        function restaurarModalMetodosSeguridadTrasRenovacion() {
            const bloqueRegistroVoz = document.getElementById("bloqueRegistroVoz");
            const opcionCorreo = document.getElementById("opcionCorreo");
            const opcionAplicacion = document.getElementById("opcionAplicacion");
            const opcionVoz = document.getElementById("opcionVoz");
            const botonContinuar = document.querySelector(".btn-continuar-seguridad");

            if (bloqueRegistroVoz) {
                bloqueRegistroVoz.style.display = "none";
                const h5 = bloqueRegistroVoz.querySelector("h5");
                if (h5) h5.innerText = "Registro de voz obligatorio";
                const p = bloqueRegistroVoz.querySelector("p");
                if (p) p.innerText = "No tienes registrada una voz de seguridad. Debes realizar una grabación para continuar.";
            }

            if (opcionCorreo) opcionCorreo.style.display = "flex";
            if (opcionAplicacion) opcionAplicacion.style.display = "flex";
            if (opcionVoz) opcionVoz.style.display = "flex";
            if (botonContinuar) botonContinuar.style.display = "inline-block";

            const titulo = document.querySelector("#modalVerificacionSeguridad .modal-title");
            if (titulo) titulo.innerText = "Verificación de seguridad";

            const texto = document.querySelector("#modalVerificacionSeguridad .texto-seguridad");
            if (texto) texto.innerText = "Para continuar, selecciona cómo deseas verificar tu identidad.";

            document.querySelectorAll(".textoFraseEsperadaVoz, #textoFraseEsperadaRegistroVoz, #textoFraseEsperadaVoz").forEach(el => {
                el.innerText = fraseVozBD || "Di tu palabra o frase de seguridad registrada";
            });

            // Seleccionar método voz con el enlace visible
            seleccionarMetodo("voz");
        }

            // ==========================================
            // CONTINUAR
            // ==========================================

            async function continuarVerificacion() {

                // ==========================================
                // ELIGIÓ VOZ PERO NO TIENE VOZ REGISTRADA
                // ==========================================

                if (
                    metodoVerificacion === "voz" &&
                    !tieneRegistroVozLogin
                ) {

                    const opcionCorreo =
                        document.getElementById(
                            "opcionCorreo"
                        );

                    const opcionAplicacion =
                        document.getElementById(
                            "opcionAplicacion"
                        );

                    const opcionVoz =
                        document.getElementById(
                            "opcionVoz"
                        );

                    const bloqueRegistroVoz =
                        document.getElementById(
                            "bloqueRegistroVoz"
                        );

                    const bloqueVerificacionVoz =
                        document.getElementById(
                            "bloqueVerificacionVoz"
                        );

                    const botonContinuar =
                        document.querySelector(
                            ".btn-continuar-seguridad"
                        );

                    const titulo =
                        document.querySelector(
                            "#modalVerificacionSeguridad .modal-title"
                        );

                    const texto =
                        document.querySelector(
                            "#modalVerificacionSeguridad .texto-seguridad"
                        );


                    // Ocultar métodos

                    if (opcionCorreo) {
                        opcionCorreo.style.display =
                            "none";
                    }

                    if (opcionAplicacion) {
                        opcionAplicacion.style.display =
                            "none";
                    }

                    if (opcionVoz) {
                        opcionVoz.style.display =
                            "none";
                    }


                    // Mostrar REGISTRO de voz

                    if (bloqueRegistroVoz) {
                        bloqueRegistroVoz.style.display =
                            "block";
                    }


                    // NO mostrar todavía verificación

                    if (bloqueVerificacionVoz) {
                        bloqueVerificacionVoz.style.display =
                            "none";
                    }


                    if (titulo) {
                        titulo.innerText =
                            "Registrar voz";
                    }


                    if (texto) {
                        texto.innerText =
                            "Para utilizar el acceso por voz debes registrar primero tu voz de seguridad.";
                    }


                    if (botonContinuar) {
                        botonContinuar.style.display =
                            "none";
                    }


                    return;
                }

                // ==========================================
                // VERIFICACIÓN POR VOZ
                // ==========================================

                if (metodoVerificacion === "voz") {

                    const opcionCorreo =
                        document.getElementById("opcionCorreo");

                    const opcionAplicacion =
                        document.getElementById("opcionAplicacion");

                    const opcionVoz =
                        document.getElementById("opcionVoz");

                    const bloqueRegistroVoz =
                        document.getElementById("bloqueRegistroVoz");

                    const bloqueVerificacionVoz =
                        document.getElementById("bloqueVerificacionVoz");



                    const botonContinuar =
                        document.querySelector(".btn-continuar-seguridad");

                    const titulo =
                        document.querySelector(
                            "#modalVerificacionSeguridad .modal-title"
                        );

                    const texto =
                        document.querySelector(
                            "#modalVerificacionSeguridad .texto-seguridad"
                        );


                    // ==========================================
                    // OCULTAR OPCIONES
                    // ==========================================

                    if (opcionCorreo) {
                        opcionCorreo.style.display = "none";
                    }

                    if (opcionAplicacion) {
                        opcionAplicacion.style.display = "none";
                    }

                    if (opcionVoz) {
                        opcionVoz.style.display = "none";
                    }




                    // ==========================================
                    // OCULTAR REGISTRO DE VOZ
                    // ==========================================

                    if (bloqueRegistroVoz) {
                        bloqueRegistroVoz.style.display = "none";
                    }


                    // ==========================================
                    // MOSTRAR VERIFICACIÓN DE VOZ
                    // ==========================================

                    if (bloqueVerificacionVoz) {
                        bloqueVerificacionVoz.style.display = "block";
                    }


                    // ==========================================
                    // CAMBIAR TÍTULO
                    // ==========================================

                    if (titulo) {
                        titulo.innerText =
                            "Verificación por voz";
                    }


                    // ==========================================
                    // CAMBIAR DESCRIPCIÓN
                    // ==========================================

                    if (texto) {
                        texto.innerText =
                            "Di tu frase de seguridad para verificar tu identidad.";
                    }


                    // ==========================================
                    // OCULTAR BOTÓN CONTINUAR
                    // ==========================================

                    if (botonContinuar) {
                        botonContinuar.style.display = "none";
                    }


                    // ==========================================
                    // REINICIAR ESTADO
                    // ==========================================

                    const estado =
                        document.getElementById(
                            "estadoVerificacionVoz"
                        );

                    const boton =
                        document.getElementById(
                            "btnVerificarVoz"
                        );

                    if (estado) {

                        estado.innerText =
                            "⚪ Aún no has realizado la verificación";

                    }

                    if (boton) {

                        boton.disabled = false;

                        boton.innerHTML =
                            "🎙️ Hablar palabra clave";

                    }

                    return;
                }


        if (metodoVerificacion === "aplicacion") {

            const boton =
                document.querySelector(
                    ".btn-continuar-seguridad"
                );

            if (boton) {
                boton.disabled = true;

                boton.innerHTML =
                    '<span class="spinner-border spinner-border-sm"></span> Verificando...';
            }

            try {

                const respuesta =
                    await fetch(
                        "/Login/CrearSolicitudAuthenticator",
                        {
                            method: "POST"
                        }
                    );

                const resultado =
                    await respuesta.json();

                if (!resultado.ok) {

                    alert(
                        resultado.mensaje ||
                        "No se pudo iniciar la verificación."
                    );

                    return;
                }

                // ==========================================
                // PRIMERA VEZ
                // TODAVÍA NO TIENE CELULAR VINCULADO
                // ==========================================

        if (!resultado.configurado) {

            abrirFiltroAuthenticator(
                resultado.correoOculto
            );

            return;
        }

                // ==========================================
                // YA TIENE AUTHENTICATOR CONFIGURADO
                // ==========================================


        solicitudAuthenticatorActual =
            resultado.solicitudId;

        solicitudAuthenticatorTerminada =
            false;

        iniciarContadorSolicitudAuthenticator();

        mostrarModalAuthenticator(
            "Confirma el inicio de sesión",
            "Abre CrediPlus Authenticator en tu celular e ingresa el número mostrado.",
            resultado.numero,
            "esperando"
        );

        esperarRespuestaAuthenticator(
            resultado.solicitudId
        );
                console.log(
                    "Solicitud Authenticator:",
                    resultado.solicitudId
                );

            }
            catch (error) {

                console.error(
                    "ERROR AUTHENTICATOR:",
                    error
                );

                alert(
                    "No se pudo conectar con CrediPlus Authenticator."
                );
            }
            finally {

                if (boton) {

                    boton.disabled = false;

                    boton.innerHTML =
                        'Continuar <i class="bi bi-arrow-right ms-1"></i>';
                }
            }

            return;
        }


                // ==========================================
                // CORREO
                // ==========================================

                const boton =
                    document.querySelector(
                        ".btn-continuar-seguridad"
                    );


                if (boton) {

                    boton.disabled = true;

                    boton.innerHTML =
                        '<span class="spinner-border spinner-border-sm"></span> Enviando...';

                }


                try {

                    const respuesta =
                        await fetch(
                            '/Login/EnviarCodigoLogin',
                            {
                                method: 'POST',
                                headers: {
                                    'Content-Type': 'application/json'
                                }
                            }
                        );


                    const resultado =
                        await respuesta.json();


                    if (!resultado.ok) {

                        // ==========================================
                        // BLOQUEO POR 3 INTENTOS
                        // ==========================================

                        if (resultado.bloqueadoVoz) {

                            grabandoVerificacionVoz = false;
                            procesandoVoz = false;

                            estado.innerText =
                                "🔒 " +
                                resultado.mensaje;

                            boton.disabled = true;

                            boton.innerHTML =
                                "🔒 Voz bloqueada";


                            // Esperar un momento para que vea
                            // qué ocurrió.
                            setTimeout(function() {

                                // Volver al primer modal
                                restaurarModalSeguridad();

                                // Mostrar las opciones nuevamente
                                bloquearOpcionVoz(
                                    resultado.segundosRestantes || 300
                                );

                            }, 1200);

                            return;
                        }


                        // ==========================================
                        // TODAVÍA QUEDAN INTENTOS
                        // ==========================================

                        estado.innerText =
                            "❌ " +
                            (
                                resultado.mensaje ||
                                "No se pudo verificar la voz."
                            );


                        boton.disabled = false;

                        boton.innerHTML =
                            "🎙️ Intentar nuevamente";


                        grabandoVerificacionVoz = false;
                        procesandoVoz = false;

                        return;
                    }


                    // ==========================================
                    // LIMPIAR CÓDIGO
                    // ==========================================

                    document.getElementById(
                        "codigoLogin"
                    ).value = "";

                    document.getElementById(
                        "mensajeCodigoLogin"
                    ).style.display = "none";

                    // ==========================================
                    // CERRAR MODAL 1 Y LUEGO ABRIR MODAL 2
                    // ==========================================

                    abrirModalCodigo();

                    // ==========================================
                    // INICIAR CONTADOR
                    // ==========================================

                    iniciarContadorCodigo();

                }
                catch (error) {

                    console.error(error);

                    alert(
                        "No se pudo enviar el código de seguridad."
                    );

                }
                finally {

                    if (boton) {

                        boton.disabled = false;

                        boton.innerHTML =
                            'Continuar <i class="bi bi-arrow-right ms-1"></i>';

                    }

                }
            }
            // ==========================================
            // REENVIAR CÓDIGO
            // ==========================================

            async function reenviarCodigoLogin() {

                const boton =
                    document.getElementById(
                        "btnReenviarCodigo"
                    );

                const mensaje =
                    document.getElementById(
                        "mensajeCodigoLogin"
                    );


                if (!boton) {
                    return;
                }


                // No permitir reenviar antes de tiempo

                if (segundosCodigo > 0) {

                    return;

                }


                boton.disabled = true;

                boton.innerHTML =
                    '<span class="spinner-border spinner-border-sm"></span> Enviando...';


                try {

                    const respuesta =
                        await fetch(
                            "/Login/EnviarCodigoLogin",
                            {
                                method: "POST",
                                headers: {
                                    "Content-Type": "application/json"
                                }
                            }
                        );


                    const resultado =
                        await respuesta.json();


                    if (!resultado.ok) {

                        mensaje.style.display = "block";

                        mensaje.className =
                            "alert alert-danger mt-3";

                        mensaje.innerText =
                            resultado.mensaje ||
                            "No se pudo reenviar el código.";

                        boton.disabled = false;

                        boton.innerHTML =
                            '<i class="bi bi-arrow-clockwise"></i> Reenviar código';

                        return;
                    }


                    // ==========================================
                    // LIMPIAR CÓDIGO ANTERIOR
                    // ==========================================

                    document.getElementById(
                        "codigoLogin"
                    ).value = "";


                    // ==========================================
                    // MENSAJE
                    // ==========================================

                    mensaje.style.display = "block";

                    mensaje.className =
                        "alert alert-success mt-3";

                    mensaje.innerText =
                        "Se ha enviado un nuevo código a tu correo.";


                    // ==========================================
                    // NUEVO CONTADOR
                    // ==========================================

                    iniciarContadorCodigo();


                }
                catch (error) {

                    console.error(error);

                    mensaje.style.display = "block";

                    mensaje.className =
                        "alert alert-danger mt-3";

                    mensaje.innerText =
                        "No se pudo reenviar el código.";

                    boton.disabled = false;

                }
                finally {

                    boton.innerHTML =
                        '<i class="bi bi-arrow-clockwise"></i> Reenviar código';

                }

            }
            async function verificarCodigoLogin() {

                const codigo =
                    document.getElementById("codigoLogin")
                        .value
                        .trim()
                        .toUpperCase();

                const mensaje =
                    document.getElementById(
                        "mensajeCodigoLogin"
                    );

                const boton =
                    document.getElementById(
                        "btnVerificarCodigo"
                    );

                if (codigo.length !== 6) {

                    mensaje.style.display = "block";
                    mensaje.className = "alert alert-warning mt-3";
                    mensaje.innerText =
                        "Debes ingresar un código de 6 caracteres.";

                    return;
                }

                boton.disabled = true;

                boton.innerHTML =
                    '<span class="spinner-border spinner-border-sm"></span> Verificando...';

                try {

                    const formData = new FormData();

                    formData.append(
                        "codigo",
                        codigo
                    );

                    const respuesta = await fetch(
                        '/Login/VerificarCodigoLogin',
                        {
                            method: 'POST',
                            body: formData
                        }
                    );

                    const resultado =
                        await respuesta.json();

                    if (!resultado.ok) {

                        mensaje.style.display = "block";
                        mensaje.className =
                            "alert alert-danger mt-3";

                        mensaje.innerText =
                            resultado.mensaje;

                        boton.disabled = false;

                        boton.innerHTML =
                            '<i class="bi bi-shield-check"></i> Verificar código';

                        return;
                    }

                    // Código correcto
                    mensaje.style.display = "block";
                    mensaje.className =
                        "alert alert-success mt-3";

                    mensaje.innerText =
                        "Código correcto. Ingresando...";

                    // Esperamos un momento para mostrar el mensaje
                    setTimeout(function() {

                        window.location.href =
                            resultado.redirectUrl;

                    }, 700);

                }
                catch (error) {

                    console.error(error);

                    mensaje.style.display = "block";
                    mensaje.className =
                        "alert alert-danger mt-3";

                    mensaje.innerText =
                        "Ocurrió un error al verificar el código.";

                    boton.disabled = false;

                    boton.innerHTML =
                        '<i class="bi bi-shield-check"></i> Verificar código';
                }
            }
            // ==========================================
            // INICIAR SESIÓN
            // ==========================================
                    let intervaloBloqueoLogin = null;
        let bloqueoLoginHasta = 0;

        function iniciarBloqueoLogin(segundos, boton, mensaje) {
            if (intervaloBloqueoLogin !== null) {
                clearInterval(intervaloBloqueoLogin);
            }

            bloqueoLoginHasta = Date.now() + Math.max(0, segundos) * 1000;

            function actualizarContadorLogin() {
                const restantes = Math.max(
                    0,
                    Math.ceil((bloqueoLoginHasta - Date.now()) / 1000)
                );

                if (restantes === 0) {
                    clearInterval(intervaloBloqueoLogin);
                    intervaloBloqueoLogin = null;

                    if (boton) {
                        boton.disabled = false;
                        boton.innerText = "Iniciar sesión";
                    }

                    if (mensaje) {
                        mensaje.style.display = "none";
                    }

                    return;
                }

                const minutos = String(Math.floor(restantes / 60)).padStart(2, "0");
                const segundosTexto = String(restantes % 60).padStart(2, "0");

                if (mensaje) {
                    mensaje.style.display = "block";
                    mensaje.className = "alert alert-danger mt-3";
                    mensaje.innerText =
                        "Demasiados intentos fallidos. Inténtalo nuevamente en " +
                        minutos + ":" + segundosTexto;
                }

                if (boton) {
                    boton.disabled = true;
                    boton.innerText = "Bloqueado " + minutos + ":" + segundosTexto;
                }
            }

            actualizarContadorLogin();
            intervaloBloqueoLogin = setInterval(actualizarContadorLogin, 250);
        }
            document.addEventListener("DOMContentLoaded", function() {

                const formulario = document.getElementById("formLogin");

                if (!formulario) {
                    console.error("No se encontró el formulario #formLogin");
                    return;
                }

                const loginConfigEl = document.getElementById("loginConfig");
                if (loginConfigEl) {
                    const isBloqueado = loginConfigEl.getAttribute("data-bloqueado") === "true";
                    const segundosBloqueo = parseInt(loginConfigEl.getAttribute("data-segundos-bloqueo") || "60", 10);
                    const intentosFallidos = parseInt(loginConfigEl.getAttribute("data-intentos-fallidos") || "0", 10);
                    const intentosRestantes = loginConfigEl.getAttribute("data-intentos-restantes") || "0";

                    if (isBloqueado) {
                        const btnInit = formulario.querySelector('button[type="submit"]');
                        const msgInit = document.getElementById("mensajeLogin");
                        iniciarBloqueoLogin(segundosBloqueo, btnInit, msgInit);
                    } else if (intentosFallidos > 0) {
                        const msgInitIntentos = document.getElementById("mensajeLogin");
                        if (msgInitIntentos) {
                            msgInitIntentos.style.display = "block";
                            msgInitIntentos.className = "alert alert-danger mt-3";
                            msgInitIntentos.innerText = "DNI o contraseña incorrectos. Intentos restantes: " + intentosRestantes + ".";
                        }
                    }
                }

                formulario.addEventListener("submit", async function(event) {

                    // MUY IMPORTANTE:
                    // evita que el navegador vaya a /Login/IniciarSesion
                    event.preventDefault();

                    const boton = formulario.querySelector('button[type="submit"]');
                    const mensaje = document.getElementById("mensajeLogin");

                    if (mensaje) {
                        mensaje.style.display = "none";
                    }

                    const turnstileInput = formulario.querySelector('[name="cf-turnstile-response"]');
                    if (window.turnstile && turnstileInput && !turnstileInput.value) {
                        if (mensaje) {
                            mensaje.style.display = "block";
                            mensaje.className = "alert alert-warning mt-3";
                            mensaje.innerText = "Verificación de seguridad pendiente. Por favor espera un momento...";
                        }
                        return;
                    }

                    if (boton) {
                        boton.disabled = true;
                        boton.innerHTML =
                            '<span class="spinner-border spinner-border-sm"></span> Verificando...';
                    }

                    try {

                        const datos = new FormData(formulario);

                        const respuesta = await fetch(formulario.action, {
                            method: "POST",
                            body: datos
                        });

                        const resultado = await respuesta.json();
                        if (resultado.fraseVoz) {
                            fraseVozBD = resultado.fraseVoz;
                            document.querySelectorAll("#textoFraseEsperadaVoz").forEach(el => {
                                el.innerText = fraseVozBD;
                            });

                            console.log(
                                "Frase de seguridad configurada para verificación:",
                                fraseVozBD
                            );
                        }
                        // ==========================================
                        // SABER SI YA PUEDE UTILIZAR VOZ
                        // ==========================================

                        vozHabilitadaLogin =
                            resultado.vozHabilitada === true;
                        tieneRegistroVozLogin =
                            resultado.tieneRegistroVoz === true;

                        if (resultado.correo) {
                            correoUsuarioOcultoRenovacion = enmascararCorreoCliente(resultado.correo);
                        }
                        // ==========================================
                        // DNI / CONTRASEÑA INCORRECTOS
                        // ==========================================

                                if (!resultado.ok) {

            if (window.turnstile) {
                try { turnstile.reset(); } catch (e) {}
            }

            if (resultado.bloqueado === true) {
                iniciarBloqueoLogin(
                    Number(resultado.segundosRestantes) || 60,
                    boton,
                    mensaje
                );
                return;
            }

            if (mensaje) {
                mensaje.style.display = "block";
                mensaje.className = "alert alert-danger mt-3";
                mensaje.innerText =
                    resultado.mensaje || "DNI o contraseña incorrectos.";

                if (typeof resultado.intentosRestantes === "number") {
                    mensaje.innerText +=
                        " Intentos restantes: " + resultado.intentosRestantes + ".";
                }
            }

            if (boton) {
                boton.disabled = false;
                boton.innerHTML = "Iniciar sesión";
            }

            return;
        }

                        // ==========================================
                        // DNI + CONTRASEÑA CORRECTOS
                        // ==========================================
                                if (intervaloBloqueoLogin !== null) {
            clearInterval(intervaloBloqueoLogin);
            intervaloBloqueoLogin = null;
        }
                        const modalElemento =
                            document.getElementById("modalVerificacionSeguridad");

                        if (!modalElemento) {

                            console.error(
                                "No se encontró #modalVerificacionSeguridad"
                            );

                            return;
                        }

                        // ==========================================
                        // VERIFICAR SI NECESITA REGISTRAR VOZ
                        // ==========================================

                        const bloqueRegistroVoz =
                            document.getElementById("bloqueRegistroVoz");

                        const opcionCorreo =
                            document.getElementById("opcionCorreo");

                        const opcionAplicacion =
                            document.getElementById("opcionAplicacion");
                        const opcionVoz =
                            document.getElementById("opcionVoz");

                        const footerModal =
                            modalElemento.querySelector(".modal-footer");

                        // ==========================================
                        // SIEMPRE MOSTRAR PRIMERO LOS MÉTODOS
                        // DE VERIFICACIÓN
                        // ==========================================

                        if (bloqueRegistroVoz) {
                            bloqueRegistroVoz.style.display = "none";
                        }

                        const bloqueVerificacionVoz =
                            document.getElementById(
                                "bloqueVerificacionVoz"
                            );

                        if (bloqueVerificacionVoz) {
                            bloqueVerificacionVoz.style.display = "none";
                        }


                        // Mostrar siempre las opciones

                        if (opcionCorreo) {
                            opcionCorreo.style.display = "flex";
                        }

                        if (opcionAplicacion) {
                            opcionAplicacion.style.display = "flex";
                        }

                        if (opcionVoz) {
                            opcionVoz.style.display = "flex";
                        }


                        // ==========================================
                        // RESTAURAR TÍTULO NORMAL
                        // ==========================================

                        const titulo =
                            modalElemento.querySelector(
                                ".modal-title"
                            );

                        const texto =
                            modalElemento.querySelector(
                                ".texto-seguridad"
                            );

                        if (titulo) {
                            titulo.innerText =
                                "Verificación de seguridad";
                        }

                        if (texto) {
                            texto.innerText =
                                "Para continuar, selecciona cómo deseas verificar tu identidad.";
                        }


                        // ==========================================
                        // MOSTRAR BOTÓN CONTINUAR
                        // ==========================================

                        const botonContinuar =
                            modalElemento.querySelector(
                                ".btn-continuar-seguridad"
                            );

                        if (botonContinuar) {

                            botonContinuar.style.display =
                                "inline-block";

                            botonContinuar.disabled =
                                false;

                            botonContinuar.innerHTML =
                                'Continuar <i class="bi bi-arrow-right ms-1"></i>';
                        }


                        // ==========================================
                        // SELECCIONAR CORREO POR DEFECTO
                        // ==========================================

                        seleccionarMetodo("correo");




                        const modal =
                            bootstrap.Modal.getOrCreateInstance(
                                modalElemento
                            );

                        modal.show();

                        if (boton) {
                            boton.disabled = false;
                            boton.innerHTML = "Iniciar sesión";
                        }

                    }
                    catch (error) {

                        console.error("Error al iniciar sesión:", error);

                        if (mensaje) {
                            mensaje.style.display = "block";
                            mensaje.className = "alert alert-danger mt-3";
                            mensaje.innerText =
                                "Ocurrió un error al iniciar sesión.";
                        }

                        if (boton) {
                            boton.disabled = false;
                            boton.innerHTML = "Iniciar sesión";
                        }
                    }

                });

            });
            // ==========================================
            // DETECCIÓN DE FORMATOS Y SOPORTE DE AUDIO
            // (ANDROID, IPHONE / SAFARI, CHROME, EDGE)
            // ==========================================

            function obtenerMimeTypeSoportado() {
                const tipos = [
                    "audio/webm;codecs=opus",
                    "audio/webm",
                    "audio/mp4",
                    "audio/ogg;codecs=opus",
                    "audio/ogg",
                    "audio/wav"
                ];
                if (window.MediaRecorder && typeof MediaRecorder.isTypeSupported === "function") {
                    for (let i = 0; i < tipos.length; i++) {
                        if (MediaRecorder.isTypeSupported(tipos[i])) {
                            return tipos[i];
                        }
                    }
                }
                return "";
            }

            function obtenerExtensionAudio(mimeType) {
                if (!mimeType) return "webm";
                const m = mimeType.toLowerCase();
                if (m.includes("mp4")) return "mp4";
                if (m.includes("ogg")) return "ogg";
                if (m.includes("wav")) return "wav";
                return "webm";
            }

            // ==========================================
            // VARIABLES GLOBALES DE AUDIO
            // ==========================================

            let mediaRecorderLogin = null;
            let audioChunksLogin = [];
            let audioBlobLogin = null;
            let grabandoVozLogin = false;
            let streamActualLogin = null;
            let temporizadorRegistroLogin = null;

            let reconocimientoRegistroVozLogin = null;
            let fraseRegistroVozLogin = "";
            let finalizandoRegistroVozLogin = false;

            let mediaRecorderVerificacion = null;
            let audioChunksVerificacion = [];
            let grabandoVerificacionVoz = false;
            let streamActualVerificacion = null;
            let verificacionCancelada = false;

            let reconocimientoVoz = null;
            let fraseReconocida = "";

            let procesandoVoz = false;
            let temporizadorVoz = null;

            // ==========================================
            // LIBERACIÓN SEGURA DE RECURSOS DE AUDIO
            // ==========================================

            function liberarRecursosVoz() {
                // Marcar cancelación explícita para evitar que onstop envíe audio incompleto
                verificacionCancelada = true;

                // 1. Liberar pistas del micrófono de registro
                if (streamActualLogin) {
                    try {
                        streamActualLogin.getTracks().forEach(track => {
                            try { track.stop(); } catch (e) {}
                        });
                    } catch (e) {}
                    streamActualLogin = null;
                }

                // 2. Liberar pistas del micrófono de verificación
                if (streamActualVerificacion) {
                    try {
                        streamActualVerificacion.getTracks().forEach(track => {
                            try { track.stop(); } catch (e) {}
                        });
                    } catch (e) {}
                    streamActualVerificacion = null;
                }

                // 3. Detener grabadores
                if (mediaRecorderLogin && mediaRecorderLogin.state !== "inactive") {
                    try { mediaRecorderLogin.stop(); } catch (e) {}
                }
                if (mediaRecorderVerificacion && mediaRecorderVerificacion.state !== "inactive") {
                    try { mediaRecorderVerificacion.stop(); } catch (e) {}
                }

                // 4. Detener reconocedores de voz
                if (reconocimientoRegistroVozLogin) {
                    try { reconocimientoRegistroVozLogin.stop(); } catch (e) {}
                }
                if (reconocimientoVoz) {
                    try { reconocimientoVoz.stop(); } catch (e) {}
                }

                // 5. Cancelar temporizadores activos
                if (temporizadorVoz) {
                    clearTimeout(temporizadorVoz);
                    temporizadorVoz = null;
                }
                if (temporizadorRegistroLogin) {
                    clearTimeout(temporizadorRegistroLogin);
                    temporizadorRegistroLogin = null;
                }

                // 6. Restablecer banderas de estado
                grabandoVozLogin = false;
                finalizandoRegistroVozLogin = false;
                grabandoVerificacionVoz = false;
                procesandoVoz = false;

                // 7. Restaurar interfaz si los botones no están en proceso de envío
                const btnReg = document.getElementById("btnGrabarVozLogin");
                if (btnReg && !btnReg.disabled) {
                    btnReg.classList.remove("grabando", "btn-danger");
                    btnReg.classList.add("btn-primary");
                    btnReg.innerHTML = "🎙️ Grabar mi voz";
                }

                const btnVer = document.getElementById("btnVerificarVoz");
                if (btnVer && !btnVer.disabled) {
                    btnVer.classList.remove("grabando", "btn-danger");
                    btnVer.classList.add("btn-success");
                    btnVer.innerHTML = "🎙️ Hablar palabra clave";
                }
            }

            // ==========================================
            // NORMALIZAR TEXTO
            // ==========================================

            function normalizarTextoVoz(texto) {
                if (!texto) return "";
                return texto
                    .toLowerCase()
                    .normalize("NFD")
                    .replace(/[\u0300-\u036f]/g, "")
                    .replace(/[.,!?;:]/g, "")
                    .replace(/\s+/g, " ")
                    .trim();
            }

            // ==========================================
            // REGISTRO DE VOZ DESDE LOGIN
            // ==========================================

            async function grabarVozLogin() {
                const boton = document.getElementById("btnGrabarVozLogin");
                const estado = document.getElementById("estadoVozLogin");

                // Si ya está grabando, permitir finalizar con el mismo botón
                if (grabandoVozLogin) {
                    finalizarRegistroVozLogin();
                    return;
                }

                if (finalizandoRegistroVozLogin) {
                    return;
                }

                // Comprobar HTTPS y soporte de micrófono
                if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                    if (window.isSecureContext === false) {
                        estado.innerText = "❌ El micrófono requiere una conexión segura (HTTPS).";
                    } else {
                        estado.innerText = "❌ Tu navegador no permite utilizar el micrófono. Verifica la configuración.";
                    }
                    return;
                }

                try {
                    estado.innerText = "⏳ Solicitando acceso al micrófono...";

                    const stream = await navigator.mediaDevices.getUserMedia({
                        audio: {
                            echoCancellation: true,
                            noiseSuppression: true,
                            autoGainControl: true
                        }
                    });

                    streamActualLogin = stream;

                    audioChunksLogin = [];
                    audioBlobLogin = null;
                    fraseRegistroVozLogin = "";
                    finalizandoRegistroVozLogin = false;

                    const mimeType = obtenerMimeTypeSoportado();
                    const opciones = mimeType ? { mimeType: mimeType } : undefined;

                    mediaRecorderLogin = opciones ? new MediaRecorder(stream, opciones) : new MediaRecorder(stream);

                    mediaRecorderLogin.ondataavailable = function(event) {
                        if (event.data && event.data.size > 0) {
                            audioChunksLogin.push(event.data);
                        }
                    };

                    mediaRecorderLogin.onstop = function() {
                        const tipoFinal = (mediaRecorderLogin && mediaRecorderLogin.mimeType) || mimeType || "audio/webm";
                        audioBlobLogin = new Blob(audioChunksLogin, { type: tipoFinal });

                        // Liberar pistas del micrófono inmediatamente
                        if (streamActualLogin) {
                            try {
                                streamActualLogin.getTracks().forEach(track => track.stop());
                            } catch (e) {}
                            streamActualLogin = null;
                        }

                        grabandoVozLogin = false;

                        if (boton) {
                            boton.disabled = false;
                            boton.classList.remove("grabando", "btn-danger");
                            boton.classList.add("btn-primary");
                            boton.innerHTML = "🎙️ Grabar nuevamente";
                        }

                        mostrarConfirmacionRegistroVozLogin();
                    };

                    // Reconocimiento de voz asistido (si el navegador lo soporta)
                    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
                    if (SpeechRecognition) {
                        try {
                            reconocimientoRegistroVozLogin = new SpeechRecognition();
                            reconocimientoRegistroVozLogin.lang = "es-PE";
                            reconocimientoRegistroVozLogin.continuous = false;
                            reconocimientoRegistroVozLogin.interimResults = false;

                            reconocimientoRegistroVozLogin.onresult = function(event) {
                                let textoFinal = "";
                                for (let i = event.resultIndex; i < event.results.length; i++) {
                                    if (event.results[i].isFinal) {
                                        textoFinal += event.results[i][0].transcript.trim() + " ";
                                    }
                                }
                                textoFinal = textoFinal.trim();
                                if (textoFinal !== "") {
                                    fraseRegistroVozLogin = textoFinal;
                                    console.log("🎙️ Frase detectada:", fraseRegistroVozLogin);
                                    estado.innerText = "🟢 Voz detectada. Preparando confirmación...";
                                    finalizarRegistroVozLogin();
                                }
                            };

                            reconocimientoRegistroVozLogin.onerror = function(event) {
                                console.log("SpeechRecognition aviso en registro:", event.error);
                            };

                            try {
                                reconocimientoRegistroVozLogin.start();
                            } catch (e) {
                                console.log("SpeechRecognition start error:", e);
                            }
                        } catch (e) {
                            console.log("SpeechRecognition init error:", e);
                        }
                    }

                    // Iniciar grabación de audio
                    mediaRecorderLogin.start();
                    grabandoVozLogin = true;

                    boton.disabled = false;
                    boton.classList.remove("btn-primary");
                    boton.classList.add("grabando", "btn-danger");
                    boton.innerHTML = "⏹️ Detener grabación";

                    estado.innerText = "🔴 Grabando... Di tu palabra o frase clave registrada";

                    // Temporizador de seguridad: finalizar grabación tras 4.5 segundos si no se pulsa detener
                    temporizadorRegistroLogin = setTimeout(function() {
                        if (grabandoVozLogin && !finalizandoRegistroVozLogin) {
                            finalizarRegistroVozLogin();
                        }
                    }, 4500);

                } catch (error) {
                    console.error("Error al acceder al micrófono:", error);
                    liberarRecursosVoz();

                    if (boton) {
                        boton.disabled = false;
                        boton.classList.remove("grabando", "btn-danger");
                        boton.classList.add("btn-primary");
                        boton.innerHTML = "🎙️ Grabar mi voz";
                    }

                    if (error.name === "NotAllowedError" || error.name === "PermissionDeniedError") {
                        estado.innerText = "❌ No se pudo acceder al micrófono. Verifica los permisos del navegador y vuelve a intentarlo.";
                    } else if (error.name === "NotFoundError" || error.name === "DevicesNotFoundError") {
                        estado.innerText = "❌ No se encontró ningún micrófono en este dispositivo.";
                    } else {
                        estado.innerText = "❌ No se pudo acceder al micrófono. Verifica los permisos.";
                    }
                }
            }

            function finalizarRegistroVozLogin() {
                if (finalizandoRegistroVozLogin || !grabandoVozLogin) {
                    return;
                }
                finalizandoRegistroVozLogin = true;

                if (temporizadorRegistroLogin) {
                    clearTimeout(temporizadorRegistroLogin);
                    temporizadorRegistroLogin = null;
                }

                const estado = document.getElementById("estadoVozLogin");
                if (estado) {
                    estado.innerText = "🟢 Procesando grabación...";
                }

                if (reconocimientoRegistroVozLogin) {
                    try { reconocimientoRegistroVozLogin.stop(); } catch (e) {}
                }

                if (mediaRecorderLogin && mediaRecorderLogin.state !== "inactive") {
                    try { mediaRecorderLogin.stop(); } catch (e) {}
                }
            }

            function mostrarConfirmacionRegistroVozLogin() {
                const estado = document.getElementById("estadoVozLogin");

                if (!audioBlobLogin || audioBlobLogin.size === 0) {
                    audioBlobLogin = null;
                    finalizandoRegistroVozLogin = false;
                    if (estado) {
                        estado.innerText = "⚪ No se capturó audio. Intenta grabar nuevamente.";
                    }
                    return;
                }

                let frase = fraseRegistroVozLogin.trim();
                if (frase === "") {
                    frase = "Mi voz protege mi cuenta";
                    fraseRegistroVozLogin = frase;
                }

                const texto = document.getElementById("textoRegistroVozDetectado");
                if (texto) {
                    texto.innerText = frase;
                }

                const modalElemento = document.getElementById("modalConfirmarRegistroVozLogin");
                if (modalElemento) {
                    const modal = bootstrap.Modal.getOrCreateInstance(modalElemento);
                    modal.show();
                }
            }

            function rechazarRegistroVozLogin() {
                liberarRecursosVoz();
                audioBlobLogin = null;
                audioChunksLogin = [];
                fraseRegistroVozLogin = "";

                const modalElemento = document.getElementById("modalConfirmarRegistroVozLogin");
                if (modalElemento) {
                    const modal = bootstrap.Modal.getOrCreateInstance(modalElemento);
                    modal.hide();
                }

                const estado = document.getElementById("estadoVozLogin");
                const boton = document.getElementById("btnGrabarVozLogin");

                if (estado) {
                    estado.innerText = "⚪ Grabación descartada. Pulsa «Grabar mi voz» para intentarlo nuevamente.";
                }
                if (boton) {
                    boton.disabled = false;
                    boton.classList.remove("grabando", "btn-danger");
                    boton.classList.add("btn-primary");
                    boton.innerHTML = modoRenovacionVoz ? "🎙️ Grabar mi nueva voz" : "🎙️ Grabar mi voz";
                }
            }

            let enviandoConfirmacionVozLogin = false;
            async function confirmarRegistroVozLogin() {
                if (enviandoConfirmacionVozLogin) return;
                enviandoConfirmacionVozLogin = true;

                const btnModal = document.getElementById("btnConfirmarRegistroVozModal");
                if (btnModal) {
                    btnModal.disabled = true;
                    btnModal.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Confirmando...';
                }

                try {
                    const modalElemento = document.getElementById("modalConfirmarRegistroVozLogin");
                    if (modalElemento) {
                        const modal = bootstrap.Modal.getOrCreateInstance(modalElemento);
                        modal.hide();
                    }

                    const estado = document.getElementById("estadoVozLogin");
                    if (estado) {
                        estado.innerText = "⏳ Guardando tu voz de seguridad...";
                    }

                    await enviarVozLogin();
                } finally {
                    enviandoConfirmacionVozLogin = false;
                    if (btnModal) {
                        btnModal.disabled = false;
                        btnModal.innerHTML = '<i class="bi bi-check-circle-fill"></i> Confirmar voz';
                    }
                }
            }

            async function enviarVozLogin() {
                const boton = document.getElementById("btnGrabarVozLogin");
                const estado = document.getElementById("estadoVozLogin");

                if (!audioBlobLogin || audioBlobLogin.size === 0) {
                    if (estado) estado.innerText = "❌ No se obtuvo la grabación.";
                    return;
                }

                let fraseAEnviar = (fraseRegistroVozLogin || "").trim();
                if (!fraseAEnviar) {
                    fraseAEnviar = "Mi voz protege mi cuenta";
                }

                const ext = obtenerExtensionAudio(audioBlobLogin.type);
                const formData = new FormData();
                formData.append("audio", audioBlobLogin, "audioLogin." + ext);
                formData.append("fraseVoz", fraseAEnviar);

                if (modoRenovacionVoz) {
                    if (!tokenRenovacionVozActivo) {
                        if (estado) estado.innerText = "❌ No se encontró una autorización válida para actualizar la biometría.";
                        return;
                    }
                    formData.append("tokenRenovacion", tokenRenovacionVozActivo);
                }

                try {
                    if (boton) {
                        boton.disabled = true;
                        boton.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Guardando...';
                    }

                    const endpoint = modoRenovacionVoz ? "/Login/RenovarVozLogin" : "/Login/RegistrarVozLogin";
                    const respuesta = await fetch(endpoint, {
                        method: "POST",
                        body: formData
                    });

                    const resultado = await respuesta.json();

                    if (!resultado.ok) {
                        if (estado) estado.innerText = "❌ " + resultado.mensaje;
                        if (boton) {
                            boton.disabled = false;
                            boton.innerHTML = modoRenovacionVoz ? "🎙️ Grabar mi nueva voz" : "🎙️ Intentar nuevamente";
                        }
                        return;
                    }

                    if (modoRenovacionVoz) {
                        // Actualizar estado en memoria
                        tieneRegistroVozLogin = true;
                        vozHabilitadaLogin = true;
                        if (resultado.nuevaFrase) {
                            fraseVozBD = resultado.nuevaFrase;
                            document.querySelectorAll(".textoFraseEsperadaVoz, #textoFraseEsperadaRegistroVoz, #textoFraseEsperadaVoz").forEach(el => {
                                el.innerText = fraseVozBD;
                            });
                        }

                        if (estado) estado.innerText = "✅ Biometría actualizada correctamente.";
                        if (boton) {
                            boton.innerHTML = "✅ Biometría guardada";
                            boton.disabled = true;
                        }

                        const btnCancelarForm = document.getElementById("btnCancelarRenovacionFormulario");
                        if (btnCancelarForm) btnCancelarForm.style.display = "none";

                        // Notificación de éxito requerida
                        if (typeof Swal !== "undefined") {
                            Swal.fire({
                                icon: "success",
                                title: "✅ Biometría actualizada correctamente",
                                text: "Tu nueva biometría de voz y palabra secreta se guardaron correctamente. Ya puedes utilizarlas para verificar tu identidad.",
                                confirmButtonColor: "#3c096c"
                            });
                        } else {
                            alert("✅ Biometría actualizada correctamente\nTu nueva biometría de voz y palabra secreta se guardaron correctamente. Ya puedes utilizarlas para verificar tu identidad.");
                        }

                        // Limpiar modo renovación
                        modoRenovacionVoz = false;
                        tokenRenovacionVozActivo = null;

                        // Regresar al modal de selección de métodos de seguridad
                        setTimeout(function() {
                            restaurarModalMetodosSeguridadTrasRenovacion();
                        }, 1200);
                        return;
                    }

                    // Flujo de registro inicial
                    if (estado) estado.innerText = "🟢 Voz registrada correctamente.";
                    if (boton) {
                        boton.innerHTML = "✅ Voz registrada";
                        boton.disabled = true;
                    }

                    setTimeout(function() {
                        const bloqueRegistroVoz = document.getElementById("bloqueRegistroVoz");
                        const opcionCorreo = document.getElementById("opcionCorreo");
                        const opcionAplicacion = document.getElementById("opcionAplicacion");
                        const opcionVoz = document.getElementById("opcionVoz");
                        const botonContinuar = document.querySelector(".btn-continuar-seguridad");

                        if (bloqueRegistroVoz) bloqueRegistroVoz.style.display = "none";
                        if (opcionCorreo) opcionCorreo.style.display = "flex";
                        if (opcionAplicacion) opcionAplicacion.style.display = "flex";
                        if (opcionVoz) opcionVoz.style.display = "flex";
                        if (botonContinuar) botonContinuar.style.display = "inline-block";

                        const titulo = document.querySelector("#modalVerificacionSeguridad .modal-title");
                        if (titulo) titulo.innerText = "Verificación de seguridad";

                        const texto = document.querySelector("#modalVerificacionSeguridad .texto-seguridad");
                        if (texto) texto.innerText = "Selecciona cómo deseas recibir tu código de seguridad.";
                    }, 1200);

                } catch (error) {
                    console.error(error);
                    if (estado) estado.innerText = "❌ No se pudo guardar la grabación.";
                    if (boton) {
                        boton.disabled = false;
                        boton.innerHTML = "🎙️ Intentar nuevamente";
                    }
                }
            }

            // ==========================================
            // VERIFICACIÓN DE VOZ
            // ==========================================

            async function iniciarVerificacionVoz() {
                const boton = document.getElementById("btnVerificarVoz");
                const estado = document.getElementById("estadoVerificacionVoz");

                if (!boton || !estado) return;

                // Si ya está grabando, permitir finalizar y verificar
                if (grabandoVerificacionVoz) {
                    detenerVerificacionVoz();
                    return;
                }

                if (procesandoVoz) return;

                // Comprobar HTTPS y soporte de micrófono
                if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                    if (window.isSecureContext === false) {
                        estado.innerText = "❌ El micrófono requiere una conexión segura (HTTPS).";
                    } else {
                        estado.innerText = "❌ Tu navegador no permite utilizar el micrófono. Verifica la configuración.";
                    }
                    return;
                }

                try {
                    estado.innerText = "⏳ Solicitando acceso al micrófono...";

                    const stream = await navigator.mediaDevices.getUserMedia({
                        audio: {
                            echoCancellation: true,
                            noiseSuppression: true,
                            autoGainControl: true
                        }
                    });

                    streamActualVerificacion = stream;

                    verificacionCancelada = false;
                    fraseReconocida = "";
                    audioChunksVerificacion = [];
                    procesandoVoz = false;

                    const mimeType = obtenerMimeTypeSoportado();
                    const opciones = mimeType ? { mimeType: mimeType } : undefined;

                    mediaRecorderVerificacion = opciones ? new MediaRecorder(stream, opciones) : new MediaRecorder(stream);

                    mediaRecorderVerificacion.ondataavailable = function(event) {
                        if (event.data && event.data.size > 0) {
                            audioChunksVerificacion.push(event.data);
                        }
                    };

                    mediaRecorderVerificacion.onstop = async function() {
                        console.log("🎙️ Grabación de verificación terminada.");

                        if (streamActualVerificacion) {
                            try {
                                streamActualVerificacion.getTracks().forEach(track => track.stop());
                            } catch (e) {}
                            streamActualVerificacion = null;
                        }

                        if (verificacionCancelada) {
                            console.log("🛑 Grabación cancelada: no se envía.");
                            audioChunksVerificacion = [];
                            return;
                        }

                        const tipoFinal = (mediaRecorderVerificacion && mediaRecorderVerificacion.mimeType) || mimeType || "audio/webm";
                        const audio = new Blob(audioChunksVerificacion, { type: tipoFinal });
                        audioChunksVerificacion = [];

                        console.log("🎙️ Audio creado:", audio.size, "bytes");
                        estado.innerText = "⏳ Procesando grabación...";

                        await enviarVerificacionVoz(audio);
                    };

                    // NOTA: NO ejecutar SpeechRecognition simultáneamente con MediaRecorder.
                    // En Android Chrome, SpeechRecognition toma bloqueo exclusivo del hardware de audio,
                    // silenciando por completo la pista de MediaRecorder (lo que provocaba similitud 0.3193).
                    // La autenticación biométrica es 100% realizada por el modelo ECAPA-TDNN en el servidor.

                    mediaRecorderVerificacion.start(250);
                    grabandoVerificacionVoz = true;

                    boton.disabled = false;
                    boton.classList.remove("btn-success");
                    boton.classList.add("grabando", "btn-danger");
                    boton.innerHTML = "⏹️ Detener y verificar";

                    estado.innerText = "🔴 Grabando... Di tu palabra o frase clave registrada";

                    temporizadorVoz = setTimeout(function() {
                        if (grabandoVerificacionVoz && !procesandoVoz) {
                            console.log("⏰ Tiempo de escucha completado.");
                            detenerVerificacionVoz();
                        }
                    }, 4500);

                } catch (error) {
                    console.error("Error al iniciar verificación:", error);
                    liberarRecursosVoz();

                    if (boton) {
                        boton.disabled = false;
                        boton.classList.remove("grabando", "btn-danger");
                        boton.classList.add("btn-success");
                        boton.innerHTML = "🎙️ Hablar palabra clave";
                    }

                    if (error.name === "NotAllowedError" || error.name === "PermissionDeniedError") {
                        estado.innerText = "❌ No se pudo acceder al micrófono. Verifica los permisos del navegador y vuelve a intentarlo.";
                    } else if (error.name === "NotFoundError" || error.name === "DevicesNotFoundError") {
                        estado.innerText = "❌ No se encontró ningún micrófono en este dispositivo.";
                    } else {
                        estado.innerText = "❌ No se pudo acceder al micrófono. Verifica los permisos.";
                    }
                }
            }

            function detenerVerificacionVoz() {
                if (!grabandoVerificacionVoz) return;
                procesandoVoz = true;

                if (temporizadorVoz) {
                    clearTimeout(temporizadorVoz);
                    temporizadorVoz = null;
                }

                if (reconocimientoVoz) {
                    try { reconocimientoVoz.stop(); } catch (e) {}
                }

                if (mediaRecorderVerificacion && mediaRecorderVerificacion.state !== "inactive") {
                    try { mediaRecorderVerificacion.stop(); } catch (e) {}
                }
            }

            function detenerGrabacionSinEnviar() {
                liberarRecursosVoz();
            }

            async function enviarVerificacionVoz(audioBlob) {
                const boton = document.getElementById("btnVerificarVoz");
                const estado = document.getElementById("estadoVerificacionVoz");

                if (!audioBlob || audioBlob.size < 2000) {
                    if (estado) estado.innerText = "❌ No se detectó audio del micrófono. Habla claramente e inténtalo nuevamente.";
                    if (boton) {
                        boton.disabled = false;
                        boton.classList.remove("grabando", "btn-danger");
                        boton.classList.add("btn-success");
                        boton.innerHTML = "🎙️ Hablar palabra clave";
                    }
                    grabandoVerificacionVoz = false;
                    procesandoVoz = false;
                    return;
                }

                if (estado) estado.innerText = "✅ Grabación capturada. Verificando identidad y palabra clave...";

                const ext = obtenerExtensionAudio(audioBlob.type);
                const formData = new FormData();
                formData.append("audio", audioBlob, "verificacionVoz." + ext);

                try {
                    if (boton) {
                        boton.disabled = true;
                        boton.classList.remove("grabando", "btn-danger");
                        boton.classList.add("btn-success");
                        boton.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Verificando...';
                    }

                    const respuesta = await fetch("/Login/VerificarVozLogin", {
                        method: "POST",
                        body: formData
                    });

                    const resultado = await respuesta.json();

                    if (!resultado.ok) {
                        if (resultado.bloqueadoVoz) {
                            grabandoVerificacionVoz = false;
                            procesandoVoz = false;
                            if (estado) estado.innerText = "🔒 " + resultado.mensaje;
                            if (boton) {
                                boton.disabled = true;
                                boton.innerHTML = "🔒 Voz bloqueada";
                            }
                            setTimeout(function() {
                                restaurarModalSeguridad();
                                bloquearOpcionVoz(resultado.segundosRestantes || 300);
                            }, 1200);
                            return;
                        }

                        if (resultado.esErrorAudio) {
                            if (estado) estado.innerText = "⚠️ " + resultado.mensaje;
                            if (boton) {
                                boton.disabled = false;
                                boton.classList.remove("grabando", "btn-danger");
                                boton.classList.add("btn-success");
                                boton.innerHTML = "🎙️ Hablar palabra clave";
                            }
                            grabandoVerificacionVoz = false;
                            procesandoVoz = false;
                            return;
                        }

                        if (estado) {
                            estado.innerText = "❌ " + (resultado.mensaje || "No se pudo verificar la voz.");
                        }
                        if (boton) {
                            boton.disabled = false;
                            boton.innerHTML = "🎙️ Intentar nuevamente";
                        }
                        grabandoVerificacionVoz = false;
                        procesandoVoz = false;
                        return;
                    }

                    if (estado) estado.innerText = "🟢 Voz y palabra clave verificadas correctamente. Ingresando...";
                    if (boton) {
                        boton.innerHTML = "✅ Verificación correcta";
                        boton.disabled = true;
                    }

                    if (resultado.redirectUrl) {
                        setTimeout(function() {
                            window.location.href = resultado.redirectUrl;
                        }, 500);
                    } else {
                        if (estado) estado.innerText = "❌ Inicio de sesión correcto, pero no se recibió la dirección de destino.";
                        if (boton) boton.disabled = false;
                    }

                } catch (error) {
                    console.error("ERROR VERIFICANDO VOZ:", error);
                    if (estado) estado.innerText = "❌ No se pudo verificar la voz.";
                    if (boton) {
                        boton.disabled = false;
                        boton.classList.remove("grabando", "btn-danger");
                        boton.classList.add("btn-success");
                        boton.innerHTML = "🎙️ Intentar nuevamente";
                    }
                    grabandoVerificacionVoz = false;
                    procesandoVoz = false;
                }
            }

            // ==========================================
            // CAMBIAR MODALES SIN SUPERPONERLOS
            // ==========================================

            function abrirModalCodigo() {
                const modalSeguridadElemento = document.getElementById("modalVerificacionSeguridad");
                const modalCodigoElemento = document.getElementById("modalCodigoSeguridad");

                if (!modalSeguridadElemento || !modalCodigoElemento) return;

                const modalSeguridad = bootstrap.Modal.getOrCreateInstance(modalSeguridadElemento);
                const modalCodigo = bootstrap.Modal.getOrCreateInstance(modalCodigoElemento);

                modalSeguridadElemento.addEventListener("hidden.bs.modal", function abrirCodigoUnaVez() {
                    modalSeguridadElemento.removeEventListener("hidden.bs.modal", abrirCodigoUnaVez);
                    modalCodigo.show();
                });

                modalSeguridad.hide();
            }

            // ==========================================
            // RESTAURAR MODAL DE SEGURIDAD A SU ESTADO INICIAL
            // ==========================================

            function restaurarModalSeguridad() {
                liberarRecursosVoz();

                const modal = document.getElementById("modalVerificacionSeguridad");
                if (!modal) return;

                const bloqueRegistroVoz = document.getElementById("bloqueRegistroVoz");
                const bloqueVerificacionVoz = document.getElementById("bloqueVerificacionVoz");
                const opcionCorreo = document.getElementById("opcionCorreo");
                const opcionAplicacion = document.getElementById("opcionAplicacion");
                const opcionVoz = document.getElementById("opcionVoz");
                const botonContinuar = modal.querySelector(".btn-continuar-seguridad");
                const titulo = modal.querySelector(".modal-title");
                const texto = modal.querySelector(".texto-seguridad");

                metodoVerificacion = "correo";

                if (opcionCorreo) {
                    opcionCorreo.style.display = "flex";
                    opcionCorreo.classList.add("seleccionada");
                }
                if (opcionAplicacion) {
                    opcionAplicacion.style.display = "flex";
                    opcionAplicacion.classList.remove("seleccionada");
                }
                if (opcionVoz) {
                    opcionVoz.style.display = "flex";
                    opcionVoz.classList.remove("seleccionada");
                }

                const radioCorreo = document.getElementById("radioCorreo");
                const radioAplicacion = document.getElementById("radioAplicacion");
                const radioVoz = document.getElementById("radioVoz");

                if (radioCorreo) radioCorreo.style.display = "block";
                if (radioAplicacion) radioAplicacion.style.display = "none";
                if (radioVoz) radioVoz.style.display = "none";

                if (bloqueRegistroVoz) bloqueRegistroVoz.style.display = "none";
                if (bloqueVerificacionVoz) bloqueVerificacionVoz.style.display = "none";

                const contenedorCambiar = document.getElementById("contenedorCambiarBiometria");
                if (contenedorCambiar) contenedorCambiar.style.display = "none";

                const btnCancelarForm = document.getElementById("btnCancelarRenovacionFormulario");
                if (btnCancelarForm) btnCancelarForm.style.display = "none";

                if (titulo) titulo.innerText = "Verificación de seguridad";
                if (texto) texto.innerText = "Para continuar, selecciona cómo deseas recibir tu código de seguridad.";

                if (botonContinuar) {
                    botonContinuar.style.display = "inline-block";
                    botonContinuar.disabled = false;
                    botonContinuar.innerHTML = 'Continuar <i class="bi bi-arrow-right ms-1"></i>';
                }
            }

            // ==========================================
            // AL CERRAR LOS MODALES, LIBERAR RECURSOS
            // ==========================================

            document.addEventListener("DOMContentLoaded", function() {
                const modalSeguridad = document.getElementById("modalVerificacionSeguridad");
                if (modalSeguridad) {
                    modalSeguridad.addEventListener("hidden.bs.modal", function() {
                        liberarRecursosVoz();
                        restaurarModalSeguridad();
                    });
                }

                const modalConfirmar = document.getElementById("modalConfirmarRegistroVozLogin");
                if (modalConfirmar) {
                    modalConfirmar.addEventListener("hidden.bs.modal", function() {
                        liberarRecursosVoz();
                    });
                }

                // Configurar eventos para casillas OTP de renovación
                configurarCasillasOtpRenovacion();
            });

            // ==========================================
            // BLOQUEAR OPCIÓN DE VOZ
            // ==========================================

            let intervaloBloqueoVoz = null;


            function bloquearOpcionVoz(
                segundos
            ) {

                const opcionVoz =
                    document.getElementById(
                        "opcionVoz"
                    );

                if (!opcionVoz) {
                    return;
                }


                // Guardamos el onclick original
                opcionVoz.onclick = null;

                opcionVoz.style.opacity =
                    "0.55";

                opcionVoz.style.cursor =
                    "not-allowed";

                opcionVoz.style.pointerEvents =
                    "none";


                // ==========================================
                // BUSCAR TEXTO DE LA OPCIÓN
                // ==========================================

                const descripcion =
                    opcionVoz.querySelector(
                        ".opcion-contenido p"
                    );


                if (intervaloBloqueoVoz) {

                    clearInterval(
                        intervaloBloqueoVoz
                    );
                }


                let segundosRestantes =
                    segundos;


                function actualizarBloqueo() {

                    const minutos =
                        Math.floor(
                            segundosRestantes /
                            60
                        );

                    const segundosVista =
                        segundosRestantes %
                        60;


                    const tiempo =
                        String(minutos)
                            .padStart(2, "0") +
                        ":" +
                        String(segundosVista)
                            .padStart(2, "0");


                    if (descripcion) {

                        descripcion.innerText =
                            "🔒 Voz bloqueada. Disponible nuevamente en " +
                            tiempo;
                    }


                    // ==========================================
                    // TERMINÓ BLOQUEO
                    // ==========================================

                    if (
                        segundosRestantes <= 0
                    ) {

                        clearInterval(
                            intervaloBloqueoVoz
                        );

                        intervaloBloqueoVoz =
                            null;


                        opcionVoz.style.opacity =
                            "1";

                        opcionVoz.style.cursor =
                            "pointer";

                        opcionVoz.style.pointerEvents =
                            "auto";


                        opcionVoz.onclick =
                            function() {

                                seleccionarMetodo(
                                    "voz"
                                );

                            };


                        if (descripcion) {

                            descripcion.innerText =
                                "Habla tu palabra o frase de seguridad registrada.";
                        }


                        return;
                    }


                    segundosRestantes--;
                }


                actualizarBloqueo();


                intervaloBloqueoVoz =
                    setInterval(
                        actualizarBloqueo,
                        1000
                    );
            }
            function usarCorreoParaActivarVoz() {

                const modalElemento =
                    document.getElementById(
                        "modalVozNoHabilitada"
                    );

                if (!modalElemento) {
                    return;
                }


                const modal =
                    bootstrap.Modal
                        .getOrCreateInstance(
                            modalElemento
                        );


                modalElemento.addEventListener(
                    "hidden.bs.modal",
                    function cambiarACorreo() {

                        modalElemento.removeEventListener(
                            "hidden.bs.modal",
                            cambiarACorreo
                        );

                        seleccionarMetodo(
                            "correo"
                        );

                    }
                );


                modal.hide();
            }
                            let qrDescargaAuthenticatorCreado = false;

        function continuarConfiguracionAuthenticator() {

            const modalExitoElemento =
                document.getElementById(
                    "modalIdentidadConfirmadaAuthenticator"
                );

            const modalExito =
                bootstrap.Modal.getInstance(
                    modalExitoElemento
                );

            if (modalExito) {
                modalExito.hide();
            }


            modalExitoElemento.addEventListener(
                "hidden.bs.modal",
                function abrirDescarga() {

                    modalExitoElemento.removeEventListener(
                        "hidden.bs.modal",
                        abrirDescarga
                    );


                    const modalDescargaElemento =
                        document.getElementById(
                            "modalDescargaAuthenticator"
                        );


                    const modalDescarga =
                        bootstrap.Modal.getOrCreateInstance(
                            modalDescargaElemento
                        );


                    modalDescarga.show();


                    setTimeout(function () {

                        generarQrDescargaAuthenticator();

                    }, 250);
                }
            );
        }
                function generarQrDescargaAuthenticator() {

            if (qrDescargaAuthenticatorCreado) {
                return;
            }

            const contenedor =
                document.getElementById(
                    "qrDescargaAuthenticator"
                );

            if (!contenedor) {
                return;
            }

            contenedor.innerHTML = "";

            let origenDescarga = window.location.origin;

            // =====================================
            // LOCALHOST
            // =====================================

            if (
                window.location.hostname === "localhost" ||
                window.location.hostname === "127.0.0.1"
            ) {
                origenDescarga =
                    "http://192.168.18.127:5280";
            }

            // =====================================
            // URL DIRECTA DEL APK
            // =====================================

            const urlApk =
                origenDescarga +
                "/descargas/CrediPlusAuthenticator.apk";

            console.log(
                "URL APK QR:",
                urlApk
            );

            new QRCode(
                contenedor,
                {
                    text: urlApk,

                    width: 190,
                    height: 190,

                    colorDark: "#172554",
                    colorLight: "#ffffff",

                    correctLevel:
                        QRCode.CorrectLevel.H
                }
            );

            qrDescargaAuthenticatorCreado = true;
        }
                function continuarDespuesDeInstalarAuthenticator() {

            const modal =
                bootstrap.Modal.getInstance(
                    document.getElementById(
                        "modalDescargaAuthenticator"
                    )
                );

            if (modal) {
                modal.hide();
            }


            console.log(
                "Aplicación instalada. Continuar con vinculación."
            );

        }
                               function iniciarProcesoInstalacionAuthenticator() {

            const elementoModalPrincipal =
                document.getElementById(
                    "modalDescargaAuthenticator"
                );

            const modalPrincipal =
                bootstrap.Modal.getInstance(
                    elementoModalPrincipal
                );

            if (modalPrincipal) {
                modalPrincipal.hide();
            }


            elementoModalPrincipal.addEventListener(
                "hidden.bs.modal",
                function abrirGuiaInstalacion() {

                    elementoModalPrincipal.removeEventListener(
                        "hidden.bs.modal",
                        abrirGuiaInstalacion
                    );

                    const modalGuia =
                        bootstrap.Modal.getOrCreateInstance(
                            document.getElementById(
                                "modalGuiaInstalacionAuthenticator"
                            )
                        );

                    modalGuia.show();
                }
            );
        }
                      let pasoActualInstalacionAuthenticator = 1;


        function mostrarPasoInstalacionAuthenticator(numeroPaso) {

            pasoActualInstalacionAuthenticator = numeroPaso;


            // =====================================
            // MOSTRAR SOLAMENTE EL PASO ACTUAL
            // =====================================

            for (let i = 1; i <= 8; i++) {

                const paso =
                    document.getElementById(
                        "guiaInstalacionPaso" + i
                    );

                if (paso) {

                    paso.style.display =
                        i === numeroPaso
                            ? "block"
                            : "none";

                }
            }


            // =====================================
            // INDICADORES
            // =====================================

            const indicadores =
                document.querySelectorAll(
                    ".guia-indicadores-instalacion .guia-punto"
                );

            indicadores.forEach(
                function (indicador, indice) {

                    indicador.classList.toggle(
                        "activo",
                        indice === numeroPaso - 1
                    );

                }
            );


            // =====================================
            // BOTÓN ANTERIOR
            // =====================================

            const botonAnterior =
                document.getElementById(
                    "btnGuiaInstalacionAnterior"
                );

            if (botonAnterior) {

                botonAnterior.disabled =
                    numeroPaso === 1;

            }


            // =====================================
            // BOTÓN SIGUIENTE
            // =====================================

            const botonSiguiente =
                document.getElementById(
                    "btnGuiaInstalacionSiguiente"
                );

            if (botonSiguiente) {

                if (numeroPaso === 8) {

                    botonSiguiente.innerHTML =
                        'Continuar con la configuración ' +
                        '<i class="bi bi-arrow-right ms-1"></i>';

                }
                else {

                    botonSiguiente.innerHTML =
                        'Siguiente ' +
                        '<i class="bi bi-arrow-right ms-1"></i>';

                }
            }
        }


        function siguienteGuiaInstalacionAuthenticator() {

            // =====================================
            // PASOS 1 AL 7
            // =====================================

            if (
                pasoActualInstalacionAuthenticator < 8
            ) {

                mostrarPasoInstalacionAuthenticator(
                    pasoActualInstalacionAuthenticator + 1
                );

                return;
            }


            // =====================================
            // PASO 8 TERMINADO
            // =====================================

            continuarConfiguracionCuentaAuthenticator();
        }


        function anteriorGuiaInstalacionAuthenticator() {

            if (
                pasoActualInstalacionAuthenticator > 1
            ) {

                mostrarPasoInstalacionAuthenticator(
                    pasoActualInstalacionAuthenticator - 1
                );

            }
        }
        function continuarConfiguracionCuentaAuthenticator() {

            const elementoGuia =
                document.getElementById(
                    "modalGuiaInstalacionAuthenticator"
                );

            const modalGuia =
                bootstrap.Modal.getInstance(
                    elementoGuia
                );

            if (modalGuia) {
                modalGuia.hide();
            }


            elementoGuia.addEventListener(
                "hidden.bs.modal",
                function abrirConfiguracionTotp() {

                    elementoGuia.removeEventListener(
                        "hidden.bs.modal",
                        abrirConfiguracionTotp
                    );


                    const modalTotp =
                        bootstrap.Modal.getOrCreateInstance(
                            document.getElementById(
                                "modalConfigurarTotpAuthenticator"
                            )
                        );

                    modalTotp.show();


                    prepararConfiguracionTotp();

                }
            );
        }
                async function prepararConfiguracionTotp() {

            const correo =
                document.getElementById(
                    "correoConfiguracionTotp"
                );

            const clave =
                document.getElementById(
                    "claveConfiguracionTotp"
                );


            correo.innerText = "Cargando...";
            clave.innerText = "Cargando...";


            try {

                const respuesta =
                    await fetch(
                        "/Login/PrepararConfiguracionTotp",
                        {
                            method: "POST"
                        }
                    );


                const datos =
                    await respuesta.json();


                if (!datos.ok) {

                    mostrarMensajeTotp(
                        datos.mensaje ||
                        "No se pudo preparar la configuración.",
                        "danger"
                    );

                    return;
                }


                correo.innerText =
                    datos.correo;

                clave.innerText =
                    datos.clave;
                            generarQrConfiguracionTotp(
            datos.correo,
            datos.clave
        );

            }
            catch {

                mostrarMensajeTotp(
                    "No fue posible conectar con el servidor.",
                    "danger"
                );

            }
        }
                function generarQrConfiguracionTotp(
            correo,
            clave
        ) {

            const contenedor =
                document.getElementById(
                    "qrConfiguracionTotp"
                );

            if (!contenedor) {
                return;
            }


            // Limpiar QR anterior
            contenedor.innerHTML = "";


            // La clave viene visualmente con espacios.
            // Para TOTP debemos quitar esos espacios.
            const secreto =
                clave.replace(/\s/g, "");


            const issuer =
                "CrediPlus";


            const etiqueta =
                encodeURIComponent(
                    issuer + ":" + correo
                );


            const uriTotp =
                "otpauth://totp/" +
                etiqueta +
                "?secret=" +
                encodeURIComponent(secreto) +
                "&issuer=" +
                encodeURIComponent(issuer) +
                "&algorithm=SHA1" +
                "&digits=6" +
                "&period=30";


            new QRCode(
                contenedor,
                {
                    text: uriTotp,
                    width: 164,
                    height: 164,
                    colorDark: "#172554",
                    colorLight: "#ffffff",
                    correctLevel:
                        QRCode.CorrectLevel.H
                }
            );
        }

        function copiarCorreoTotp() {

            const correo =
                document.getElementById(
                    "correoConfiguracionTotp"
                ).innerText;

            navigator.clipboard.writeText(
                correo
            );
        }


        function copiarClaveTotp() {

            const clave =
                document.getElementById(
                    "claveConfiguracionTotp"
                ).innerText;

            navigator.clipboard.writeText(
                clave.replace(/\s/g, "")
            );
        }


        async function verificarConfiguracionTotp() {

            const codigo =
                document.getElementById(
                    "codigoVerificacionTotp"
                ).value.trim();


            if (!/^\d{6}$/.test(codigo)) {

                mostrarMensajeTotp(
                    "Ingresa el código de 6 dígitos generado por la aplicación.",
                    "warning"
                );

                return;
            }


            try {

                const respuesta =
                    await fetch(
                        "/Login/ConfirmarConfiguracionTotp",
                        {
                            method: "POST",

                            headers: {
                                "Content-Type":
                                    "application/x-www-form-urlencoded"
                            },

                            body:
                                "codigo=" +
                                encodeURIComponent(codigo)
                        }
                    );


                const datos =
                    await respuesta.json();


                if (!datos.ok) {

                    mostrarMensajeTotp(
                        datos.mensaje ||
                        "El código ingresado no es correcto.",
                        "danger"
                    );

                    return;
                }

        mostrarMensajeTotp(
            "CrediPlus Authenticator fue vinculado correctamente.",
            "success"
        );

        const botonVerificar =
            document.getElementById(
                "btnVerificarTotp"
            );

        if (botonVerificar) {
            botonVerificar.disabled = true;
        }

        setTimeout(function () {

            const elementoModal =
                document.getElementById(
                    "modalConfigurarTotpAuthenticator"
                );

            const modal =
                bootstrap.Modal.getInstance(
                    elementoModal
                );

            if (modal) {
                modal.hide();
            }

            elementoModal.addEventListener(
                "hidden.bs.modal",
                function volverInicioSesion() {

                    elementoModal.removeEventListener(
                        "hidden.bs.modal",
                        volverInicioSesion
                    );

                    window.location.href =
                        "/Login/IniciarSesion";
                }
            );

        }, 1400);

            }
            catch {

                mostrarMensajeTotp(
                    "No fue posible verificar el código.",
                    "danger"
                );

            }
        }


        function mostrarMensajeTotp(
            mensaje,
            tipo
        ) {

            const contenedor =
                document.getElementById(
                    "mensajeConfiguracionTotp"
                );

            contenedor.style.display =
                "block";

            contenedor.className =
                "alert alert-" +
                tipo +
                " mt-3";

            contenedor.innerText =
                mensaje;
        }


        function volverGuiaInstalacion() {

            const elementoTotp =
                document.getElementById(
                    "modalConfigurarTotpAuthenticator"
                );

            const modalTotp =
                bootstrap.Modal.getInstance(
                    elementoTotp
                );

            if (modalTotp) {
                modalTotp.hide();
            }


            elementoTotp.addEventListener(
                "hidden.bs.modal",
                function volverGuia() {

                    elementoTotp.removeEventListener(
                        "hidden.bs.modal",
                        volverGuia
                    );

                    const modalGuia =
                        bootstrap.Modal.getOrCreateInstance(
                            document.getElementById(
                                "modalGuiaInstalacionAuthenticator"
                            )
                        );

                    mostrarPasoInstalacionAuthenticator(8);

                    modalGuia.show();

                }
            );
        }
        let pasoActualGuiaVinculacion = 1;


        function abrirGuiaVinculacionTotp() {

            pasoActualGuiaVinculacion = 1;

            mostrarPasoGuiaVinculacion(1);


            const modal =
                bootstrap.Modal.getOrCreateInstance(
                    document.getElementById(
                        "modalGuiaVinculacionTotp"
                    )
                );

            modal.show();
        }


        function mostrarPasoGuiaVinculacion(numeroPaso) {

            pasoActualGuiaVinculacion =
                numeroPaso;


            // MOSTRAR SOLO EL PASO ACTUAL

            for (let i = 1; i <= 4; i++) {

                const paso =
                    document.getElementById(
                        "guiaVinculacionPaso" + i
                    );

                if (paso) {

                    paso.style.display =
                        i === numeroPaso
                            ? "block"
                            : "none";
                }
            }


            // INDICADORES

            const indicadores =
                document.querySelectorAll(
                    ".guia-vinculacion-punto"
                );

            indicadores.forEach(
                function (indicador, indice) {

                    indicador.classList.toggle(
                        "activo",
                        indice === numeroPaso - 1
                    );
                }
            );


            // BOTÓN ANTERIOR

            const btnAnterior =
                document.getElementById(
                    "btnGuiaVinculacionAnterior"
                );

            if (btnAnterior) {

                btnAnterior.disabled =
                    numeroPaso === 1;
            }


            // BOTÓN SIGUIENTE

            const btnSiguiente =
                document.getElementById(
                    "btnGuiaVinculacionSiguiente"
                );

            if (btnSiguiente) {

                if (numeroPaso === 4) {

                    btnSiguiente.innerHTML =
                        'Entendido ' +
                        '<i class="bi bi-check-lg ms-1"></i>';

                }
                else {

                    btnSiguiente.innerHTML =
                        'Siguiente ' +
                        '<i class="bi bi-arrow-right ms-1"></i>';
                }
            }
        }


        function siguienteGuiaVinculacionTotp() {

            if (
                pasoActualGuiaVinculacion < 4
            ) {

                mostrarPasoGuiaVinculacion(
                    pasoActualGuiaVinculacion + 1
                );

                return;
            }


            const modal =
                bootstrap.Modal.getInstance(
                    document.getElementById(
                        "modalGuiaVinculacionTotp"
                    )
                );

            if (modal) {
                modal.hide();
            }
        }


        function anteriorGuiaVinculacionTotp() {

            if (
                pasoActualGuiaVinculacion > 1
            ) {

                mostrarPasoGuiaVinculacion(
                    pasoActualGuiaVinculacion - 1
                );
            }
        }
        async function esperarRespuestaAuthenticator(
            solicitudId
        ) {

            const inicio = Date.now();

            const tiempoMaximo = 30000;

            while (
                Date.now() - inicio <
                tiempoMaximo
            ) {

                await new Promise(
                    resolve =>
                        setTimeout(
                            resolve,
                            1000
                        )
                );

                /*
                 * Si el usuario ya canceló desde
                 * la propia página, dejamos de consultar.
                 */
                if (
                    solicitudAuthenticatorTerminada
                ) {
                    return;
                }

                try {

                    const respuesta =
                        await fetch(
                            "/Login/ConsultarEstadoSolicitudAuthenticator" +
                            "?solicitudId=" +
                            encodeURIComponent(
                                solicitudId
                            ),
                            {
                                cache: "no-store"
                            }
                        );

                    const datos =
                        await respuesta.json();

                    if (!datos.ok) {

                        solicitudAuthenticatorTerminada =
                            true;

                        if (
                            intervaloSolicitudAuthenticator
                        ) {

                            clearInterval(
                                intervaloSolicitudAuthenticator
                            );

                            intervaloSolicitudAuthenticator =
                                null;
                        }

                        mostrarModalAuthenticator(
                            "No se pudo verificar",
                            datos.mensaje ||
                                "No se pudo consultar la solicitud.",
                            null,
                            "error"
                        );

                        return;
                    }

                    // ==============================
                    // CANCELADO / RECHAZADO
                    // ==============================

                    if (
                        datos.estado ===
                        "Rechazado"
                    ) {

                        solicitudAuthenticatorTerminada =
                            true;

                        if (
                            intervaloSolicitudAuthenticator
                        ) {

                            clearInterval(
                                intervaloSolicitudAuthenticator
                            );

                            intervaloSolicitudAuthenticator =
                                null;
                        }

                        mostrarModalAuthenticator(
                            "Solicitud cancelada",
                            "El inicio de sesión fue cancelado desde CrediPlus Authenticator.",
                            null,
                            "error"
                        );

                        return;
                    }
                            // ==============================
        // NÚMERO INCORRECTO
        // ==============================

        if (
            datos.estado ===
            "Incorrecto"
        ) {

            solicitudAuthenticatorTerminada =
                true;

            if (
                intervaloSolicitudAuthenticator
            ) {

                clearInterval(
                    intervaloSolicitudAuthenticator
                );

                intervaloSolicitudAuthenticator =
                    null;
            }

            mostrarModalAuthenticator(
                "Número incorrecto",
                "El número ingresado en CrediPlus Authenticator no coincide con el mostrado en esta computadora.",
                null,
                "incorrecto"
            );

            return;
        }
                    // ==============================
                    // EXPIRADO
                    // ==============================

                    if (
                        datos.estado ===
                        "Expirado"
                    ) {

                        solicitudAuthenticatorTerminada =
                            true;

                        if (
                            intervaloSolicitudAuthenticator
                        ) {

                            clearInterval(
                                intervaloSolicitudAuthenticator
                            );

                            intervaloSolicitudAuthenticator =
                                null;
                        }

                        mostrarModalAuthenticator(
                            "Solicitud expirada",
                            "Los 30 segundos terminaron. Inicia una nueva solicitud para volver a intentarlo.",
                            null,
                            "error"
                        );

                        return;
                    }

                    // ==============================
                    // APROBADO
                    // ==============================

        if (
            datos.aprobado === true
        ) {

            solicitudAuthenticatorTerminada =
                true;

            if (
                intervaloSolicitudAuthenticator
            ) {

                clearInterval(
                    intervaloSolicitudAuthenticator
                );

                intervaloSolicitudAuthenticator =
                    null;
            }

            // Limpiar solicitud actual
            solicitudAuthenticatorActual = null;

            mostrarModalAuthenticator(
                "Verificación correcta",
                "Tu identidad fue verificada correctamente. Ingresando a CrediPlus...",
                null,
                "correcto"
            );

            setTimeout(function () {

                window.location.href =
                    datos.redirectUrl;

            }, 1200);

            return;
        }

                }
                catch (error) {

                    console.error(
                        "Error consultando Authenticator:",
                        error
                    );

                    /*
                     * No terminamos inmediatamente.
                     * Puede haber sido un fallo momentáneo
                     * de red.
                     */
                }
            }

            // ==========================================
            // TERMINARON LOS 30 SEGUNDOS
            // ==========================================

            solicitudAuthenticatorTerminada =
                true;

            if (
                intervaloSolicitudAuthenticator
            ) {

                clearInterval(
                    intervaloSolicitudAuthenticator
                );

                intervaloSolicitudAuthenticator =
                    null;
            }

            /*
             * Hacemos una última consulta.
             * El backend verá que FechaExpiracion
             * ya terminó y pondrá Expirado.
             */
            try {

                await fetch(
                    "/Login/ConsultarEstadoSolicitudAuthenticator" +
                    "?solicitudId=" +
                    encodeURIComponent(
                        solicitudId
                    ),
                    {
                        cache: "no-store"
                    }
                );

            }
            catch (error) {

                console.error(
                    "Error actualizando expiración:",
                    error
                );
            }

            mostrarModalAuthenticator(
                "Solicitud expirada",
                "Los 30 segundos terminaron. Inicia una nueva solicitud para volver a intentarlo.",
                null,
                "error"
            );
        }
                function mostrarModalAuthenticator(
            titulo,
            mensaje,
            numero = null,
            tipo = "esperando"
        ) {

            const elemento =
                document.getElementById(
                    "modalEsperandoAuthenticator"
                );

            const tituloElemento =
                document.getElementById(
                    "tituloEstadoAuthenticator"
                );

            const textoElemento =
                document.getElementById(
                    "textoEstadoAuthenticator"
                );

            const numeroElemento =
                document.getElementById(
                    "numeroEstadoAuthenticator"
                );

            const contenedorNumero =
                document.getElementById(
                    "contenedorNumeroAuthenticator"
                );

            const esperando =
                document.getElementById(
                    "esperandoEstadoAuthenticator"
                );

            const botonCerrar =
                document.getElementById(
                    "btnCerrarEstadoAuthenticator"
                );
                    const botonCancelar =
        document.getElementById(
            "btnCancelarEstadoAuthenticator"
        );
            const botonReenviar =
        document.getElementById(
            "btnReenviarEstadoAuthenticator"
        );
                if (botonReenviar) {

            botonReenviar.style.display =
                (
                    tipo === "error" ||
                    tipo === "expirado" ||
                    tipo === "incorrecto"
                )
                    ? "inline-block"
                    : "none";
        }
                if (botonCancelar) {

            botonCancelar.style.display =
                tipo === "esperando"
                    ? "inline-block"
                    : "none";
        }
            const icono =
                document.getElementById(
                    "iconoEstadoAuthenticator"
                );


            tituloElemento.textContent =
                titulo;

            textoElemento.textContent =
                mensaje;


            if (numero !== null) {

                numeroElemento.textContent =
                    numero;

                contenedorNumero.style.display =
                    "block";

            } else {

                contenedorNumero.style.display =
                    "none";
            }


        if (tipo === "esperando") {

            esperando.style.setProperty(
                "display",
                "flex",
                "important"
            );

        } else {

            esperando.style.setProperty(
                "display",
                "none",
                "important"
            );
        }


            botonCerrar.style.display =
                tipo === "esperando"
                    ? "none"
                    : "inline-block";


            if (tipo === "correcto") {

                icono.className =
                    "bi bi-check-circle-fill";

                icono.style.color =
                    "#16a34a";

            }
            else if (tipo === "error") {

                icono.className =
                    "bi bi-x-circle-fill";

                icono.style.color =
                    "#dc2626";

            }
            else {

                icono.className =
                    "bi bi-phone";

                icono.style.color =
                    "#2563eb";
            }


            const modal =
                bootstrap.Modal
                    .getOrCreateInstance(
                        elemento
                    );

            modal.show();
        }
                function iniciarContadorSolicitudAuthenticator() {

            if (intervaloSolicitudAuthenticator) {

                clearInterval(
                    intervaloSolicitudAuthenticator
                );

                intervaloSolicitudAuthenticator =
                    null;
            }

            let segundos = 30;

            const elemento =
                document.getElementById(
                    "tiempoSolicitudAuthenticator"
                );

            if (elemento) {
                elemento.textContent =
                    "30 s";
            }

            intervaloSolicitudAuthenticator =
                setInterval(
                    function () {

                        if (
                            solicitudAuthenticatorTerminada
                        ) {

                            clearInterval(
                                intervaloSolicitudAuthenticator
                            );

                            intervaloSolicitudAuthenticator =
                                null;

                            return;
                        }

                        segundos--;

                        if (segundos < 0) {
                            segundos = 0;
                        }

                        if (elemento) {

                            elemento.textContent =
                                segundos + " s";

                            elemento.style.color =
                                segundos <= 10
                                    ? "#dc2626"
                                    : "#2563eb";
                        }

                        if (segundos <= 0) {

                            clearInterval(
                                intervaloSolicitudAuthenticator
                            );

                            intervaloSolicitudAuthenticator =
                                null;
                        }

                    },
                    1000
                );
        }
                async function cancelarSolicitudAuthenticatorDesdeWeb() {

            if (
                !solicitudAuthenticatorActual ||
                solicitudAuthenticatorTerminada
            ) {
                return;
            }

            solicitudAuthenticatorTerminada = true;

            if (intervaloSolicitudAuthenticator) {

                clearInterval(
                    intervaloSolicitudAuthenticator
                );

                intervaloSolicitudAuthenticator =
                    null;
            }

            try {

                const cuerpo =
                    new URLSearchParams();

                cuerpo.append(
                    "solicitudId",
                    solicitudAuthenticatorActual
                );

                const respuesta =
                    await fetch(
                        "/Login/CancelarSolicitudAuthenticator",
                        {
                            method: "POST",

                            headers: {
                                "Content-Type":
                                    "application/x-www-form-urlencoded"
                            },

                            body:
                                cuerpo.toString()
                        }
                    );

                const datos =
                    await respuesta.json();

                if (!datos.ok) {

                    mostrarModalAuthenticator(
                        "No se pudo cancelar",
                        "No fue posible cancelar la solicitud.",
                        null,
                        "error"
                    );

                    return;
                }

                mostrarModalAuthenticator(
                    "Solicitud cancelada",
                    "La solicitud fue cancelada correctamente.",
                    null,
                    "error"
                );

            }
            catch (error) {

                console.error(
                    "ERROR CANCELANDO AUTHENTICATOR:",
                    error
                );

                mostrarModalAuthenticator(
                    "Error de conexión",
                    "No se pudo cancelar la solicitud.",
                    null,
                    "error"
                );
            }
        }
                async function reenviarSolicitudAuthenticator() {

            // ==========================================
            // LIMPIEZA DEL INTENTO ANTERIOR
            // ==========================================

            if (intervaloSolicitudAuthenticator) {

                clearInterval(
                    intervaloSolicitudAuthenticator
                );

                intervaloSolicitudAuthenticator =
                    null;
            }

            solicitudAuthenticatorActual = null;
            solicitudAuthenticatorTerminada = false;
                    const numeroElemento =
            document.getElementById(
                "numeroEstadoAuthenticator"
            );

        if (numeroElemento) {
            numeroElemento.textContent = "--";
        }

        const contenedorNumero =
            document.getElementById(
                "contenedorNumeroAuthenticator"
            );

        if (contenedorNumero) {
            contenedorNumero.style.display =
                "none";
        }


            const tiempo =
                document.getElementById(
                    "tiempoSolicitudAuthenticator"
                );

            if (tiempo) {

                tiempo.textContent = "30 s";
                tiempo.style.color = "#2563eb";
            }


            const botonReenviar =
                document.getElementById(
                    "btnReenviarEstadoAuthenticator"
                );

            if (botonReenviar) {

                botonReenviar.disabled = true;

                botonReenviar.innerHTML =
                    '<span class="spinner-border spinner-border-sm me-1"></span>' +
                    'Enviando...';
            }


            try {

                const respuesta =
                    await fetch(
                        "/Login/CrearSolicitudAuthenticator",
                        {
                            method: "POST"
                        }
                    );

                const resultado =
                    await respuesta.json();


                if (!resultado.ok) {

                    mostrarModalAuthenticator(
                        "No se pudo reenviar",
                        resultado.mensaje ||
                            "No se pudo crear una nueva solicitud.",
                        null,
                        "error"
                    );

                    return;
                }


                if (!resultado.configurado) {

                    mostrarModalAuthenticator(
                        "Authenticator no disponible",
                        "No hay un dispositivo vinculado.",
                        null,
                        "error"
                    );

                    return;
                }


                // ==========================================
                // NUEVA SOLICITUD
                // ==========================================

                solicitudAuthenticatorActual =
                    resultado.solicitudId;

                solicitudAuthenticatorTerminada =
                    false;


                iniciarContadorSolicitudAuthenticator();


                mostrarModalAuthenticator(
                    "Confirma el inicio de sesión",
                    "Abre CrediPlus Authenticator en tu celular e ingresa el número mostrado.",
                    resultado.numero,
                    "esperando"
                );


                esperarRespuestaAuthenticator(
                    resultado.solicitudId
                );

            }
            catch (error) {

                console.error(
                    "ERROR REENVIANDO AUTHENTICATOR:",
                    error
                );

                mostrarModalAuthenticator(
                    "Error de conexión",
                    "No se pudo reenviar la solicitud.",
                    null,
                    "error"
                );
            }
            finally {

                if (botonReenviar) {

                    botonReenviar.disabled =
                        false;

                    botonReenviar.innerHTML =
                        '<i class="bi bi-arrow-clockwise me-1"></i>' +
                        'Reenviar solicitud';
                }
            }
        }



/* ========================================================
   MIGRACIÓN DE EVENTOS INLINE A ADDEVENTLISTENER
   ======================================================== */
document.addEventListener("DOMContentLoaded", function () {
    // 1. Filtrado de entradas
    const dniInput = document.getElementById("dniInput");
    if (dniInput) {
        dniInput.addEventListener("input", function () {
            this.value = this.value.replace(/[^0-9]/g, "");
        });
    }

    const codigoLogin = document.getElementById("codigoLogin");
    if (codigoLogin) {
        codigoLogin.addEventListener("input", function () {
            this.value = this.value.replace(/[^a-zA-Z0-9]/g, "").toUpperCase();
        });
    }

    const codigoAuthenticatorCorreo = document.getElementById("codigoAuthenticatorCorreo");
    if (codigoAuthenticatorCorreo) {
        codigoAuthenticatorCorreo.addEventListener("input", function () {
            this.value = this.value.replace(/[^a-zA-Z0-9]/g, "").toUpperCase();
        });
    }

    // 2. Botones de voz y autenticación inicial
    const btnGrabarVozLogin = document.getElementById("btnGrabarVozLogin");
    if (btnGrabarVozLogin) {
        btnGrabarVozLogin.addEventListener("click", function () {
            if (typeof grabarVozLogin === "function") {
                grabarVozLogin();
            }
        });
    }

    const btnCancelarRenovacionFormulario = document.getElementById("btnCancelarRenovacionDesdeFormulario");
    if (btnCancelarRenovacionFormulario) {
        btnCancelarRenovacionFormulario.addEventListener("click", function () {
            if (typeof cancelarRenovacionDesdeFormulario === "function") {
                cancelarRenovacionDesdeFormulario();
            }
        });
    }

    const btnVerificarVoz = document.getElementById("btnVerificarVoz");
    if (btnVerificarVoz) {
        btnVerificarVoz.addEventListener("click", function () {
            if (typeof iniciarVerificacionVoz === "function") {
                iniciarVerificacionVoz();
            }
        });
    }

    // 3. Selección de método 2FA
    const opcionCorreo = document.getElementById("opcionCorreo");
    if (opcionCorreo) {
        opcionCorreo.addEventListener("click", function () {
            if (typeof seleccionarMetodo === "function") {
                seleccionarMetodo("correo");
            }
        });
    }

    const opcionAplicacion = document.getElementById("opcionAplicacion");
    if (opcionAplicacion) {
        opcionAplicacion.addEventListener("click", function () {
            if (typeof seleccionarMetodo === "function") {
                seleccionarMetodo("aplicacion");
            }
        });
    }

    const opcionVoz = document.getElementById("opcionVoz");
    if (opcionVoz) {
        opcionVoz.addEventListener("click", function () {
            if (typeof seleccionarMetodo === "function") {
                seleccionarMetodo("voz");
            }
        });
    }

    const linkCambiarBiometriaVoz = document.getElementById("linkCambiarBiometriaVoz");
    if (linkCambiarBiometriaVoz) {
        linkCambiarBiometriaVoz.addEventListener("click", function (e) {
            e.preventDefault();
            if (typeof abrirModalSolicitarRenovacion === "function") {
                abrirModalSolicitarRenovacion();
            }
        });
    }

    const btnContinuarSeguridad = document.getElementById("btnContinuarSeguridad");
    if (btnContinuarSeguridad) {
        btnContinuarSeguridad.addEventListener("click", function () {
            if (typeof continuarVerificacion === "function") {
                continuarVerificacion();
            }
        });
    }

    // 4. Códigos de verificación de login
    const btnReenviarCodigo = document.getElementById("btnReenviarCodigo");
    if (btnReenviarCodigo) {
        btnReenviarCodigo.addEventListener("click", function () {
            if (typeof reenviarCodigoLogin === "function") {
                reenviarCodigoLogin();
            }
        });
    }

    const btnVerificarCodigo = document.getElementById("btnVerificarCodigo");
    if (btnVerificarCodigo) {
        btnVerificarCodigo.addEventListener("click", function () {
            if (typeof verificarCodigoLogin === "function") {
                verificarCodigoLogin();
            }
        });
    }

    const btnUsarCorreoParaActivarVoz = document.getElementById("btnUsarCorreoParaActivarVoz");
    if (btnUsarCorreoParaActivarVoz) {
        btnUsarCorreoParaActivarVoz.addEventListener("click", function () {
            if (typeof usarCorreoParaActivarVoz === "function") {
                usarCorreoParaActivarVoz();
            }
        });
    }

    // 5. Modal de renovación de biometría
    const btnCerrarModalRenovacionHeader = document.getElementById("btnCerrarModalRenovacionHeader");
    if (btnCerrarModalRenovacionHeader) {
        btnCerrarModalRenovacionHeader.addEventListener("click", function () {
            if (typeof cancelarRenovacionBiometria === "function") {
                cancelarRenovacionBiometria();
            }
        });
    }

    const btnCancelarRenovacionPaso1 = document.getElementById("btnCancelarRenovacionPaso1");
    if (btnCancelarRenovacionPaso1) {
        btnCancelarRenovacionPaso1.addEventListener("click", function () {
            if (typeof cancelarRenovacionBiometria === "function") {
                cancelarRenovacionBiometria();
            }
        });
    }

    const btnEnviarCodigoRenovacion = document.getElementById("btnEnviarCodigoRenovacion");
    if (btnEnviarCodigoRenovacion) {
        btnEnviarCodigoRenovacion.addEventListener("click", function () {
            if (typeof solicitarOtpRenovacion === "function") {
                solicitarOtpRenovacion();
            }
        });
    }

    const btnReenviarOtpRenovacion = document.getElementById("btnReenviarOtpRenovacion");
    if (btnReenviarOtpRenovacion) {
        btnReenviarOtpRenovacion.addEventListener("click", function () {
            if (typeof reenviarOtpRenovacion === "function") {
                reenviarOtpRenovacion();
            }
        });
    }

    const btnCancelarRenovacionPaso2 = document.getElementById("btnCancelarRenovacionPaso2");
    if (btnCancelarRenovacionPaso2) {
        btnCancelarRenovacionPaso2.addEventListener("click", function () {
            if (typeof cancelarRenovacionBiometria === "function") {
                cancelarRenovacionBiometria();
            }
        });
    }

    const btnVerificarOtpRenovacion = document.getElementById("btnVerificarOtpRenovacion");
    if (btnVerificarOtpRenovacion) {
        btnVerificarOtpRenovacion.addEventListener("click", function () {
            if (typeof verificarOtpRenovacion === "function") {
                verificarOtpRenovacion();
            }
        });
    }

    // 6. Configuración e instalación de Authenticator
    const btnReenviarAuthenticator = document.getElementById("btnReenviarAuthenticator");
    if (btnReenviarAuthenticator) {
        btnReenviarAuthenticator.addEventListener("click", function () {
            if (typeof enviarCodigoAuthenticator === "function") {
                enviarCodigoAuthenticator();
            }
        });
    }

    const btnEnviarCodigoAuthenticator = document.getElementById("btnEnviarCodigoAuthenticator");
    if (btnEnviarCodigoAuthenticator) {
        btnEnviarCodigoAuthenticator.addEventListener("click", function () {
            if (typeof enviarCodigoAuthenticator === "function") {
                enviarCodigoAuthenticator();
            }
        });
    }

    const btnVerificarAuthenticator = document.getElementById("btnVerificarAuthenticator");
    if (btnVerificarAuthenticator) {
        btnVerificarAuthenticator.addEventListener("click", function () {
            if (typeof verificarCodigoAuthenticator === "function") {
                verificarCodigoAuthenticator();
            }
        });
    }

    const btnContinuarConfiguracionAuthenticator = document.getElementById("btnContinuarConfiguracionAuthenticator");
    if (btnContinuarConfiguracionAuthenticator) {
        btnContinuarConfiguracionAuthenticator.addEventListener("click", function () {
            if (typeof continuarConfiguracionAuthenticator === "function") {
                continuarConfiguracionAuthenticator();
            }
        });
    }

    const btnIniciarInstalacionAuthenticator = document.getElementById("btnIniciarInstalacionAuthenticator");
    if (btnIniciarInstalacionAuthenticator) {
        btnIniciarInstalacionAuthenticator.addEventListener("click", function () {
            if (typeof iniciarProcesoInstalacionAuthenticator === "function") {
                iniciarProcesoInstalacionAuthenticator();
            }
        });
    }

    const btnGuiaInstalacionAnterior = document.getElementById("btnGuiaInstalacionAnterior");
    if (btnGuiaInstalacionAnterior) {
        btnGuiaInstalacionAnterior.addEventListener("click", function () {
            if (typeof anteriorGuiaInstalacionAuthenticator === "function") {
                anteriorGuiaInstalacionAuthenticator();
            }
        });
    }

    const btnGuiaInstalacionSiguiente = document.getElementById("btnGuiaInstalacionSiguiente");
    if (btnGuiaInstalacionSiguiente) {
        btnGuiaInstalacionSiguiente.addEventListener("click", function () {
            if (typeof siguienteGuiaInstalacionAuthenticator === "function") {
                siguienteGuiaInstalacionAuthenticator();
            }
        });
    }

    const btnCopiarCorreoTotp = document.getElementById("btnCopiarCorreoTotp");
    if (btnCopiarCorreoTotp) {
        btnCopiarCorreoTotp.addEventListener("click", function () {
            if (typeof copiarCorreoTotp === "function") {
                copiarCorreoTotp();
            }
        });
    }

    const btnCopiarClaveTotp = document.getElementById("btnCopiarClaveTotp");
    if (btnCopiarClaveTotp) {
        btnCopiarClaveTotp.addEventListener("click", function () {
            if (typeof copiarClaveTotp === "function") {
                copiarClaveTotp();
            }
        });
    }

    const btnAbrirGuiaVinculacionTotp = document.getElementById("btnAbrirGuiaVinculacionTotp");
    if (btnAbrirGuiaVinculacionTotp) {
        btnAbrirGuiaVinculacionTotp.addEventListener("click", function () {
            if (typeof abrirGuiaVinculacionTotp === "function") {
                abrirGuiaVinculacionTotp();
            }
        });
    }

    const btnVolverGuiaInstalacion = document.getElementById("btnVolverGuiaInstalacion");
    if (btnVolverGuiaInstalacion) {
        btnVolverGuiaInstalacion.addEventListener("click", function () {
            if (typeof volverGuiaInstalacion === "function") {
                volverGuiaInstalacion();
            }
        });
    }

    const btnVerificarTotp = document.getElementById("btnVerificarTotp");
    if (btnVerificarTotp) {
        btnVerificarTotp.addEventListener("click", function () {
            if (typeof verificarConfiguracionTotp === "function") {
                verificarConfiguracionTotp();
            }
        });
    }

    const btnGuiaVinculacionAnterior = document.getElementById("btnGuiaVinculacionAnterior");
    if (btnGuiaVinculacionAnterior) {
        btnGuiaVinculacionAnterior.addEventListener("click", function () {
            if (typeof anteriorGuiaVinculacionTotp === "function") {
                anteriorGuiaVinculacionTotp();
            }
        });
    }

    const btnGuiaVinculacionSiguiente = document.getElementById("btnGuiaVinculacionSiguiente");
    if (btnGuiaVinculacionSiguiente) {
        btnGuiaVinculacionSiguiente.addEventListener("click", function () {
            if (typeof siguienteGuiaVinculacionTotp === "function") {
                siguienteGuiaVinculacionTotp();
            }
        });
    }

    // 7. Modal de registro de voz inicial
    const btnRechazarRegistroVozModal = document.getElementById("btnRechazarRegistroVozModal");
    if (btnRechazarRegistroVozModal) {
        btnRechazarRegistroVozModal.addEventListener("click", function () {
            if (typeof rechazarRegistroVozLogin === "function") {
                rechazarRegistroVozLogin();
            }
        });
    }

    const btnConfirmarRegistroVozModal = document.getElementById("btnConfirmarRegistroVozModal");
    if (btnConfirmarRegistroVozModal) {
        btnConfirmarRegistroVozModal.addEventListener("click", function () {
            if (typeof confirmarRegistroVozLogin === "function") {
                confirmarRegistroVozLogin();
            }
        });
    }

    // 8. Estado de Authenticator
    const btnCancelarEstadoAuthenticator = document.getElementById("btnCancelarEstadoAuthenticator");
    if (btnCancelarEstadoAuthenticator) {
        btnCancelarEstadoAuthenticator.addEventListener("click", function () {
            if (typeof cancelarSolicitudAuthenticatorDesdeWeb === "function") {
                cancelarSolicitudAuthenticatorDesdeWeb();
            }
        });
    }

    const btnReenviarEstadoAuthenticator = document.getElementById("btnReenviarEstadoAuthenticator");
    if (btnReenviarEstadoAuthenticator) {
        btnReenviarEstadoAuthenticator.addEventListener("click", function () {
            if (typeof reenviarSolicitudAuthenticator === "function") {
                reenviarSolicitudAuthenticator();
            }
        });
    }
});
