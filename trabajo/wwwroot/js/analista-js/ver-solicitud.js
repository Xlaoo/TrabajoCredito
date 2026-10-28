    const solicitudContextoEl = document.getElementById("solicitudContexto");
const idSolicitudActual = solicitudContextoEl ? Number(solicitudContextoEl.dataset.idSolicitud) : 0;
const estaEnEvaluacion = solicitudContextoEl ? (solicitudContextoEl.dataset.estaEnEvaluacion === "true") : false;
const plazoOriginalSolicitud = solicitudContextoEl ? (solicitudContextoEl.dataset.plazo || "") : "";
const abrirMensajeParam = solicitudContextoEl ? (solicitudContextoEl.dataset.abrirMensaje || "") : "";
                    function formatText(command) {
        document.getElementById("editorComentarios").focus();
        document.execCommand(command, false, null);
    }
    const editor = document.getElementById("editorComentarios");
    if (editor) {
        editor.addEventListener("focus", function () {
            if (editor.innerHTML === "<br>") {
                editor.innerHTML = "";
            }
        });

        editor.addEventListener("blur", function () {
            if (editor.innerText.trim() === "") {
                editor.innerHTML = "";
            }
        });
    }

    const btnNoValidarEl = document.getElementById("btnNoValidar");
    if (btnNoValidarEl) {
        btnNoValidarEl.addEventListener("click", function(){
            const evalFin = document.getElementById("evaluacionFinanciera");
            if (evalFin) evalFin.style.display = "none";
            const btnVal = document.getElementById("btnValidar");
            if (btnVal) btnVal.style.display = "inline-block";
            const btnReg = document.getElementById("btnRegresar");
            if (btnReg) btnReg.style.display = "inline-flex";
        });
    }

    const selectorRiesgoEl = document.getElementById("selectorRiesgo");
    if (selectorRiesgoEl) {
        selectorRiesgoEl.addEventListener("change", function(){
            const badge = document.getElementById("badgeNivel");
            if (badge) badge.innerText = this.options[this.selectedIndex].text;
        });
    }
    const modalEvaluacion = document.getElementById("modalEvaluacion");
    const cerrarModal = document.querySelector(".cerrar-modal");
    const btnRegresarModal = document.getElementById("btnRegresarModal");
    const btnEnviarModal = document.getElementById("btnEnviarModal");
    const btnEvaluacion = document.getElementById("btnEvaluacion");
    const contenedorOpciones = document.getElementById("contenedorOpciones");
    const btnAgregarOpcion = document.getElementById("btnAgregarOpcion");


    if (btnEvaluacion) {

        btnEvaluacion.addEventListener("click", function () {

            const comentario = document
                .getElementById("editorComentarios")
                .innerText.trim();

            const nivelRiesgo = document
                .querySelector(".resultado-riesgo span")
                .innerText.trim()
                .toUpperCase();

            const nivelRecomendacion = document
                .getElementById("badgeNivel")
                .innerText.trim();
    console.log("Nivel riesgo:", "[" + nivelRiesgo + "]");
    console.log("Perfil modificado:", perfilModificado);
            if (!perfilModificado &&
                (nivelRiesgo === "ALTO" || nivelRiesgo === "MEDIO")) {

                Swal.fire({
                    icon: "warning",
                    title: "Perfil financiero pendiente",
                    text: "Debe editar y guardar los cambios del perfil financiero.",
                    confirmButtonText: "Entendido"
                });

                return;
            }

            if (!perfilModificado && nivelRiesgo === "BAJO") {

                Swal.fire({
                    title: "Perfil financiero",
                    text: "¿Harás algún cambio en el perfil financiero (monto o plazo)?",
                    icon: "question",
                    showCancelButton: true,
                    confirmButtonText: "Sí",
                    cancelButtonText: "No"
                }).then((result) => {

                    if (result.isConfirmed) {

                        Swal.fire({
                            icon: "warning",
                            title: "Perfil financiero pendiente",
                            text: "Debe editar y guardar los cambios del perfil financiero.",
                            confirmButtonText: "Entendido"
                        });

                    } else {

                        perfilModificado = true;
                        btnEvaluacion.click();

                    }

                });

                return;
            }

            if (comentario === "") {

                Swal.fire({
                    icon: "warning",
                    title: "Comentario obligatorio",
                    text: "Debe escribir un comentario para continuar.",
                    confirmButtonText: "Entendido"
                });

                return;
            }

            if (nivelRecomendacion === "Manual") {

                Swal.fire({
                    icon: "warning",
                    title: "Nivel de recomendación",
                    text: "Debe seleccionar un nivel de recomendación.",
                    confirmButtonText: "Entendido"
                });

                return;
            }

            modalEvaluacion.style.display = "flex";

        });

    }
                            const btnRechazar = document.getElementById("btnRechazar");

    if (btnRechazar) {

        btnRechazar.addEventListener("click", function () {

            const comentario = document
                .getElementById("editorComentarios")
                .innerText.trim();

            const nivel = document
                .getElementById("badgeNivel")
                .innerText.trim();

            if (comentario === "") {

                Swal.fire({
                    icon: "warning",
                    title: "Comentario requerido",
                    text: "Debe registrar un comentario antes de continuar.",
                    confirmButtonColor: "#7c3aed"
                });

                return;
            }

            if (nivel === "Manual") {

                Swal.fire({
                    icon: "warning",
                    title: "Nivel de recomendación",
                    text: "Debe seleccionar un nivel de recomendación.",
                    confirmButtonColor: "#7c3aed"
                });

                return;
            }

            Swal.fire({
                title: "¿Está seguro de rechazar al cliente?",
                text: "La solicitud será Rechazada.",
                icon: "question",
                showCancelButton: true,
                confirmButtonColor: "#16a34a",
                cancelButtonColor: "#6b7280",
                confirmButtonText: "SI",
                cancelButtonText: "NO"
            }).then((result) => {
                if (result.isConfirmed) {
                    fetch('/Analista/RechazarSolicitud', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body:
                'idSolicitud=' + idSolicitudActual
                + '&comentario=' + encodeURIComponent(comentario)
                + '&nivelRiesgo=' + encodeURIComponent(nivel)
        })
        .then(response => response.json())
        .then(data => {

            if (data.success) {

                        Swal.fire({
        icon: "success",
        title: "Solicitud rechazada",
        text: "El usuario fue rechazado correctamente.",
        confirmButtonColor: "#dc2626"
    }).then(() => {

        window.location.href = '/Analista/SolicitudesPendientes';

    });

            }
            else {

                Swal.fire({
                    icon: "error",
                    title: "Error",
                    text: data.mensaje
                });

            }

        })
        .catch(error => {

            console.error(error);

            Swal.fire({
                icon: "error",
                title: "Error",
                text: "Ocurrió un error al rechazar la solicitud."
            });

        });

    }

            });

        });

    }
    const btnAprobar = document.getElementById("btnAprobar");

    if (btnAprobar) {

        btnAprobar.addEventListener("click", function () {

            const comentario = document
                .getElementById("editorComentarios")
                .innerText.trim();

            const nivel = document
                .getElementById("badgeNivel")
                .innerText.trim();

    if (!perfilModificado) {

        Swal.fire({
            title: "Perfil financiero",
            text: "¿Seguirás con los mismos datos de monto y plazo?",
            footer: "Si selecciona 'No', deberá editar y guardar el perfil financiero.",
            icon: "question",
            showCancelButton: true,
            confirmButtonText: "Sí",
            cancelButtonText: "No"
        }).then((result) => {

            if (result.isConfirmed) {

                perfilModificado = true;
                btnAprobar.click();
            }
            else {

                Swal.fire({
                    icon: "warning",
                    title: "Perfil financiero pendiente",
                    text: "Debe editar y guardar los cambios del perfil financiero para continuar."
                });

            }

        });

        return;
    }

            if (comentario === "") {

                Swal.fire({
                    icon: "warning",
                    title: "Comentario requerido",
                    text: "Debe registrar un comentario antes de continuar.",
                    confirmButtonColor: "#7c3aed"
                });

                return;
            }
            if (nivel === "Manual") {

                Swal.fire({
                    icon: "warning",
                    title: "Nivel de recomendación",
                    text: "Debe seleccionar un nivel de recomendación.",
                    confirmButtonColor: "#7c3aed"
                });

                return;
            }

            Swal.fire({
                title: "¿Está seguro de aprobar el crédito al usuario?",
                text: "La solicitud será aprobada.",
                icon: "question",
                showCancelButton: true,
                confirmButtonColor: "#16a34a",
                cancelButtonColor: "#6b7280",
                confirmButtonText: "SI",
                cancelButtonText: "NO"
            }).then((result) => {

                if (result.isConfirmed) {

                    fetch('/Analista/AprobarSolicitud', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/x-www-form-urlencoded'
                        },
                        body:
                            'idSolicitud=' + idSolicitudActual
                            + '&comentario=' + encodeURIComponent(comentario)
                            + '&nivelRiesgo=' + encodeURIComponent(nivel)
                    })
                    .then(response => response.json())
                    .then(data => {

                        if (data.success) {

                            Swal.fire({
                                icon: "success",
                                title: "Solicitud aprobada",
                                text: "El usuario fue aprobado correctamente.",
                                confirmButtonColor: "#16a34a"
                            }).then(() => {

                                window.location.href = '/Analista/SolicitudesPendientes';

                            });

                        }
                        else {

                            Swal.fire({
                                icon: "error",
                                title: "Error",
                                text: data.mensaje
                            });

                        }

                    })
                    .catch(error => {

                        console.error(error);

                        Swal.fire({
                            icon: "error",
                            title: "Error",
                            text: "Ocurrió un error al aprobar la solicitud."
                        });

                    });

                }

            });

        });

    }

        const btnValidar = document.getElementById("btnValidar");
        const btnRegresar = document.getElementById("btnRegresar");
        const evaluacionFinanciera = document.getElementById("evaluacionFinanciera");
                const btnHistorial =
    document.querySelector(".btn-historial");

    console.log("btnValidar =", btnValidar);

    if (btnValidar) {
        btnValidar.addEventListener("click", function () {

        console.log("Botón presionado");
        console.log("Id Solicitud:", idSolicitudActual);

                fetch('/Analista/ValidarCliente?idSolicitud=' + idSolicitudActual, {
        method: 'POST'
    })
    .then(async response => {

        if (!response.ok) {
            const mensaje = await response.text();
            throw new Error(mensaje);
        }

        return response.json();
    })
    .then(data =>{



    console.log(data);

    const mensajeSinHistorial =
        document.getElementById("sinHistorial");

        if (
            data.activas == 0 &&
            data.pagadas == 0 &&
            data.vencidas == 0
        ) {
            btnHistorial.style.display = "none";
            mensajeSinHistorial.style.display = "block";
        }
        else {
            btnHistorial.style.display = "block";
            mensajeSinHistorial.style.display = "none";
        }

        document.getElementById("txtActivas").innerText = data.activas;
        document.getElementById("txtPagadas").innerText = data.pagadas;
        document.getElementById("txtVencidas").innerText = data.vencidas;
                if (
        data.activas == 0 &&
        data.pagadas == 0 &&
        data.vencidas == 0
    )
    {
        document.getElementById("txtUltimaActualizacion")
            .innerText = "No tiene historial";
    }
    else
    {
        document.getElementById("txtUltimaActualizacion")
            .innerText = data.ultimaFecha;
    }

        document.getElementById("txtDeudasEncontradas").innerText =
            (data.activas + data.vencidas) + " deudas encontradas";
                    let nivel = data.nivel || "MEDIO";
                    let clase = data.clase || "medio";
                    let aguja = data.aguja || "-20deg";

                    document.querySelector(".resultado-riesgo span").innerText = nivel;
                    document.querySelector(".resultado-riesgo span").className = clase;

                    document.querySelector(".aguja").style.transform =
                        `translateX(-50%) rotate(${aguja})`;

                    const motivoEl = document.getElementById("txtMotivoScoring");
                    if (motivoEl && data.motivo) {
                        motivoEl.innerText = data.motivo;
                    }

                    const btnAprobar = document.getElementById("btnAprobar");
                    const btnRechazar = document.getElementById("btnRechazar");
                    const btnEvaluacion = document.getElementById("btnEvaluacion");

                    // estaEnEvaluacion is defined at top

                    if (estaEnEvaluacion) {
                        // En evaluación: solo aprobar si no es crítico ni alto y la cuota es financieramente viable
                        btnAprobar.disabled = (nivel === "CRÍTICO" || nivel === "ALTO" || !data.esCuotaViable);
                        btnRechazar.disabled = false;
                        if (btnEvaluacion) {
                            btnEvaluacion.disabled = true;
                        }
                    }
                    else {
                        // RIESGO CRÍTICO y ALTO: Aprobación ordinaria estrictamente bloqueada
                        if (nivel === "CRÍTICO" || nivel === "ALTO") {
                            btnAprobar.disabled = true;
                            btnRechazar.disabled = false;
                            if (btnEvaluacion) btnEvaluacion.disabled = false;
                        }
                        // RIESGO MEDIO y BAJO: Aprobación permitida solo si la cuota es financieramente viable
                        else {
                            btnAprobar.disabled = !data.esCuotaViable;
                            btnRechazar.disabled = false;
                            if (btnEvaluacion) btnEvaluacion.disabled = false;
                        }
                    }

                btnRegresar.style.display = "none";
    btnValidar.style.display = "none";

    document.getElementById("btnNoValidar").style.display = "flex";

    evaluacionFinanciera.style.display = "flex";
    })
    .catch(error => {
        console.error(error);
        alert(error.message);
    });

    });
    }
        const btnIniciarEdicion = document.getElementById("btnIniciarEdicion");
        const btnGuardarPerfil = document.getElementById("btnGuardarPerfil");
        const btnCancelarPerfil = document.getElementById("btnCancelarPerfil");
        const grupoAcciones = document.getElementById("grupoAcciones");

        const textoMonto = document.getElementById("txtMonto");
        const textoPlazo = document.getElementById("txtPlazo");
        const textoIngreso = document.getElementById("txtIngresoPerfil");
        const textoEgreso = document.getElementById("txtEgresoPerfil");

        const inputMonto = document.getElementById("inputMonto");
        const inputPlazo = document.getElementById("inputPlazo");
        const inputIngreso = document.getElementById("inputIngreso");
        const inputEgreso = document.getElementById("inputEgreso");

        if (inputPlazo) {
            inputPlazo.value = plazoOriginalSolicitud;
        }

        let montoOriginal;
        let plazoOriginal;
        let ingresoOriginal;
        let egresoOriginal;
        let decisionPerfil = null;
        let perfilModificado = false;

        if (btnIniciarEdicion) {
            btnIniciarEdicion.addEventListener("click", function () {
                const iconoEst = document.getElementById("iconoEstado");
                if (iconoEst) iconoEst.innerHTML = "💾";

                if (inputMonto) montoOriginal = inputMonto.value;
                if (inputPlazo) plazoOriginal = inputPlazo.value;
                if (inputIngreso) ingresoOriginal = inputIngreso.value;
                if (inputEgreso) egresoOriginal = inputEgreso.value;

                if (textoMonto) textoMonto.style.display = "none";
                if (textoPlazo) textoPlazo.style.display = "none";
                if (textoIngreso) textoIngreso.style.display = "none";
                if (textoEgreso) textoEgreso.style.display = "none";

                if (inputMonto) inputMonto.style.display = "inline-block";
                if (inputPlazo) inputPlazo.style.display = "inline-block";
                if (inputIngreso) inputIngreso.style.display = "inline-block";
                if (inputEgreso) inputEgreso.style.display = "inline-block";

                btnIniciarEdicion.style.display = "none";
                if (grupoAcciones) grupoAcciones.style.display = "inline-flex";
            });
        }

        if (btnGuardarPerfil) {
            btnGuardarPerfil.addEventListener("click", async function () {
            document.getElementById("iconoEstado").innerHTML = "⏳";

            const montoNumerico = parseFloat(inputMonto.value.replace(/[^\d.]/g, "")) || 0;
            const plazoNumerico = parseInt(inputPlazo.value) || 12;
            const ingresoNumerico = inputIngreso ? (parseFloat(inputIngreso.value.replace(/[^\d.]/g, "")) || null) : null;
            const egresoNumerico = inputEgreso ? (parseFloat(inputEgreso.value.replace(/[^\d.]/g, "")) || null) : null;

            if (montoNumerico < 1000 || montoNumerico > 50000) {
                Swal.fire({
                    icon: "warning",
                    title: "Monto no válido",
                    text: "El monto debe estar entre S/ 1,000 y S/ 50,000."
                });
                document.getElementById("iconoEstado").innerHTML = "💾";
                return;
            }

            try {
                const formData = new URLSearchParams();
                formData.append("idSolicitud", idSolicitudActual);
                formData.append("montoSolicitado", montoNumerico);
                formData.append("plazoMeses", plazoNumerico);
                if (ingresoNumerico !== null) formData.append("ingresoMensual", ingresoNumerico);
                if (egresoNumerico !== null) formData.append("egresoMensual", egresoNumerico);

                const response = await fetch('/Analista/GuardarPerfilFinanciero', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: formData.toString()
                });

                const data = await response.json();
                if (!data.ok) {
                    Swal.fire({
                        icon: "error",
                        title: "Error al guardar",
                        text: data.mensaje || "No se pudo actualizar el perfil financiero."
                    });
                    document.getElementById("iconoEstado").innerHTML = "💾";
                    return;
                }

                perfilModificado = true;
                montoOriginal = inputMonto.value;
                plazoOriginal = inputPlazo.value;
                if (inputIngreso) ingresoOriginal = inputIngreso.value;
                if (inputEgreso) egresoOriginal = inputEgreso.value;

                // 1. Actualizar textos en Perfil Financiero
                textoMonto.innerText = "S/ " + Number(data.monto).toLocaleString('es-PE');
                textoPlazo.innerText = data.plazo + " meses";
                if (textoIngreso && data.ingreso) textoIngreso.innerText = "S/ " + Number(data.ingreso).toLocaleString('es-PE', { minimumFractionDigits: 0 });
                if (textoEgreso && data.egreso) textoEgreso.innerText = "S/ " + Number(data.egreso).toLocaleString('es-PE', { minimumFractionDigits: 0 });

                // 2. Actualizar tarjetas de Resumen Financiero
                const resIngreso = document.getElementById("txtIngresosResumen");
                if (resIngreso) resIngreso.innerText = "S/ " + Number(data.ingreso).toLocaleString('es-PE', { minimumFractionDigits: 2 });

                const resEgreso = document.getElementById("txtEgresosResumen");
                if (resEgreso) resEgreso.innerText = "S/ " + Number(data.egreso).toLocaleString('es-PE', { minimumFractionDigits: 2 });

                const resCapacidad = document.getElementById("txtCapacidadResumen");
                if (resCapacidad) resCapacidad.innerText = "S/ " + Number(data.capacidadPago).toLocaleString('es-PE', { minimumFractionDigits: 2 });

                const resDisponible = document.getElementById("txtDisponibleResumen");
                if (resDisponible) resDisponible.innerText = "S/ " + Number(data.disponiblePrestamo).toLocaleString('es-PE', { minimumFractionDigits: 2 });

                // 3. Actualizar Velocímetro y Scoring
                const resultadoRiesgoSpan = document.querySelector(".resultado-riesgo span");
                if (resultadoRiesgoSpan) {
                    resultadoRiesgoSpan.innerText = data.nivel;
                    resultadoRiesgoSpan.className = data.clase;
                }

                const aguja = document.querySelector(".aguja");
                if (aguja && data.aguja) {
                    aguja.style.transform = `translateX(-50%) rotate(${data.aguja})`;
                }

                const motivoEl = document.getElementById("txtMotivoScoring");
                if (motivoEl && data.motivo) {
                    motivoEl.innerText = data.motivo;
                }

                // 4. Actualizar estado de botones según viabilidad y riesgo
                const btnAprobar = document.getElementById("btnAprobar");
                const btnRechazar = document.getElementById("btnRechazar");
                const btnEvaluacion = document.getElementById("btnEvaluacion");

                if (btnAprobar) {
                    // Si el riesgo es CRÍTICO, ALTO o la cuota supera la capacidad disponible, la aprobación está bloqueada
                    btnAprobar.disabled = (data.nivel === "CRÍTICO" || data.nivel === "ALTO" || !data.esCuotaViable);
                }
                if (btnRechazar) btnRechazar.disabled = false;
                if (btnEvaluacion) btnEvaluacion.disabled = false;

                // 5. Ocultar inputs y mostrar textos
                textoMonto.style.display = "inline";
                textoPlazo.style.display = "inline";
                if (textoIngreso) textoIngreso.style.display = "inline";
                if (textoEgreso) textoEgreso.style.display = "inline";

                inputMonto.style.display = "none";
                inputPlazo.style.display = "none";
                if (inputIngreso) inputIngreso.style.display = "none";
                if (inputEgreso) inputEgreso.style.display = "none";

                grupoAcciones.style.display = "none";
                btnIniciarEdicion.style.display = "inline-block";
                document.getElementById("iconoEstado").innerHTML = "✏️";

                // 6. Alerta de confirmación con advertencia de capacidad si aplica
                if (!data.esCuotaViable) {
                    Swal.fire({
                        icon: "warning",
                        title: `Scoring: ${data.nivel}`,
                        html: `
                            <p><b>Cambios guardados con éxito en la base de datos.</b></p>
                            <p style="color: #dc2626; font-weight: 600; margin-top: 10px;">
                                ⚠️ ALERTA DE CAPACIDAD: La cuota mensual estimada (S/ ${Number(data.cuotaMensual).toFixed(2)}) 
                                supera la capacidad disponible del cliente (S/ ${Number(data.capacidadCuotaDisponible).toFixed(2)}).
                            </p>
                            <p style="margin-top: 8px;">La aprobación directa ha sido bloqueada en frontend y backend. Utilice <b>«EN EVALUACIÓN»</b> para reformular monto o plazo.</p>
                        `,
                        confirmButtonColor: "#f59e0b"
                    });
                } else if (data.nivel === "MEDIO") {
                    Swal.fire({
                        icon: "info",
                        title: "Scoring: MEDIO",
                        html: `
                            <p><b>Cambios guardados con éxito en la base de datos.</b></p>
                            <p style="margin-top: 8px;">${data.motivo}</p>
                        `,
                        confirmButtonColor: "#f59e0b"
                    });
                } else {
                    Swal.fire({
                        icon: "success",
                        title: `Scoring: ${data.nivel}`,
                        text: "Perfil financiero actualizado y scoring recalculado exitosamente.",
                        timer: 2000,
                        showConfirmButton: false
                    });
                }

            } catch (err) {
                console.error(err);
                Swal.fire({
                    icon: "error",
                    title: "Error de conexión",
                    text: "No se pudo comunicar con el servidor para guardar y recalcular el scoring."
                });
            }
        });
        }

        if (btnCancelarPerfil) {
            btnCancelarPerfil.addEventListener("click", function () {
                const iconoEst = document.getElementById("iconoEstado");
                if (iconoEst) iconoEst.innerHTML = "✏️";
                if (inputMonto && montoOriginal !== undefined) inputMonto.value = montoOriginal;
                if (inputPlazo && plazoOriginal !== undefined) inputPlazo.value = plazoOriginal;
                if (inputIngreso && ingresoOriginal !== undefined) inputIngreso.value = ingresoOriginal;
                if (inputEgreso && egresoOriginal !== undefined) inputEgreso.value = egresoOriginal;

                if (textoMonto) textoMonto.style.display = "inline";
                if (textoPlazo) textoPlazo.style.display = "inline";
                if (textoIngreso) textoIngreso.style.display = "inline";
                if (textoEgreso) textoEgreso.style.display = "inline";

                if (inputMonto) inputMonto.style.display = "none";
                if (inputPlazo) inputPlazo.style.display = "none";
                if (inputIngreso) inputIngreso.style.display = "none";
                if (inputEgreso) inputEgreso.style.display = "none";

                if (grupoAcciones) grupoAcciones.style.display = "none";
                if (btnIniciarEdicion) btnIniciarEdicion.style.display = "inline-block";
            });
        }
                document.querySelectorAll(".btnHistorialCredito")
    .forEach(btn => {

        btn.addEventListener("click", async () => {

            const id = btn.dataset.id;

                const response =
                await fetch(`/Analista/ObtenerHistorialCredito?idSolicitud=${id}`);

            const data = await response.json();

            if (!data.ok)
                return;



            let filas = "";

            if (!data.historial || data.historial.length === 0) {
                filas = `
                <tr>
                    <td colspan="11" style="text-align:center; padding: 30px; color: #64748b; font-size: 15px; font-weight: 500;">
                        <i class="fas fa-info-circle me-2" style="color: #6366f1;"></i>
                        No se encontraron registros crediticios para este cliente.
                    </td>
                </tr>`;
            } else {
                data.historial.forEach(h => {
                    const esCrediPlus = h.origen && h.origen.includes("CrediPlus");
                    const origenTag = esCrediPlus
                        ? `<span class="badge" style="background:#e0e7ff; color:#4338ca; font-size:10px; padding:3px 6px; border-radius:4px; margin-left:4px;">CrediPlus</span>`
                        : `<span class="badge" style="background:#f1f5f9; color:#475569; font-size:10px; padding:3px 6px; border-radius:4px; margin-left:4px;">Externo</span>`;

                    filas += `
                    <tr>
                        <td>${data.nombre}</td>
                        <td>${data.apellido}</td>
                        <td>${data.dni}</td>
                        <td><b>${h.banco}</b> ${origenTag}</td>
                        <td>${h.tipoCredito}</td>
                        <td>S/ ${Number(h.monto).toLocaleString('es-PE')}</td>
                        <td>${h.plazoMeses} m</td>
                        <td>${h.cuotasPagadas}</td>
                        <td>
                            <span class="${
                                h.estadoCredito === 'ACTIVA'
                                    ? 'estado-activa'
                                    : h.estadoCredito === 'PAGADA'
                                    ? 'estado-pagada'
                                    : 'estado-vencida'
                            }">
                                ${h.estadoCredito}
                            </span>
                        </td>
                        <td>${h.fechaInicio}</td>
                        <td>${h.fechaFin}</td>
                    </tr>
                    `;
                });
            }

            document.getElementById("tablaHistorial").innerHTML = filas;
            document.getElementById("modalHistorialCredito").style.display = "flex";

        });

    });

    const cerrarHistorialEl = document.getElementById("cerrarHistorial");
    if (cerrarHistorialEl) {
        cerrarHistorialEl.addEventListener("click", () => {
            const modalHist = document.getElementById("modalHistorialCredito");
            if (modalHist) modalHist.style.display = "none";
        });
    }
                    function mostrarAviso(texto)
    {
        Swal.fire({
            icon: 'warning',
            title: 'Atención',
            text: texto,
            confirmButtonText: 'Entendido',
            confirmButtonColor: '#6d28d9',
            allowOutsideClick: false,

            didOpen: () => {
                const swal = document.querySelector('.swal2-container');
                if (swal) {
                    swal.style.zIndex = '999999999';
                }
            }
        });
    }
                    let solicitudActual = 0;

                            function abrirChat(idSolicitud)
    {
        console.log("Solicitud:", idSolicitud);
        solicitudActual = idSolicitud;

        document.getElementById("modalChat").style.display = "flex";

        cargarInfoSolicitud(idSolicitud);

        cargarMensajes(idSolicitud);
    }
                    function cargarInfoSolicitud(idSolicitud)
    {
        fetch('/Analista/ObtenerInfoSolicitud?idSolicitud=' + idSolicitud)
        .then(r => r.json())
        .then(data => {
             console.log(data);
            document.getElementById("tituloSolicitud").innerHTML =
                "Mensajes - Solicitud " + data.numeroSolicitud;

            document.getElementById("chatCliente").innerHTML =
                data.cliente;

            document.getElementById("chatEstado").innerHTML =
                data.estado;
        });
    }
        let imagenSeleccionada = null;
        const fileImagenEl = document.getElementById("fileImagen");
        if (fileImagenEl) {
            fileImagenEl.addEventListener("change", function () {

            if(this.files.length > 0)
    {
        imagenSeleccionada = this.files[0];

        document.getElementById("nombreArchivoSeleccionado")
            .innerHTML =
                    `
    <div class="archivo-preview">
        📷 ${imagenSeleccionada.name}
        <button type="button" class="btn-quitar-imagen">✕</button>
    </div>
    `;
    }

    });
    }
            function quitarImagen()
    {
        imagenSeleccionada = null;

        document.getElementById("fileImagen").value = "";

        document.getElementById("nombreArchivoSeleccionado").innerHTML = "";
    }
                            function enviarMensaje()
    {
        let mensaje = document.getElementById("txtMensaje").value.trim();

        if(mensaje === "" && imagenSeleccionada == null)
        {
            mostrarAviso(
                "Debe escribir un mensaje o adjuntar una imagen."
            );
            return;
        }

        let formData = new FormData();

        formData.append("idSolicitud", solicitudActual);
        formData.append("mensaje", mensaje);

        if(imagenSeleccionada != null)
        {
            formData.append("imagen", imagenSeleccionada);
        }

        fetch('/Analista/EnviarMensaje', {
            method: 'POST',
            body: formData
        })
        .then(r => r.json())
        .then(data => {

            document.getElementById("txtMensaje").value = "";
            document.getElementById("fileImagen").value = "";
              document.getElementById("nombreArchivoSeleccionado")
    .innerHTML = "";
            imagenSeleccionada = null;

            cargarMensajes(solicitudActual);

        })
        .catch(error => {

            console.error(error);
            alert("Error al enviar mensaje");

        });
    }
            function verImagen(url)
    {
        Swal.fire({
            imageUrl: url,
            imageAlt: 'Imagen',
            showConfirmButton: false,
            showCloseButton: true,
            width: '80%'
        });
    }

                    function cargarMensajes(idSolicitud)
    {
        fetch('/Analista/ObtenerMensajes?idSolicitud=' + idSolicitud)
        .then(r => r.json())
        .then(data => {

            let html = "";

            if(data.length === 0)
            {
                html = `
                <div class="chat-vacio">
                    <i class="fas fa-comments"></i>
                    <h4>No hay mensajes todavía</h4>
                    <p>Inicia una conversación con el Usuario</p>
                </div>`;
            }
            else
            {
                data.forEach(m => {
                console.log(m);
                if (m.tipoUsuario === "Analista") {

        html += `
    <div class="mensaje-analista">

        <div class="chat-derecha">

            <div class="cabecera-derecha">
                <span class="nombre-chat">
                    ${m.nombre}
                </span>

                <div class="avatar-chat">
                    <i class="fas fa-user-circle"></i>
                </div>
            </div>

        <div class="${m.imagen ? 'imagen-contenedor' : 'burbuja-analista'}">

        ${m.imagen
            ? `<img src="${m.imagen}"
               class="imagen-chat"
               >`
            : ''
        }

        ${m.mensaje != "[Imagen]"
            ? `<div class="texto-chat">${m.mensaje}</div>`
            : ''
        }

    </div>

            <div class="fecha-chat fecha-derecha">
                ${m.fecha}
            </div>

        </div>

    </div>`;
    }
    else {

        html += `
    <div class="mensaje-cliente">

        <div class="avatar-chat">
            <i class="fas fa-user-circle"></i>
        </div>

        <div>
            <div class="nombre-chat">
                ${m.nombre}
            </div>

                        <div class="${m.imagen ? 'imagen-contenedor' : 'burbuja-cliente'}">

        ${m.imagen
            ? `<img src="${m.imagen}"
               class="imagen-chat"
               >`
            : ''
        }

        ${m.mensaje != "[Imagen]"
            ? `<div class="texto-chat">${m.mensaje}</div>`
            : ''
        }

    </div>

            <div class="fecha-chat">
                ${m.fecha}
            </div>
        </div>

    </div>`;
    }

    });
            }

            document.getElementById("chatMensajes").innerHTML = html;

        });
    }

    function cerrarChat()
    {
        document.getElementById("modalChat").style.display = "none";
    }


    if (btnEnviarModal) {
        btnEnviarModal.addEventListener("click", function () {

        const cantidadOpciones =
            contenedorOpciones.querySelectorAll(".fila-opcion").length;

    if (cantidadOpciones === 0) {

        Swal.fire({
            title: "¿Está seguro?",
            text: "¿Está seguro que desea enviar la opción actual al usuario?",
            icon: "question",
            showCancelButton: true,
            confirmButtonText: "Sí",
            cancelButtonText: "No",
            confirmButtonColor: "#6d28d9"
        }).then((result) => {

            if (result.isConfirmed) {

                const propuestas = [];

                        let montoPrincipal;
    let plazoPrincipal;

    const inputMonto = document.getElementById("inputMonto");
    const inputPlazo = document.getElementById("inputPlazo");

    const txtMonto = document.getElementById("txtMonto");
    const txtPlazo = document.getElementById("txtPlazo");

    if (inputMonto.style.display !== "none") {

    montoPrincipal = parseFloat(
        inputMonto.value.replace(/[^\d]/g, "")
    );

        plazoPrincipal = parseInt(inputPlazo.value);

    } else {

        montoPrincipal = parseFloat(
            txtMonto.textContent.replace(/[^\d]/g, "")
        );

        plazoPrincipal = parseInt(
            txtPlazo.textContent.replace(/[^\d]/g, "")
        );
    }

    propuestas.push({
        monto: montoPrincipal,
        plazoMeses: plazoPrincipal,
        esRecomendada: true
    });
        const comentarioActual = document
    .getElementById("editorComentarios")
    .innerText.trim();

                fetch("/Analista/EnviarEvaluacion", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json"
                    },
                    body: JSON.stringify({
                        idSolicitud: idSolicitudActual,
                        propuestas: propuestas,
                        comentario: comentarioActual

                    })
                })
                .then(response => response.json())
                .then(data => {

                    if (data.success) {

                        Swal.fire({
                            icon: "success",
                            title: "Enviado",
                            text: "La propuesta fue enviada al usuario.",
                            confirmButtonColor: "#6d28d9"
                        }).then(() => {

                            window.location.href = "/Analista/SolicitudesPendientes";

                        });

                    } else {

                        Swal.fire({
                            icon: "error",
                            title: "Error",
                            text: data.mensaje
                        });

                    }

                });

            }

        });

        return;
    }


        if (!validarOpcionesCredito()) {
            return;
        }

        Swal.fire({
            title: "¿Desea enviar estas opciones de crédito al usuario?",
            icon: "warning",
            showCancelButton: true,
            confirmButtonText: "Sí",
            cancelButtonText: "No",
            confirmButtonColor: "#6d28d9"
        }).then((result) => {

                    if (result.isConfirmed) {

        const propuestas = [];

                let montoPrincipal;
    let plazoPrincipal;

    const inputMonto = document.getElementById("inputMonto");
    const inputPlazo = document.getElementById("inputPlazo");

    const txtMonto = document.getElementById("txtMonto");
    const txtPlazo = document.getElementById("txtPlazo");

    if (inputMonto.style.display !== "none") {

                montoPrincipal = parseFloat(
        inputMonto.value.replace(/[^\d]/g, "")
    );

        plazoPrincipal = parseInt(inputPlazo.value);

    } else {

        montoPrincipal = parseFloat(
            txtMonto.textContent.replace(/[^\d]/g, "")
        );

        plazoPrincipal = parseInt(
            txtPlazo.textContent.replace(/[^\d]/g, "")
        );
    }

    propuestas.push({
        monto: montoPrincipal,
        plazoMeses: plazoPrincipal,
        esRecomendada: true
    });
                let opcionRepetida = false;

    document.querySelectorAll(".fila-opcion").forEach(fila => {

        let monto = fila.querySelector(".input-monto").value
            .replace(/[^\d]/g, "");

        let plazo = fila.querySelector(".select-plazo").value;

        if (
            parseFloat(monto) === montoPrincipal &&
            parseInt(plazo) === plazoPrincipal
        ) {
            opcionRepetida = true;
        }

        propuestas.push({
            monto: parseFloat(monto),
            plazoMeses: parseInt(plazo),
            esRecomendada: false
        });

            if (opcionRepetida) {

        Swal.fire({
            icon: "warning",
            title: "Opción repetida",
            text: "Existe una opción adicional igual a la propuesta principal."
        });

        return;
    }

        });
            const comentarioActual = document
    .getElementById("editorComentarios")
    .innerText.trim();

        fetch("/Analista/EnviarEvaluacion", {

            method: "POST",

            headers: {
                "Content-Type": "application/json"
            },

            body: JSON.stringify({

                idSolicitud: idSolicitudActual,
                propuestas: propuestas,
                comentario: comentarioActual

            })

        })
        .then(response => response.json())
        .then(data => {

            if (data.success) {

                        Swal.fire({
        icon: "success",
        title: "Enviado",
        text: "Las propuestas fueron enviadas al usuario.",
        confirmButtonColor: "#6d28d9"
    }).then(() => {

        window.location.href = "/Analista/SolicitudesPendientes";

    });

            }
            else {

                Swal.fire({
                    icon: "error",
                    title: "Error",
                    text: data.mensaje
                });

            }

        });

    }

        });

    });
    }

    if (btnAgregarOpcion) {
        btnAgregarOpcion.addEventListener("click", function () {

        const fila = document.createElement("div");

        fila.className = "fila-opcion";

        fila.innerHTML = `
            <div class="grupo-monto">
                <label>Monto:</label>

                <input type="text"
                       class="input-monto"
                       placeholder="S/ 1,000">
            </div>

            <div class="grupo-plazo">
                <label>Plazo:</label>

    <select class="select-plazo">
        <option value="">Seleccione</option>

        <option value="12">12 meses</option>
        <option value="18">18 meses</option>
        <option value="24">24 meses</option>
        <option value="30">30 meses</option>
        <option value="36">36 meses</option>
        <option value="48">48 meses</option>
        <option value="60">60 meses</option>
        <option value="72">72 meses</option>
    </select>
            </div>

            <button type="button" class="btnEliminarOpcion">
                🗑
            </button>
        `;

        contenedorOpciones.appendChild(fila);
                const nuevoMonto = fila.querySelector(".input-monto");

    nuevoMonto.addEventListener("input", function () {

        let valor = this.value.replace(/\D/g, "");

        if (valor === "") {
            this.value = "";
            return;
        }

        valor = parseInt(valor);

        if (valor > 50000) {
            valor = 50000;
        }

        this.value = "S/ " + valor.toLocaleString('es-PE');

        });

        });
    }

    if (contenedorOpciones) {
        contenedorOpciones.addEventListener("click", function (e) {
            if (e.target.classList.contains("btnEliminarOpcion")) {
                e.target.closest(".fila-opcion").remove();
            }
        });
    }
            function validarOpcionesCredito() {

        const filas = document.querySelectorAll(".fila-opcion");

        for (const fila of filas) {

            const inputMonto = fila.querySelector(".input-monto");
            const plazo = fila.querySelector(".select-plazo");

            let montoTexto = inputMonto.value.trim();

            if (montoTexto === "") {

                Swal.fire({
                    icon: "warning",
                    title: "Monto requerido",
                    text: "Debe ingresar un monto."
                });

                return false;
            }

            let monto = parseInt(
                montoTexto.replace(/[^\d]/g, "")
            );

            if (monto < 1000 || monto > 50000) {

                Swal.fire({
                    icon: "warning",
                    title: "Monto inválido",
                    text: "El monto debe estar entre S/ 1,000 y S/ 50,000."
                });

                return false;
            }

            if (plazo.value === "") {

                Swal.fire({
                    icon: "warning",
                    title: "Plazo requerido",
                    text: "Debe seleccionar un plazo."
                });

                return false;
            }
        }

        return true;
    }


            if (btnRegresarModal) {

        btnRegresarModal.addEventListener("click", function () {

            console.log("CLICK REGRESAR");

            const cantidad =
                contenedorOpciones.querySelectorAll(".fila-opcion").length;

            if (cantidad > 0) {

                Swal.fire({
                    title: "¿Está seguro?",
                    text: "Se perderán todas las opciones agregadas.",
                    icon: "warning",
                    showCancelButton: true,
                    confirmButtonText: "Sí",
                    cancelButtonText: "No"
                }).then((result) => {

                    if (result.isConfirmed) {

                        contenedorOpciones.innerHTML = "";

                        modalEvaluacion.style.display = "none";
                    }

                });

            } else {

                modalEvaluacion.style.display = "none";
            }

        });

    }

    window.addEventListener("click", function (e) {

        if (e.target === modalEvaluacion) {
            modalEvaluacion.style.display = "none";
        }

    });
    const elInputMontoGlobal = document.getElementById("inputMonto");
    if (elInputMontoGlobal) {
        elInputMontoGlobal.addEventListener("input", function () {
            let valor = this.value.replace(/\D/g, "");

            if (valor === "") {
                this.value = "";
                return;
            }

            valor = parseInt(valor);

            if (valor > 50000) {
                valor = 50000;
            }

            this.value = valor.toLocaleString('es-PE');
        });
    }
            function abrirReporte(idSolicitud) {

        document.getElementById("modalReporte").style.display = "flex";
        document.getElementById("contenidoReporte").innerHTML = "Cargando reporte...";

        fetch('/Analista/ObtenerReportePropuestas?idSolicitud=' + idSolicitud)
            .then(response => response.json())
            .then(data => {

                let html = "";

                if (!data.ok || data.propuestas.length === 0) {
                    html = `
                        <div class="sin-reporte">
                            📭 Sin reporte para esta solicitud.
                        </div>`;
                }
                else {
                    data.propuestas.forEach((p, index) => {

                        html += `
                            <div class="reporte-card">
                                <h4>${p.esRecomendada ? "⭐ Propuesta recomendada" : "Opción " + (index + 1)}</h4>
                                <p><b>Monto:</b> S/ ${Number(p.monto).toLocaleString('es-PE')}</p>
                                <p><b>Plazo:</b> ${p.plazoMeses} meses</p>
                            </div>
                        `;

                    });
                }

                document.getElementById("contenidoReporte").innerHTML = html;
            })
            .catch(() => {
                document.getElementById("contenidoReporte").innerHTML = `
                    <div class="sin-reporte">
                        ❌ Error al cargar el reporte.
                    </div>`;
            });
    }

    function cerrarReporte() {
        document.getElementById("modalReporte").style.display = "none";
    }

    document.addEventListener("DOMContentLoaded", function () {
        var abrirMensaje = abrirMensajeParam;

        if (abrirMensaje === 'true') {
            setTimeout(function () {
                var botonMensaje = document.querySelector("#btnMensaje");

                if (botonMensaje) {
                    botonMensaje.click();
                }
            }, 500);
        }
    });


// === Initializations and Event Delegation (CSP compliant) ===
document.addEventListener("DOMContentLoaded", function () {
    const agujaInit = document.querySelector(".aguja[data-rotation]");
    if (agujaInit) {
        agujaInit.style.transform = `translateX(-50%) rotate(${agujaInit.getAttribute("data-rotation")})`;
    }

    const btnAbrirReporte = document.getElementById("btnReporte") || document.getElementById("btnAbrirReporte");
    if (btnAbrirReporte) {
        btnAbrirReporte.addEventListener("click", function () {
            abrirReporte(idSolicitudActual);
        });
    }

    const btnAbrirChat = document.getElementById("btnMensaje") || document.getElementById("btnAbrirChat");
    if (btnAbrirChat) {
        btnAbrirChat.addEventListener("click", function () {
            abrirChat(idSolicitudActual);
        });
    }

    const btnCerrarChat = document.getElementById("btnCerrarChat") || document.querySelector(".btn-cerrar-chat");
    if (btnCerrarChat) {
        btnCerrarChat.addEventListener("click", cerrarChat);
    }

    const btnCerrarReporte = document.getElementById("btnCerrarReporte");
    if (btnCerrarReporte) {
        btnCerrarReporte.addEventListener("click", cerrarReporte);
    }

    const btnSubirImagenChat = document.getElementById("btnSubirImagenChat");
    if (btnSubirImagenChat) {
        btnSubirImagenChat.addEventListener("click", function () {
            const fileInput = document.getElementById("fileImagen");
            if (fileInput) fileInput.click();
        });
    }

    const btnEnviarChat = document.getElementById("btnEnviarChat");
    if (btnEnviarChat) {
        btnEnviarChat.addEventListener("click", enviarMensaje);
    }

    const txtMensajeInput = document.getElementById("txtMensaje");
    if (txtMensajeInput) {
        txtMensajeInput.addEventListener("keydown", function (e) {
            if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                enviarMensaje();
            }
        });
    }

    document.addEventListener("click", function (e) {
        const linkBloqueado = e.target.closest(".menu-bloqueado");
        if (linkBloqueado) {
            e.preventDefault();
            e.stopPropagation();
            return;
        }

        const btnMsg = e.target.closest("#btnMensaje, .btn-mensaje, #btnAbrirChat");
        if (btnMsg) {
            e.preventDefault();
            abrirChat(idSolicitudActual);
            return;
        }

        const btnRep = e.target.closest("#btnReporte, .btn-reporte, #btnAbrirReporte");
        if (btnRep) {
            e.preventDefault();
            abrirReporte(idSolicitudActual);
            return;
        }

        const btnCerrarC = e.target.closest("#btnCerrarChat, .btn-cerrar-chat");
        if (btnCerrarC) {
            e.preventDefault();
            cerrarChat();
            return;
        }

        const btnCerrarR = e.target.closest("#btnCerrarReporte, .reporte-header button");
        if (btnCerrarR) {
            e.preventDefault();
            cerrarReporte();
            return;
        }

        const btnFormat = e.target.closest(".btn-format-text");
        if (btnFormat) {
            formatText(btnFormat.dataset.cmd);
            return;
        }

        if (e.target.closest(".btn-quitar-imagen")) {
            quitarImagen();
            return;
        }

        if (e.target.classList.contains("imagen-chat")) {
            verImagen(e.target.getAttribute("src"));
            return;
        }
    });
});
