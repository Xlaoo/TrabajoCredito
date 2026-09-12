package com.crediplus.crediplus_authenticator

import android.app.Activity
import android.content.Intent
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.widget.Toast
import flutter.overlay.window.flutter_overlay_window.OverlayService
import java.lang.ref.WeakReference

class CrediPlusGuardActivity : Activity() {

    companion object {

        @Volatile
        var biometriaEnCurso = false

        @Volatile
        var finalizado = false

        private var instancia:
                WeakReference<CrediPlusGuardActivity>? = null

        fun cerrarGuardia() {

            val activity =
                instancia?.get() ?: return

            activity.runOnUiThread {
                if (!activity.isFinishing) {
                    activity.finishAndRemoveTask()
                }
            }
        }
    }

    private val handler =
        Handler(Looper.getMainLooper())

    private val comprobarSalida =
        Runnable {

            if (
                !biometriaEnCurso &&
                !finalizado &&
                !isFinishing
            ) {

                finalizado = true

                stopService(
                    Intent(
                        this,
                        OverlayService::class.java
                    )
                )

                Toast.makeText(
                    applicationContext,
                    "✕ ANULADO",
                    Toast.LENGTH_SHORT
                ).show()

                finishAndRemoveTask()
            }
        }

    override fun onCreate(
        savedInstanceState: Bundle?
    ) {
        super.onCreate(savedInstanceState)

        instancia = WeakReference(this)

        biometriaEnCurso = false
        finalizado = false

        // No oscurece ni muestra otra pantalla.
        window.setDimAmount(0f)
    }

    override fun onResume() {
        super.onResume()

        handler.removeCallbacks(
            comprobarSalida
        )
    }

    override fun onPause() {
        super.onPause()

        handler.removeCallbacks(
            comprobarSalida
        )

        /*
         * ○ Inicio y □ Recientes provocan
         * que esta Activity pierda actividad.
         *
         * Damos un pequeño margen porque
         * la seguridad biométrica también
         * abre otra pantalla del sistema.
         */
        handler.postDelayed(
            comprobarSalida,
            700
        )
    }

    override fun onBackPressed() {

        if (!finalizado) {

            finalizado = true

            stopService(
                Intent(
                    this,
                    OverlayService::class.java
                )
            )

            Toast.makeText(
                applicationContext,
                "✕ ANULADO",
                Toast.LENGTH_SHORT
            ).show()
        }

        finishAndRemoveTask()
    }

    override fun onDestroy() {

        handler.removeCallbacks(
            comprobarSalida
        )

        if (instancia?.get() === this) {
            instancia = null
        }

        super.onDestroy()
    }
}