package flutter.overlay.window.flutter_overlay_window

import android.content.Context
import android.content.Intent
import android.os.Build
import android.util.Log
import android.view.Gravity
import android.view.WindowManager
import io.flutter.embedding.engine.FlutterEngineCache

object CrediPlusOverlayLauncher {

    fun abrir(context: Context) {

        // ==========================================
        // DESTRUIR ENGINE ANTERIOR
        // ==========================================

        val engineAnterior =
            FlutterEngineCache
                .getInstance()
                .get("myCachedEngine")

        if (engineAnterior != null) {

            try {

                FlutterEngineCache
                    .getInstance()
                    .remove("myCachedEngine")

                engineAnterior.destroy()

                Log.d(
                    "CrediPlus",
                    "FlutterEngine anterior destruido"
                )

            } catch (e: Exception) {

                Log.e(
                    "CrediPlus",
                    "Error destruyendo FlutterEngine anterior",
                    e
                )
            }
        }


        // ==========================================
        // CONFIGURAR OVERLAY
        // ==========================================

        val displayMetrics =
            context.resources.displayMetrics

        WindowSetup.width =
            displayMetrics.widthPixels

        // NO CAMBIAR:
        // esta altura ya está correcta
        WindowSetup.height =
            displayMetrics.heightPixels + 650

        WindowSetup.gravity =
            Gravity.CENTER

        WindowSetup.flag =
            WindowManager.LayoutParams
                .FLAG_NOT_TOUCH_MODAL

        WindowSetup.enableDrag =
            false

        WindowSetup.overlayTitle =
            "CrediPlus Authenticator"

        WindowSetup.overlayContent =
            "Solicitud de inicio de sesión"

        WindowSetup.positionGravity =
            "none"


        // ==========================================
        // ABRIR OVERLAY NUEVO
        // ==========================================

        val overlayIntent =
            Intent(
                context,
                OverlayService::class.java
            )

        if (
            Build.VERSION.SDK_INT >=
            Build.VERSION_CODES.O
        ) {

            context.startForegroundService(
                overlayIntent
            )

        } else {

            context.startService(
                overlayIntent
            )
        }
    }
}