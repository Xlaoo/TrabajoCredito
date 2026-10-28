/**
 * Script para Perfil de Administrador - CrediPlus
 */
document.addEventListener('DOMContentLoaded', function () {
    const configEl = document.getElementById('perfilAdminDatos');
    const correoOriginal = configEl ? configEl.getAttribute('data-correo-original') : '';
    const urlEnviarCodigo = configEl ? configEl.getAttribute('data-url-enviar-codigo') : '';
    const urlPreferencias = configEl ? configEl.getAttribute('data-url-preferencias') : '';

    // Utilidad: Limpiar input
    document.addEventListener('click', function (e) {
        const btnClear = e.target.closest('.btn-clear');
        if (btnClear) {
            const wrap = btnClear.closest('.input-util-wrap');
            if (wrap) {
                const input = wrap.querySelector('input');
                if (input) {
                    input.value = '';
                    input.focus();
                    input.dispatchEvent(new Event('input'));
                }
            }
            return;
        }

        // Utilidad: Alternar visibilidad de contraseña
        const btnEye = e.target.closest('.btn-eye');
        if (btnEye) {
            const wrap = btnEye.closest('.input-util-wrap');
            if (wrap) {
                const input = wrap.querySelector('input');
                if (input) {
                    const isPass = input.type === 'password';
                    input.type = isPass ? 'text' : 'password';
                    const icon = btnEye.querySelector('i');
                    if (icon) {
                        icon.className = isPass ? 'fas fa-eye-slash' : 'fas fa-eye';
                    }
                }
            }
            return;
        }
    });

    // Filtros de solo números para celular y DNI
    const celularInput = document.getElementById('celularInput');
    if (celularInput) {
        celularInput.addEventListener('input', function () {
            this.value = this.value.replace(/[^0-9]/g, '').slice(0, 9);
        });
    }

    const dniInput = document.getElementById('dniInput');
    if (dniInput) {
        dniInput.addEventListener('input', function () {
            this.value = this.value.replace(/[^0-9]/g, '').slice(0, 8);
        });
    }

    // Cuadro de verificación por si cambia el correo
    const correoInput = document.getElementById('correoInput');
    const codigoBox = document.getElementById('codigoBox');

    function onCorreoCambia() {
        if (!correoInput || !codigoBox) return;
        const nuevo = correoInput.value.trim();
        if (nuevo !== correoOriginal) {
            codigoBox.classList.add('show');
        } else {
            codigoBox.classList.remove('show');
        }
    }

    if (correoInput) {
        correoInput.addEventListener('input', onCorreoCambia);
    }

    // Botón enviar código
    const btnEnviarCodigo = document.getElementById('btnEnviarCodigo');
    if (btnEnviarCodigo) {
        btnEnviarCodigo.addEventListener('click', function () {
            if (!correoInput || !urlEnviarCodigo) return;
            const correo = correoInput.value.trim();
            fetch(urlEnviarCodigo, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: 'correo=' + encodeURIComponent(correo)
            })
            .then(r => r.json())
            .then(d => alert(d.mensaje))
            .catch(() => alert("No se pudo enviar el código."));
        });
    }

    // Validación de reglas de contraseña
    const claveInput = document.getElementById('claveInput');
    const reglasClave = document.getElementById('reglasClave');

    function validarClave() {
        if (!claveInput || !reglasClave) return;
        const v = claveInput.value;

        if (v.length > 0) {
            reglasClave.classList.add('show');
        } else {
            reglasClave.classList.remove('show');
        }

        setRegla("r-mayus", /[A-Z]/.test(v), "Una letra mayúscula");
        setRegla("r-minus", /[a-z]/.test(v), "Una letra minúscula");
        setRegla("r-num",   /[0-9]/.test(v), "Un número");
        setRegla("r-min",   v.length >= 6,   "Mínimo 6 caracteres");
    }

    function setRegla(id, ok, texto) {
        const el = document.getElementById(id);
        if (!el) return;
        el.className = ok ? "regla-ok" : "regla-bad";
        el.textContent = (ok ? "✔ " : "✖ ") + texto;
    }

    if (claveInput) {
        claveInput.addEventListener('input', validarClave);
    }

    // Guardar preferencias de notificaciones
    const notifCorreo = document.getElementById('notifCorreo');
    const notifSistema = document.getElementById('notifSistema');
    const notifRecordatorios = document.getElementById('notifRecordatorios');

    function guardarPreferencias() {
        if (!urlPreferencias) return;
        const datos = new URLSearchParams();
        if (notifCorreo) datos.append("notificacionCorreo", notifCorreo.checked);
        if (notifSistema) datos.append("notificacionSistema", notifSistema.checked);
        if (notifRecordatorios) datos.append("notificacionRecordatorio", notifRecordatorios.checked);

        fetch(urlPreferencias, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body: datos.toString()
        });
    }

    [notifCorreo, notifSistema, notifRecordatorios].forEach(function (chk) {
        if (chk) {
            chk.addEventListener('change', guardarPreferencias);
        }
    });

    // Mostrar toda la actividad
    const btnVerActividad = document.getElementById('btnVerActividad');
    if (btnVerActividad) {
        btnVerActividad.addEventListener('click', function () {
            document.querySelectorAll('.oculto-actividad').forEach(item => {
                item.classList.remove('oculto-actividad');
            });

            const lista = document.getElementById('actividadLista');
            if (lista) {
                lista.classList.add('expandida');
            }

            btnVerActividad.style.display = 'none';
        });
    }
});
