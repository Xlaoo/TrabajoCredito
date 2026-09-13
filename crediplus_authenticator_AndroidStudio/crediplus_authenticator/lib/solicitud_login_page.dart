import 'package:flutter/material.dart';
import 'package:flutter_overlay_window/flutter_overlay_window.dart';
import 'package:flutter/services.dart';
import 'package:android_intent_plus/android_intent.dart';
import 'dart:async';
class SolicitudLoginOverlay extends StatefulWidget {
  const SolicitudLoginOverlay({
    super.key,
    required this.numeroVerificacion,
    required this.solicitudId,
  });

  final String numeroVerificacion;
  final String solicitudId;

  @override
  State<SolicitudLoginOverlay> createState() =>
      _SolicitudLoginOverlayState();
}

class _SolicitudLoginOverlayState
    extends State<SolicitudLoginOverlay> {

  final TextEditingController _numeroController =
      TextEditingController();

  final FocusNode _numeroFocusNode =
      FocusNode();

  String estado = '';

bool _cerrandoSolicitud = false;
bool _abriendoSeguridad = false;
Timer? _temporizadorSolicitud;
StreamSubscription?
    _overlaySubscription;
int _segundosRestantes = 30;
@override
void initState() {
  super.initState();

  _reiniciarEstadoCompleto(
    iniciarTemporizador: true,
  );

  _overlaySubscription =
      FlutterOverlayWindow
          .overlayListener
          .listen(
    (event) {

      if (!mounted) {
        return;
      }

      if (
          event ==
          'NUEVA_SOLICITUD'
      ) {

        _reiniciarEstadoCompleto(
          iniciarTemporizador: true,
        );

        _abrirTecladoAutomaticamente();
      }
    },
  );

  _abrirTecladoAutomaticamente();
}
void _reiniciarEstadoCompleto({
  bool iniciarTemporizador = false,
}) {

  _temporizadorSolicitud?.cancel();

  _numeroController.clear();

  _cerrandoSolicitud = false;

  _abriendoSeguridad = false;

  if (mounted) {

    setState(() {
      estado = '';
      _segundosRestantes = 30;
    });

  } else {

    estado = '';
    _segundosRestantes = 30;
  }

  if (
      iniciarTemporizador &&
      mounted
  ) {

    _iniciarTemporizadorSolicitud();
  }
}
Future<void> _abrirTecladoAutomaticamente() async {
  await Future.delayed(
    const Duration(milliseconds: 500),
  );

  if (!mounted) return;

  FocusScope.of(context).requestFocus(
    _numeroFocusNode,
  );

  await SystemChannels.textInput.invokeMethod(
    'TextInput.show',
  );

  // Segundo intento porque el overlay puede tardar
  // un poco en obtener el foco de Android.
  await Future.delayed(
    const Duration(milliseconds: 500),
  );

  if (!mounted) return;

  _numeroFocusNode.requestFocus();

  await SystemChannels.textInput.invokeMethod(
    'TextInput.show',
  );

  // Último intento por algunos Xiaomi/Android.
  await Future.delayed(
    const Duration(milliseconds: 500),
  );

  if (!mounted) return;

_numeroFocusNode.requestFocus();

await SystemChannels.textInput.invokeMethod(
  'TextInput.show',
);
}
void _iniciarTemporizadorSolicitud() {

  _temporizadorSolicitud?.cancel();

  _segundosRestantes = 30;

  if (mounted) {
    setState(() {
      _segundosRestantes = 30;
    });
  }

  _temporizadorSolicitud =
      Timer.periodic(
    const Duration(seconds: 1),
    (timer) async {

      if (!mounted) {
        timer.cancel();
        return;
      }

if (
    _abriendoSeguridad ||
    _cerrandoSolicitud
) {
  return;
}

      if (_segundosRestantes <= 1) {

        timer.cancel();

        setState(() {
          _segundosRestantes = 0;
          estado = 'expirado';
        });

        _numeroController.clear();

        _cerrandoSolicitud = true;

        FocusScope.of(context).unfocus();

        await SystemChannels
            .textInput
            .invokeMethod(
          'TextInput.hide',
        );

        final intent =
            AndroidIntent(
          action:
              'com.crediplus.crediplus_authenticator.RESULTADO',
          componentName:
              'com.crediplus.crediplus_authenticator.CrediPlusResultReceiver',
          package:
              'com.crediplus.crediplus_authenticator',
          arguments: <String, dynamic>{
            'resultado': 'EXPIRADO',
          },
        );

        await intent.sendBroadcast();

        /*
         * IMPORTANTE:
         * dejar estado preparado para
         * la siguiente solicitud.
         */

        await Future.delayed(
          const Duration(
            milliseconds: 300,
          ),
        );

        await FlutterOverlayWindow
            .closeOverlay();

        return;
      }

      setState(() {
        _segundosRestantes--;
      });
    },
  );
}
@override
void dispose() {

  _temporizadorSolicitud?.cancel();

  _overlaySubscription?.cancel();

  _numeroController.dispose();

  _numeroFocusNode.dispose();

  super.dispose();
}
Future<void> _abrirSeguridadAndroid() async {
  const intent = AndroidIntent(
    componentName:
        'com.crediplus.crediplus_authenticator.CrediPlusBiometricActivity',
    package: 'com.crediplus.crediplus_authenticator',
    flags: <int>[
      0x10000000,
    ],
  );

  await intent.launch();
}

Future<void> _continuar() async {

  if (
      _cerrandoSolicitud ||
      _abriendoSeguridad
  ) {
    return;
  }

  final numeroIngresado =
      _numeroController.text.trim();

  if (
      numeroIngresado.length != 2
  ) {

    setState(() {
      estado = 'vacio';
    });

    return;
  }

  /*
   * Pausar contador mientras Android
   * verifica número + biometría.
   */
  _abriendoSeguridad = true;

  FocusScope.of(context).unfocus();

  await SystemChannels
      .textInput
      .invokeMethod(
    'TextInput.hide',
  );

  final intent =
      AndroidIntent(
    action:
        'com.crediplus.crediplus_authenticator.RESULTADO',
    componentName:
        'com.crediplus.crediplus_authenticator.CrediPlusResultReceiver',
    package:
        'com.crediplus.crediplus_authenticator',
    arguments: <String, dynamic>{
      'resultado': 'VERIFICAR_NUMERO',
      'numeroIngresado':
          numeroIngresado,
    },
  );

  await intent.sendBroadcast();

  /*
   * Borrar visualmente el número.
   */
  _numeroController.clear();
}

Future<void> _cerrar() async {
  await _anularSolicitud();
}
Future<void> _anularSolicitud() async {

  if (_cerrandoSolicitud) {
    return;
  }

  _cerrandoSolicitud = true;

  _temporizadorSolicitud?.cancel();

  _numeroController.clear();

  FocusScope.of(context).unfocus();

  await SystemChannels
      .textInput
      .invokeMethod(
    'TextInput.hide',
  );

  const intent =
      AndroidIntent(
    action:
        'com.crediplus.crediplus_authenticator.RESULTADO',
    componentName:
        'com.crediplus.crediplus_authenticator.CrediPlusResultReceiver',
    package:
        'com.crediplus.crediplus_authenticator',
    arguments: <String, dynamic>{
      'resultado': 'ANULADO',
    },
  );

  await intent.sendBroadcast();
}
  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, result) async {
        if (didPop) return;

        await _anularSolicitud();
      },
      child: Material(
        color: Colors.black.withOpacity(0.45),
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        child: Center(
        child: Container(
          width: MediaQuery.of(context).size.width * 0.95,
          padding: const EdgeInsets.all(22),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius:
                BorderRadius.circular(26),
            boxShadow: const [
              BoxShadow(
                color: Color(0x33000000),
                blurRadius: 25,
              ),
            ],
          ),
          child: Column(
            mainAxisSize:
                MainAxisSize.min,
            children: [
              Row(
                children: [
                  const Expanded(
                    child: Text(
                      'CrediPlus',
                      style: TextStyle(
                        fontSize: 20,
                        fontWeight:
                            FontWeight.w800,
                      ),
                    ),
                  ),
                  IconButton(
                    onPressed: _cerrar,
                    icon:
                        const Icon(Icons.close),
                  ),
                ],
              ),

              const SizedBox(height: 8),

              Container(
                width: 72,
                height: 72,
                decoration: BoxDecoration(
                  color:
                      const Color(0xFFEAF2FF),
                  borderRadius:
                      BorderRadius.circular(22),
                ),
                child: const Icon(
                  Icons.shield_rounded,
                  size: 42,
                  color:
                      Color(0xFF0F4C81),
                ),
              ),

              const SizedBox(height: 18),

              const Text(
                '¿Estás intentando iniciar sesión?',
                textAlign:
                    TextAlign.center,
                style: TextStyle(
                  fontSize: 22,
                  fontWeight:
                      FontWeight.w800,
                ),
              ),

              const SizedBox(height: 10),

              const Text(
                'Se solicitó acceso a tu cuenta de CrediPlus.',
                textAlign:
                    TextAlign.center,
                style: TextStyle(
                  color:
                      Color(0xFF667085),
                  fontSize: 14,
                ),
              ),

              const SizedBox(height: 22),

const Text(
  'Número mostrado en tu PC',
  style: TextStyle(
    fontWeight: FontWeight.w700,
  ),
),

const SizedBox(height: 6),

Text(
  'La solicitud expira en '
  '$_segundosRestantes segundos',
  style: TextStyle(
    color:
        _segundosRestantes <= 10
            ? Colors.red
            : const Color(0xFF667085),
    fontSize: 13,
    fontWeight: FontWeight.w600,
  ),
),

const SizedBox(height: 12),

              SizedBox(
                width: 130,
                child: TextField(
                  controller: _numeroController,
                  focusNode: _numeroFocusNode,
                  autofocus: true,

                  keyboardType: TextInputType.number,

                  textInputAction: TextInputAction.done,

                  textAlign: TextAlign.center,

                  maxLength: 2,

                  onSubmitted: (_) {
                    _continuar();
                  },

                  onChanged: (_) {
                    if (
                      estado == 'incorrecto' ||
                      estado == 'vacio'
                    ) {
                      setState(() {
                        estado = '';
                      });
                    }
                  },

                  style: const TextStyle(
                    fontSize: 30,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 7,
                  ),

                  decoration: InputDecoration(
                    counterText: '',
                    hintText: '__',
                    filled: true,
                    fillColor: const Color(0xFFF9FAFB),

                    border: OutlineInputBorder(
                      borderRadius:
                          BorderRadius.circular(14),
                    ),
                  ),
                ),
              ),

              const SizedBox(height: 14),

              if (estado == 'aprobado')
                Container(
                  width:
                      double.infinity,
                  padding:
                      const EdgeInsets.all(12),
                  decoration:
                      BoxDecoration(
                    color:
                        const Color(
                      0xFFE8F5E9,
                    ),
                    borderRadius:
                        BorderRadius.circular(12),
                  ),
                  child:
                      const Row(
                    mainAxisAlignment:
                        MainAxisAlignment.center,
                    children: [
                      Icon(
                        Icons
                            .check_circle_rounded,
                        color:
                            Colors.green,
                      ),
                      SizedBox(width: 8),
                      Text(
                        'APROBADO',
                        style:
                            TextStyle(
                          color:
                              Colors.green,
                          fontWeight:
                              FontWeight.w800,
                        ),
                      ),
                    ],
                  ),
                ),

              if (estado == 'incorrecto')
                Container(
                  width:
                      double.infinity,
                  padding:
                      const EdgeInsets.all(12),
                  decoration:
                      BoxDecoration(
                    color:
                        const Color(
                      0xFFFFEBEE,
                    ),
                    borderRadius:
                        BorderRadius.circular(12),
                  ),
                  child:
                      const Row(
                    mainAxisAlignment:
                        MainAxisAlignment.center,
                    children: [
                      Icon(
                        Icons.cancel_rounded,
                        color: Colors.red,
                      ),
                      SizedBox(width: 8),
                      Text(
                        'NÚMERO INCORRECTO',
                        style:
                            TextStyle(
                          color:
                              Colors.red,
                          fontWeight:
                              FontWeight.w800,
                        ),
                      ),
                    ],
                  ),
                ),
                if (estado == 'expirado')
                  Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: const Color(0xFFFFEBEE),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: const Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(
                          Icons.timer_off_rounded,
                          color: Colors.red,
                        ),
                        SizedBox(width: 8),
                        Text(
                          'SOLICITUD EXPIRADA',
                          style: TextStyle(
                            color: Colors.red,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ],
                    ),
                  ),
              if (estado == 'vacio')
                const Text(
                  'Ingresa el número mostrado en tu computadora',
                  textAlign:
                      TextAlign.center,
                  style: TextStyle(
                    color: Colors.red,
                    fontWeight:
                        FontWeight.w600,
                  ),
                ),

              const SizedBox(height: 18),

              SizedBox(
                width:
                    double.infinity,
                child: FilledButton(
                  onPressed: _continuar,
                  style:
                      FilledButton.styleFrom(
                    backgroundColor:
                        const Color(
                      0xFF0F4C81,
                    ),
                    minimumSize:
                        const Size.fromHeight(52),
                  ),
                  child:
                      const Text(
                    'CONTINUAR',
                    style:
                        TextStyle(
                      fontWeight:
                          FontWeight.w800,
                    ),
                  ),
                ),
              ),

               const SizedBox(height: 6),

TextButton.icon(
  onPressed: _cerrar,

  icon: const Icon(
    Icons.close_rounded,
    color: Colors.red,
  ),

  label: const Text(
    'CANCELAR SOLICITUD',
    style: TextStyle(
      color: Colors.red,
      fontWeight: FontWeight.w700,
    ),
  ),
),
                          ],
                        ), // Column
                      ), // Container
                    ), // Center
                  ), // GestureDetector
                ), // Material
              ); // PopScope
                }
              }