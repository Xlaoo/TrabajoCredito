package flutter.overlay.window.flutter_overlay_window

import android.content.Context
import android.content.Intent
import android.os.Build
import android.view.Gravity
import android.view.WindowManager

object CrediPlusOverlayLauncher {

    fun abrir(context: Context) {

        // ANCHO: MATCH_PARENT
        val displayMetrics =
            context.resources.displayMetrics

        WindowSetup.width =
            displayMetrics.widthPixels

        WindowSetup.height =
            displayMetrics.heightPixels + 650
        WindowSetup.gravity = Gravity.FILL

        // Permite que el overlay reciba foco.
        // Esto es necesario para que salga el teclado.
        WindowSetup.flag =
            WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL

        WindowSetup.gravity = Gravity.CENTER

        WindowSetup.enableDrag = false

        WindowSetup.overlayTitle =
            "CrediPlus Authenticator"

        WindowSetup.overlayContent =
            "Solicitud de inicio de sesión"

        WindowSetup.positionGravity = "none"

        val overlayIntent =
            Intent(
                context,
                OverlayService::class.java
            )

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
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