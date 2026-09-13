package com.crediplus.crediplus_authenticator

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.widget.Toast
import androidx.core.app.NotificationManagerCompat
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
            ) ?: return

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

            // Datos inválidos o solicitud vieja.
            if (
                solicitudId.isBlank() ||
                numeroEsperado.isBlank() ||
                baseUrl.isBlank()
            ) {

                finalizarFlujo(
                    context,
                    solicitudId
                )

                return
            }

            // ======================================
            // NÚMERO INCORRECTO
            // ======================================

            if (
                numeroIngresado !=
                numeroEsperado
            ) {

                responderNumero(
                    baseUrl,
                    solicitudId,
                    numeroIngresado
                )

                finalizarFlujo(
                    context,
                    solicitudId
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
            // ======================================

            CrediPlusGuardActivity
                .biometriaEnCurso =
                true

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
        // EXPIRADO
        // ==========================================

        if (
            resultado ==
            "EXPIRADO"
        ) {

            finalizarFlujo(
                context,
                solicitudId
            )

            return
        }

        // ==========================================
        // CANCELADO / RECHAZADO
        // ==========================================

        if (
            resultado ==
            "ANULADO"
        ) {

            cancelarSolicitud(
                baseUrl,
                solicitudId
            )

            finalizarFlujo(
                context,
                solicitudId
            )

            Toast.makeText(
                context,
                "✕ ANULADO",
                Toast.LENGTH_SHORT
            ).show()

            return
        }
    }

    // ==========================================
    // RESPONDER AL SERVIDOR
    // ==========================================

    private fun responderNumero(
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

                CrediPlusBackend.aprobar(
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
    // CANCELAR EN SERVIDOR
    // ==========================================

    private fun cancelarSolicitud(
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
    // LIMPIEZA TOTAL
    // ==========================================

    private fun finalizarFlujo(
        context: Context,
        solicitudId: String
    ) {

        // Borrar preferencias.
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
            .commit()

        // Quitar notificación.
        if (solicitudId.isNotBlank()) {

            NotificationManagerCompat
                .from(context)
                .cancel(
                    solicitudId.hashCode()
                )
        }

        // Reiniciar estados nativos.
        CrediPlusGuardActivity.finalizado =
            true

        CrediPlusGuardActivity.biometriaEnCurso =
            false

        // Destruir overlay.
        context.stopService(
            Intent(
                context,
                OverlayService::class.java
            )
        )

        // Cerrar guardia.
        CrediPlusGuardActivity
            .cerrarGuardia()
    }
}