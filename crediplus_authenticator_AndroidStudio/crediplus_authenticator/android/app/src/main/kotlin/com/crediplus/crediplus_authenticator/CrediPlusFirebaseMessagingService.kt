package com.crediplus.crediplus_authenticator

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import flutter.overlay.window.flutter_overlay_window.OverlayService

class CrediPlusFirebaseMessagingService :
    FirebaseMessagingService() {

    override fun onMessageReceived(
        message: RemoteMessage
    ) {
        super.onMessageReceived(message)

        val tipo =
            message.data["tipo"] ?: ""

        when (tipo) {

            "solicitud_login" -> {
                recibirSolicitudLogin(
                    message
                )
            }

            "cancelar_solicitud" -> {
                cancelarSolicitudDesdeServidor(
                    message
                )
            }
        }
    }

    // ==========================================
    // NUEVA SOLICITUD
    // ==========================================

    private fun recibirSolicitudLogin(
        message: RemoteMessage
    ) {

        val solicitudId =
            message.data["solicitudId"] ?: ""

        val numero =
            message.data["numero"] ?: ""

        val baseUrl =
            message.data["baseUrl"] ?: ""

        if (
            solicitudId.isBlank() ||
            numero.isBlank()
        ) {
            return
        }

        crearCanal()

        val intent =
            Intent(
                this,
                CrediPlusNotificationReceiver::class.java
            ).apply {

                putExtra(
                    "solicitudId",
                    solicitudId
                )

                putExtra(
                    "numero",
                    numero
                )

                putExtra(
                    "baseUrl",
                    baseUrl
                )
            }

        val pendingIntent =
            PendingIntent.getBroadcast(
                this,
                solicitudId.hashCode(),
                intent,
                PendingIntent.FLAG_UPDATE_CURRENT or
                        PendingIntent.FLAG_IMMUTABLE
            )

        val notificacion =
            NotificationCompat.Builder(
                this,
                "crediplus_login"
            )
                .setSmallIcon(
                    R.mipmap.ic_launcher
                )
                .setContentTitle(
                    "CrediPlus Authenticator"
                )
                .setContentText(
                    "Tienes una solicitud de inicio de sesión."
                )
                .setPriority(
                    NotificationCompat.PRIORITY_HIGH
                )
                .setCategory(
                    NotificationCompat.CATEGORY_MESSAGE
                )
                .setAutoCancel(true)
                .setContentIntent(
                    pendingIntent
                )
                .build()

        try {

            NotificationManagerCompat
                .from(this)
                .notify(
                    solicitudId.hashCode(),
                    notificacion
                )

        } catch (_: SecurityException) {
        }
    }

    // ==========================================
    // CANCELACIÓN DESDE LA PC / SERVIDOR
    // ==========================================

    private fun cancelarSolicitudDesdeServidor(
        message: RemoteMessage
    ) {

        val solicitudIdRecibida =
            message.data["solicitudId"] ?: ""

        if (solicitudIdRecibida.isBlank()) {
            return
        }

        val preferencias =
            getSharedPreferences(
                "crediplus_authenticator",
                Context.MODE_PRIVATE
            )

        val solicitudActual =
            preferencias.getString(
                "solicitudId",
                ""
            ) ?: ""

        /*
         * No cerrar una solicitud distinta.
         */
        if (
            solicitudActual.isNotBlank() &&
            solicitudActual !=
            solicitudIdRecibida
        ) {
            return
        }

        // Quitar notificación pendiente.
        NotificationManagerCompat
            .from(this)
            .cancel(
                solicitudIdRecibida.hashCode()
            )

        // Marcar flujo terminado.
        CrediPlusGuardActivity.finalizado =
            true

        CrediPlusGuardActivity.biometriaEnCurso =
            false

        // Cerrar overlay.
        stopService(
            Intent(
                this,
                OverlayService::class.java
            )
        )

        CrediPlusGuardActivity
            .cerrarGuardia()

        /*
         * Limpiar datos de esta solicitud.
         */
        preferencias
            .edit()
            .remove("solicitudId")
            .remove("numeroVerificacion")
            .remove("baseUrl")
            .apply()
    }

    private fun crearCanal() {

        if (
            Build.VERSION.SDK_INT >=
            Build.VERSION_CODES.O
        ) {

            val canal =
                NotificationChannel(
                    "crediplus_login",
                    "Solicitudes de inicio de sesión",
                    NotificationManager.IMPORTANCE_HIGH
                )

            canal.description =
                "Solicitudes de seguridad de CrediPlus"

            val manager =
                getSystemService(
                    NotificationManager::class.java
                )

            manager.createNotificationChannel(
                canal
            )
        }
    }
}