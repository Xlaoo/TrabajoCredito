package com.crediplus.crediplus_authenticator

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.provider.Settings
import android.util.Log
import flutter.overlay.window.flutter_overlay_window.CrediPlusOverlayLauncher

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
            numero.isBlank()
        ) {
            Log.e(
                "CrediPlus",
                "Faltan datos de la solicitud"
            )
            return
        }

        if (!Settings.canDrawOverlays(context)) {
            return
        }

        val preferencias =
            context.getSharedPreferences(
                "crediplus_authenticator",
                Context.MODE_PRIVATE
            )

        preferencias
            .edit()
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
            .apply()

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

        CrediPlusOverlayLauncher.abrir(
            context
        )
    }
}