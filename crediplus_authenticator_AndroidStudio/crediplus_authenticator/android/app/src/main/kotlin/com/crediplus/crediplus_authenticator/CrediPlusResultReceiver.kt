package com.crediplus.crediplus_authenticator

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.widget.Toast
import flutter.overlay.window.flutter_overlay_window.OverlayService


class CrediPlusResultReceiver :
    BroadcastReceiver() {

    override fun onReceive(
        context: Context,
        intent: Intent
    ) {

        val resultado =
            intent.getStringExtra(
                "resultado"
            ) ?: "ANULADO"


        val preferencias =
            context.getSharedPreferences(
                "crediplus_authenticator",
                Context.MODE_PRIVATE
            )


        val solicitudId =
            preferencias.getString(
                "solicitudId",
                ""
            ) ?: ""


        val numeroEsperado =
            preferencias.getString(
                "numeroVerificacion",
                ""
            ) ?: ""


        val baseUrl =
            preferencias.getString(
                "baseUrl",
                ""
            ) ?: ""


        // ==========================================
        // VERIFICAR NÚMERO
        // ==========================================

        if (
            resultado ==
            "VERIFICAR_NUMERO"
        ) {

            val numeroIngresado =
                intent.getStringExtra(
                    "numeroIngresado"
                ) ?: ""


            // ======================================
            // NÚMERO INCORRECTO
            // ======================================

            if (
                numeroIngresado !=
                numeroEsperado
            ) {

                responderNumero(
                    context = context,
                    baseUrl = baseUrl,
                    solicitudId = solicitudId,
                    numero = numeroIngresado
                )

                cerrarTodo(
                    context
                )

                limpiarSolicitud(
                    context
                )


                Toast.makeText(
                    context,
                    "✕ NÚMERO INCORRECTO",
                    Toast.LENGTH_SHORT
                ).show()

                return
            }


            // ======================================
            // NÚMERO CORRECTO
            // ABRIR BIOMETRÍA
            // ======================================

            val biometricIntent =
                Intent(
                    context,
                    CrediPlusBiometricActivity::class.java
                ).apply {

                    putExtra(
                        "solicitudId",
                        solicitudId
                    )

                    putExtra(
                        "numero",
                        numeroIngresado
                    )

                    putExtra(
                        "baseUrl",
                        baseUrl
                    )

                    addFlags(
                        Intent.FLAG_ACTIVITY_NEW_TASK
                    )
                }


            context.startActivity(
                biometricIntent
            )

            return
        }


        // ==========================================
        // USUARIO CANCELÓ
        // ==========================================

        if (
            resultado ==
            "ANULADO"
        ) {

            cancelarSolicitud(
                context,
                baseUrl,
                solicitudId
            )

            cerrarTodo(
                context
            )

            limpiarSolicitud(
                context
            )


            Toast.makeText(
                context,
                "✕ ANULADO",
                Toast.LENGTH_SHORT
            ).show()
        }
    }


    // ==========================================
    // ENVIAR NÚMERO AL BACKEND
    // ==========================================

    private fun responderNumero(
        context: Context,
        baseUrl: String,
        solicitudId: String,
        numero: String
    ) {

        if (
            baseUrl.isBlank() ||
            solicitudId.isBlank() ||
            numero.isBlank()
        ) {
            return
        }


        val pendingResult =
            goAsync()


        Thread {

            try {

                CrediPlusBackend.responder(
                    baseUrl,
                    solicitudId,
                    numero
                )

            } catch (e: Exception) {

                e.printStackTrace()

            } finally {

                pendingResult.finish()
            }

        }.start()
    }


    // ==========================================
    // CANCELAR SOLICITUD
    // ==========================================

    private fun cancelarSolicitud(
        context: Context,
        baseUrl: String,
        solicitudId: String
    ) {

        if (
            baseUrl.isBlank() ||
            solicitudId.isBlank()
        ) {
            return
        }


        val pendingResult =
            goAsync()


        Thread {

            try {

                CrediPlusBackend.cancelar(
                    baseUrl,
                    solicitudId
                )

            } catch (e: Exception) {

                e.printStackTrace()

            } finally {

                pendingResult.finish()
            }

        }.start()
    }


    // ==========================================
    // LIMPIAR SOLICITUD DEL CELULAR
    // ==========================================

    private fun limpiarSolicitud(
        context: Context
    ) {

        val preferencias =
            context.getSharedPreferences(
                "crediplus_authenticator",
                Context.MODE_PRIVATE
            )


        preferencias
            .edit()
            .remove("solicitudId")
            .remove("numeroVerificacion")
            .remove("baseUrl")
            .apply()
    }


    // ==========================================
    // CERRAR MODAL FLOTANTE
    // ==========================================

    private fun cerrarTodo(
        context: Context
    ) {

        CrediPlusGuardActivity.finalizado =
            true

        CrediPlusGuardActivity.biometriaEnCurso =
            false


        context.stopService(
            Intent(
                context,
                OverlayService::class.java
            )
        )


        CrediPlusGuardActivity
            .cerrarGuardia()
    }
}