package com.crediplus.crediplus_authenticator

import android.app.AlertDialog
import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.widget.Toast
import androidx.biometric.BiometricManager
import androidx.biometric.BiometricPrompt
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.ContextCompat
import androidx.fragment.app.FragmentActivity
import flutter.overlay.window.flutter_overlay_window.OverlayService


class CrediPlusBiometricActivity :
    FragmentActivity() {

    private var autenticacionTerminada =
        false

    private var solicitudId = ""
    private var numero = ""
    private var baseUrl = ""

    private var dialogoCorrecto:
            AlertDialog? = null

    private val handler =
        Handler(
            Looper.getMainLooper()
        )


    override fun onCreate(
        savedInstanceState: Bundle?
    ) {
        super.onCreate(savedInstanceState)

        solicitudId =
            intent.getStringExtra(
                "solicitudId"
            ) ?: ""

        numero =
            intent.getStringExtra(
                "numero"
            ) ?: ""

        baseUrl =
            intent.getStringExtra(
                "baseUrl"
            ) ?: ""

        CrediPlusGuardActivity
            .biometriaEnCurso =
            true

        mostrarSeguridadAndroid()
    }


    // ==========================================
    // SEGURIDAD ANDROID
    // ==========================================

    private fun mostrarSeguridadAndroid() {

        val executor =
            ContextCompat
                .getMainExecutor(this)

        val biometricPrompt =
            BiometricPrompt(
                this,
                executor,
                object :
                    BiometricPrompt.AuthenticationCallback() {

                    override fun onAuthenticationSucceeded(
                        result:
                        BiometricPrompt.AuthenticationResult
                    ) {

                        super.onAuthenticationSucceeded(
                            result
                        )

                        if (
                            autenticacionTerminada
                        ) {
                            return
                        }

                        autenticacionTerminada =
                            true

                        aprobarEnServidor()
                    }


                    override fun onAuthenticationError(
                        errorCode: Int,
                        errString: CharSequence
                    ) {

                        super.onAuthenticationError(
                            errorCode,
                            errString
                        )

                        if (
                            autenticacionTerminada
                        ) {
                            return
                        }

                        autenticacionTerminada =
                            true

                        cancelarEnServidor()
                    }


                    override fun onAuthenticationFailed() {

                        super.onAuthenticationFailed()

                        // Huella incorrecta:
                        // Android permite volver a intentar.
                    }
                }
            )


        val promptInfo =
            BiometricPrompt.PromptInfo
                .Builder()
                .setTitle(
                    "Confirmar inicio de sesión"
                )
                .setSubtitle(
                    "Confirma tu identidad para acceder a CrediPlus"
                )
                .setAllowedAuthenticators(
                    BiometricManager
                        .Authenticators
                        .BIOMETRIC_STRONG or
                            BiometricManager
                                .Authenticators
                                .DEVICE_CREDENTIAL
                )
                .build()


        biometricPrompt.authenticate(
            promptInfo
        )
    }


    // ==========================================
    // APROBAR
    // ==========================================

    private fun aprobarEnServidor() {

        /*
         * Guardamos copias porque después
         * limpiaremos las variables.
         */
        val idActual =
            solicitudId

        val numeroActual =
            numero

        val urlActual =
            baseUrl


        Thread {

            val aprobado =
                CrediPlusBackend.aprobar(
                    urlActual,
                    idActual,
                    numeroActual
                )


            runOnUiThread {

                if (
                    isFinishing ||
                    isDestroyed
                ) {
                    return@runOnUiThread
                }


                if (aprobado) {

                    /*
                     * Limpiar datos y cerrar overlay,
                     * PERO todavía NO cerrar Guardia.
                     *
                     * Primero queremos mostrar
                     * correctamente el modal verde.
                     */
                    limpiarTodo(
                        idSolicitud = idActual,
                        cerrarGuardiaAhora = false
                    )

                    mostrarModalCorrecto()

                } else {

                    limpiarTodo(
                        idSolicitud = idActual,
                        cerrarGuardiaAhora = true
                    )

                    Toast.makeText(
                        applicationContext,
                        "No se pudo aprobar la solicitud",
                        Toast.LENGTH_SHORT
                    ).show()

                    finish()
                }
            }

        }.start()
    }


    // ==========================================
    // BIOMETRÍA CANCELADA / RECHAZADA
    // ==========================================

    private fun cancelarEnServidor() {

        val idActual =
            solicitudId

        val urlActual =
            baseUrl


        Thread {

            if (
                urlActual.isNotBlank() &&
                idActual.isNotBlank()
            ) {

                CrediPlusBackend.cancelar(
                    urlActual,
                    idActual
                )
            }


            runOnUiThread {

                if (
                    isFinishing ||
                    isDestroyed
                ) {
                    return@runOnUiThread
                }


                limpiarTodo(
                    idSolicitud = idActual,
                    cerrarGuardiaAhora = true
                )


                Toast.makeText(
                    applicationContext,
                    "✕ ANULADO",
                    Toast.LENGTH_SHORT
                ).show()


                finish()
            }

        }.start()
    }


    // ==========================================
    // MODAL APROBADO
    // ==========================================

    private fun mostrarModalCorrecto() {

        if (
            isFinishing ||
            isDestroyed
        ) {
            return
        }


        dialogoCorrecto =
            AlertDialog.Builder(this)
                .setTitle(
                    "Verificación correcta"
                )
                .setMessage(
                    "Tu identidad fue verificada correctamente."
                )
                .setIcon(
                    android.R.drawable.checkbox_on_background
                )
                .setCancelable(false)
                .create()


        dialogoCorrecto?.show()


        handler.postDelayed(
            {

                if (
                    isFinishing ||
                    isDestroyed
                ) {
                    return@postDelayed
                }


                if (
                    dialogoCorrecto
                        ?.isShowing ==
                    true
                ) {

                    dialogoCorrecto
                        ?.dismiss()
                }


                dialogoCorrecto =
                    null


                /*
                 * AHORA sí cerramos Guardia.
                 *
                 * Ya no existe un AlertDialog
                 * abierto que pueda generar
                 * WindowLeaked.
                 */
                CrediPlusGuardActivity
                    .cerrarGuardia()


                finish()

            },
            1200
        )
    }


    // ==========================================
    // LIMPIEZA TOTAL
    // ==========================================

    private fun limpiarTodo(
        idSolicitud: String,
        cerrarGuardiaAhora: Boolean
    ) {

        val preferencias =
            getSharedPreferences(
                "crediplus_authenticator",
                Context.MODE_PRIVATE
            )


        // ------------------------------------------
        // LIMPIAR SOLICITUD GUARDADA
        // ------------------------------------------

        preferencias
            .edit()
            .remove(
                "solicitudId"
            )
            .remove(
                "numeroVerificacion"
            )
            .remove(
                "baseUrl"
            )
            .commit()


        // ------------------------------------------
        // QUITAR NOTIFICACIÓN
        // ------------------------------------------

        if (
            idSolicitud.isNotBlank()
        ) {

            NotificationManagerCompat
                .from(this)
                .cancel(
                    idSolicitud.hashCode()
                )
        }


        // ------------------------------------------
        // LIMPIAR VARIABLES
        // ------------------------------------------

        solicitudId = ""
        numero = ""
        baseUrl = ""


        // ------------------------------------------
        // REINICIAR ESTADO NATIVO
        // ------------------------------------------

        CrediPlusGuardActivity.finalizado =
            true

        CrediPlusGuardActivity.biometriaEnCurso =
            false


        // ------------------------------------------
        // CERRAR OVERLAY
        // ------------------------------------------

        stopService(
            Intent(
                this,
                OverlayService::class.java
            )
        )


        // ------------------------------------------
        // CERRAR GUARDIA
        // ------------------------------------------

        if (
            cerrarGuardiaAhora
        ) {

            CrediPlusGuardActivity
                .cerrarGuardia()
        }
    }


    // ==========================================
    // SEGURIDAD AL DESTRUIR ACTIVITY
    // ==========================================

    override fun onDestroy() {

        /*
         * Cancelar cualquier cierre
         * pendiente del Handler.
         */
        handler.removeCallbacksAndMessages(
            null
        )


        /*
         * Si por cualquier motivo Android
         * destruye la Activity antes,
         * cerrar el AlertDialog.
         */
        try {

            if (
                dialogoCorrecto
                    ?.isShowing ==
                true
            ) {

                dialogoCorrecto
                    ?.dismiss()
            }

        } catch (_: Exception) {
        }


        dialogoCorrecto =
            null


        super.onDestroy()
    }
}