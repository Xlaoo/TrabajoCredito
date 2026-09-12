package com.crediplus.crediplus_authenticator

import android.content.Intent
import android.os.Bundle
import android.widget.Toast
import androidx.biometric.BiometricManager
import androidx.biometric.BiometricPrompt
import androidx.core.content.ContextCompat
import androidx.fragment.app.FragmentActivity
import flutter.overlay.window.flutter_overlay_window.OverlayService

class CrediPlusBiometricActivity :
    FragmentActivity() {

    private var autenticacionTerminada =
        false

    private var solicitudId =
        ""

    private var numero =
        ""

    private var baseUrl =
        ""


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

                        // Huella incorrecta.
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


    private fun aprobarEnServidor() {

        Thread {

            val aprobado =
                CrediPlusBackend.aprobar(
                    baseUrl,
                    solicitudId,
                    numero
                )

            runOnUiThread {

                if (aprobado) {

                    finalizar(
                        "✓ APROBADO"
                    )

                } else {

                    finalizar(
                        "No se pudo aprobar la solicitud"
                    )
                }
            }

        }.start()
    }


    private fun cancelarEnServidor() {

        Thread {

            CrediPlusBackend.cancelar(
                baseUrl,
                solicitudId
            )

            runOnUiThread {

                finalizar(
                    "✕ ANULADO"
                )
            }

        }.start()
    }


    private fun finalizar(
        mensaje: String
    ) {

        CrediPlusGuardActivity.finalizado =
            true

        CrediPlusGuardActivity.biometriaEnCurso =
            false

        cerrarOverlay()

        Toast.makeText(
            applicationContext,
            mensaje,
            Toast.LENGTH_SHORT
        ).show()

        CrediPlusGuardActivity
            .cerrarGuardia()

        finish()
    }


    private fun cerrarOverlay() {

        stopService(
            Intent(
                this,
                OverlayService::class.java
            )
        )
    }
}