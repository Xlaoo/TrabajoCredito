package com.crediplus.crediplus_authenticator

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.os.Handler
import android.os.Looper
import android.provider.Settings
import android.util.Log
import androidx.core.app.NotificationManagerCompat
import flutter.overlay.window.flutter_overlay_window.CrediPlusOverlayLauncher
import flutter.overlay.window.flutter_overlay_window.OverlayService

class CrediPlusNotificationReceiver :
    BroadcastReceiver() {

    override fun onReceive(
        context: Context,
        intent: Intent
    ) {

        val solicitudId =
            intent.getStringExtra(
                "solicitudId"
            ) ?: ""

        val numero =
            intent.getStringExtra(
                "numero"
            ) ?: ""

        val baseUrl =
            intent.getStringExtra(
                "baseUrl"
            ) ?: ""

        if (
            solicitudId.isBlank() ||
            numero.isBlank() ||
            baseUrl.isBlank()
        ) {

            Log.e(
                "CrediPlus",
                "Faltan datos de la solicitud"
            )

            return
        }

        if (
            !Settings.canDrawOverlays(
                context
            )
        ) {
            return
        }

        val preferencias =
            context.getSharedPreferences(
                "crediplus_authenticator",
                Context.MODE_PRIVATE
            )

        val solicitudAnterior =
            preferencias.getString(
                "solicitudId",
                ""
            ) ?: ""

        // ==========================================
        // DESTRUIR FLUJO ANTERIOR
        // ==========================================

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

        if (
            solicitudAnterior.isNotBlank()
        ) {

            NotificationManagerCompat
                .from(context)
                .cancel(
                    solicitudAnterior.hashCode()
                )
        }

        // ==========================================
        // REEMPLAZAR DATOS
        // ==========================================

        preferencias
            .edit()
            .remove("solicitudId")
            .remove("numeroVerificacion")
            .remove("baseUrl")
            .putString(
                "solicitudId",
                solicitudId
            )
            .putString(
                "numeroVerificacion",
                numero
            )
            .putString(
                "baseUrl",
                baseUrl
            )
            .commit()

        /*
         * IMPORTANTE:
         * dejamos tiempo para que OverlayService
         * realmente termine antes de volverlo
         * a iniciar.
         */
        Handler(
            Looper.getMainLooper()
        ).postDelayed(
            {

                CrediPlusGuardActivity.finalizado =
                    false

                CrediPlusGuardActivity.biometriaEnCurso =
                    false

                val guardIntent =
                    Intent(
                        context,
                        CrediPlusGuardActivity::class.java
                    ).apply {

                        addFlags(
                            Intent.FLAG_ACTIVITY_NEW_TASK or
                                    Intent.FLAG_ACTIVITY_NO_ANIMATION or
                                    Intent.FLAG_ACTIVITY_EXCLUDE_FROM_RECENTS
                        )
                    }

                context.startActivity(
                    guardIntent
                )

                Handler(
                    Looper.getMainLooper()
                ).postDelayed(
                    {

                        CrediPlusOverlayLauncher
                            .abrir(
                                context
                            )

                    },
                    500
                )

            },
            700
        )
    }
}