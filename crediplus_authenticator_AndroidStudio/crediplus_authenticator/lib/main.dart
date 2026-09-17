import 'dart:async';
import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:flutter/material.dart';
import 'package:otp/otp.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'qr_scanner_page.dart';
import 'package:flutter_overlay_window/flutter_overlay_window.dart';
import 'solicitud_login_page.dart';
import 'package:flutter/services.dart';
@pragma('vm:entry-point')
Future<void> _firebaseMessagingBackgroundHandler(
    RemoteMessage message,
    ) async {
  await Firebase.initializeApp();

  debugPrint(
    'Notificación recibida en segundo plano: ${message.messageId}',
  );
}
@pragma('vm:entry-point')
void overlayMain() {
  runApp(
    const MaterialApp(
      debugShowCheckedModeBanner: false,
      home: SolicitudLoginOverlay(
        numeroVerificacion: '',
        solicitudId: '',
      ),
    ),
  );
}
Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  await Firebase.initializeApp();

  FirebaseMessaging.onBackgroundMessage(
    _firebaseMessagingBackgroundHandler,
  );

  runApp(const CrediPlusAuthenticatorApp());
}

class CrediPlusAuthenticatorApp extends StatelessWidget {
  const CrediPlusAuthenticatorApp({super.key});

  @override
  Widget build(BuildContext context) {
    const primary = Color(0xFF0F4C81);
    const accent = Color(0xFF2F80ED);

    return MaterialApp(
      debugShowCheckedModeBanner: false,
      title: 'CrediPlus Authenticator',
      theme: ThemeData(
        useMaterial3: true,
        scaffoldBackgroundColor: const Color(0xFFF5F7FA),
        colorScheme: ColorScheme.fromSeed(
          seedColor: primary,
          primary: primary,
          secondary: accent,
          brightness: Brightness.light,
        ),
      ),
      home: const AuthenticatorPage(),
    );
  }
}

class AuthenticatorPage extends StatefulWidget {
  const AuthenticatorPage({super.key});

  @override
  State<AuthenticatorPage> createState() =>
      _AuthenticatorPageState();
}

class _AuthenticatorPageState
    extends State<AuthenticatorPage> {
  static const MethodChannel _channel =
  MethodChannel('crediplus/app_control');

  Future<void> _mandarAplicacionAlFondo() async {
    try {
      await _channel.invokeMethod('moveTaskToBack');
    } catch (e) {
      debugPrint('Error enviando app al fondo: $e');
    }
  }
  Future<void> _configurarNotificaciones() async {
    final FirebaseMessaging messaging =
        FirebaseMessaging.instance;

    final NotificationSettings settings =
    await messaging.requestPermission(
      alert: true,
      badge: true,
      sound: true,
    );

    debugPrint(
      'Permiso de notificaciones: ${settings.authorizationStatus}',
    );

    final String? token =
    await messaging.getToken();

    if (token != null &&
        token.trim().isNotEmpty) {

      _fcmToken = token.trim();

      debugPrint(
        'FCM TOKEN OBTENIDO: $_fcmToken',
      );
    }
  }
  Future<void> _configurarPermisoFlotante() async {

    final bool tienePermiso =
    await FlutterOverlayWindow.isPermissionGranted();

    if (!tienePermiso) {

      if (!mounted) return;

      final bool? aceptar =
      await showDialog<bool>(
        context: context,
        barrierDismissible: false,
        builder: (context) {
          return AlertDialog(
            title: const Text(
              'Permitir ventana flotante',
            ),
            content: const Text(
              'CrediPlus necesita permiso para mostrar las solicitudes '
                  'de inicio de sesión sobre otras aplicaciones.',
            ),
            actions: [
              TextButton(
                onPressed: () {
                  Navigator.pop(
                    context,
                    false,
                  );
                },
                child: const Text(
                  'AHORA NO',
                ),
              ),
              FilledButton(
                onPressed: () {
                  Navigator.pop(
                    context,
                    true,
                  );
                },
                child: const Text(
                  'PERMITIR',
                ),
              ),
            ],
          );
        },
      );

      if (aceptar == true) {
        await FlutterOverlayWindow.requestPermission();
      }
    }
  }
  void _escucharNotificaciones() {
    FirebaseMessaging.onMessageOpenedApp.listen(
          (RemoteMessage message) async {
        await _abrirOverlaySolicitud();

        await Future.delayed(
          const Duration(milliseconds: 300),
        );

        await _mandarAplicacionAlFondo();
      },
    );

    FirebaseMessaging.instance
        .getInitialMessage()
        .then((RemoteMessage? message) async {
      if (message != null) {
        await _abrirOverlaySolicitud();

        await Future.delayed(
          const Duration(milliseconds: 300),
        );

        await _mandarAplicacionAlFondo();
      }
    });
  }
  Future<void> _abrirOverlaySolicitud() async {
    bool permiso =
    await FlutterOverlayWindow.isPermissionGranted();

    if (!permiso) {
      final resultado =
      await FlutterOverlayWindow.requestPermission();

      permiso = resultado ?? false;
    }

    if (!permiso) {
      debugPrint(
        'No se concedió permiso para mostrar sobre otras aplicaciones',
      );
      return;
    }

    await FlutterOverlayWindow.showOverlay(
      height: WindowSize.fullCover,
      width: WindowSize.fullCover,
      alignment: OverlayAlignment.center,
      flag: OverlayFlag.focusPointer,
      enableDrag: false,
      overlayTitle: 'CrediPlus Authenticator',
      overlayContent: 'Solicitud de inicio de sesión',
    );
  }

  Timer? _timer;

  final FlutterSecureStorage _secureStorage =
  const FlutterSecureStorage();

  static const String _secretStorageKey =
      'crediplus_totp_secret';

  static const String _accountStorageKey =
      'crediplus_totp_account';

  int _segundosRestantes = 30;

  String _codigoTotp = '--- ---';

  String _secretTotp = '';

  String _nombreCuenta = '';
  String _fcmToken = '';
  bool _cargando = true;
  static const bool _usarBackendLocal = false;

  String get _backendBaseUrl {
    if (_usarBackendLocal) {
      return 'http://192.168.18.127:5280';
    }

    return 'https://p01--trabajocredito--bg88tvjkmfhg.code.run';
  }
  bool get _tieneCuenta =>
      _secretTotp.trim().isNotEmpty;

  @override
  void initState() {
    super.initState();

    _inicializarAuthenticator();

    WidgetsBinding.instance.addPostFrameCallback(
          (_) {
        _configurarPermisoFlotante();
      },
    );

    //_escucharNotificaciones();

    _timer = Timer.periodic(
      const Duration(seconds: 1),
          (_) => _actualizarTiempo(),
    );
  }

  Future<void> _inicializarAuthenticator() async {

    // 1. Pedir permisos y obtener FCM Token
    await _configurarNotificaciones();

    // 2. Cargar cuenta TOTP guardada
    await _cargarDatosGuardados();

    if (!mounted) return;

    // 3. Si ya existe una cuenta configurada,
    // registrar automáticamente este celular
    // en el backend.
    if (_secretTotp.trim().isNotEmpty) {

      debugPrint(
          'CUENTA GUARDADA: [$_nombreCuenta]'
      );

      final registrado =
      await _registrarDispositivoBackend();

      debugPrint(
        registrado
            ? 'DISPOSITIVO VINCULADO AUTOMÁTICAMENTE'
            : 'NO SE PUDO VINCULAR EL DISPOSITIVO AUTOMÁTICAMENTE',
      );
    }

    if (!mounted) return;

    _actualizarTiempo();
  }

  Future<void> _cargarDatosGuardados() async {
    try {
      final secretGuardado =
      await _secureStorage.read(
        key: _secretStorageKey,
      );

      final cuentaGuardada =
      await _secureStorage.read(
        key: _accountStorageKey,
      );

      if (!mounted) return;

      setState(() {
        _secretTotp =
            secretGuardado?.trim() ?? '';

        _nombreCuenta =
            cuentaGuardada?.trim() ?? '';

        _cargando = false;
      });

      if (_secretTotp.isNotEmpty) {
        debugPrint(
          'Secreto TOTP cargado desde almacenamiento seguro',
        );
      } else {
        debugPrint(
          'No existe una cuenta TOTP configurada',
        );
      }
    } catch (e) {
      debugPrint(
        'Error al cargar los datos TOTP: $e',
      );

      if (!mounted) return;

      setState(() {
        _cargando = false;
        _secretTotp = '';
        _nombreCuenta = '';
        _codigoTotp = '--- ---';
      });
    }
  }

  Future<void> _guardarCuenta({
    required String secret,
    required String cuenta,
  }) async {
    await _secureStorage.write(
      key: _secretStorageKey,
      value: secret,
    );

    await _secureStorage.write(
      key: _accountStorageKey,
      value: cuenta,
    );

    debugPrint(
      'Cuenta TOTP guardada de forma segura',
    );
  }

  void _actualizarTiempo() {
    if (!mounted) return;

    if (_secretTotp.isEmpty) {
      if (_codigoTotp != '--- ---') {
        setState(() {
          _codigoTotp = '--- ---';
          _segundosRestantes = 30;
        });
      }

      return;
    }

    try {
      final ahora =
          DateTime.now().millisecondsSinceEpoch;

      final segundosActuales =
          ahora ~/ 1000;

      final codigo =
      OTP.generateTOTPCodeString(
        _secretTotp,
        ahora,
        interval: 30,
        length: 6,
        algorithm: Algorithm.SHA1,
        isGoogle: true,
      );

      if (!mounted) return;

      setState(() {
        _segundosRestantes =
            30 - (segundosActuales % 30);

        _codigoTotp =
        '${codigo.substring(0, 3)} '
            '${codigo.substring(3, 6)}';
      });
    } catch (e) {
      debugPrint(
        'Error generando código TOTP: $e',
      );

      if (!mounted) return;

      setState(() {
        _codigoTotp = '--- ---';
      });
    }
  }

  String _obtenerCuentaDesdeQr(Uri uri) {
    String cuenta = '';

    try {
      final ruta =
      Uri.decodeComponent(uri.path);

      if (ruta.isNotEmpty) {
        final rutaLimpia =
        ruta.startsWith('/')
            ? ruta.substring(1)
            : ruta;

        if (rutaLimpia.contains(':')) {
          cuenta =
              rutaLimpia
                  .split(':')
                  .skip(1)
                  .join(':')
                  .trim();
        } else {
          cuenta = rutaLimpia.trim();
        }
      }
    } catch (_) {}

    if (cuenta.isEmpty) {
      cuenta = 'Cuenta principal';
    }

    return cuenta;
  }

  Future<void> _configurarCuenta({
    required String secret,
    required String cuenta,
  }) async {
    final secretLimpio = secret
        .trim()
        .replaceAll(' ', '')
        .toUpperCase();

    if (secretLimpio.isEmpty) {
      throw const FormatException(
        'La clave secreta está vacía',
      );
    }

    // Probamos el secreto antes de guardarlo.
    OTP.generateTOTPCodeString(
      secretLimpio,
      DateTime.now().millisecondsSinceEpoch,
      interval: 30,
      length: 6,
      algorithm: Algorithm.SHA1,
      isGoogle: true,
    );

    final cuentaLimpia =
    cuenta.trim().isEmpty
        ? 'Cuenta principal'
        : cuenta.trim();

    await _guardarCuenta(
      secret: secretLimpio,
      cuenta: cuentaLimpia,
    );

    if (!mounted) return;

    setState(() {
      _secretTotp = secretLimpio;
      _nombreCuenta = cuentaLimpia;
    });

    _actualizarTiempo();
  }
  Future<bool> _registrarDispositivoBackend() async {
    try {
      if (_nombreCuenta.trim().isEmpty) {
        debugPrint(
          'No existe correo para registrar el dispositivo.',
        );
        return false;
      }

      if (_secretTotp.trim().isEmpty) {
        debugPrint(
          'No existe secreto TOTP.',
        );
        return false;
      }

      String token = _fcmToken.trim();

      if (token.isEmpty) {
        final nuevoToken =
        await FirebaseMessaging.instance.getToken();

        if (nuevoToken == null ||
            nuevoToken.trim().isEmpty) {
          debugPrint(
            'No se pudo obtener el FCM Token.',
          );
          return false;
        }

        token = nuevoToken.trim();
        _fcmToken = token;
      }

      final codigoTotp =
      OTP.generateTOTPCodeString(
        _secretTotp,
        DateTime.now().millisecondsSinceEpoch,
        interval: 30,
        length: 6,
        algorithm: Algorithm.SHA1,
        isGoogle: true,
      );

      final respuesta =
      await http.post(
        Uri.parse(
          '$_backendBaseUrl/Login/RegistrarDispositivoAuthenticator',
        ),
        headers: {
          'Content-Type':
          'application/x-www-form-urlencoded',
        },
        body: {
          'correo': _nombreCuenta.trim(),
          'codigoTotp': codigoTotp,
          'fcmToken': token,
          'nombreDispositivo': 'Android',
        },
      );

      debugPrint(
        'HTTP REGISTRO DISPOSITIVO: ${respuesta.statusCode}',
      );

      debugPrint(
        'RESPUESTA REGISTRO: ${respuesta.body}',
      );

      if (respuesta.statusCode != 200) {
        return false;
      }

      final datos =
      jsonDecode(respuesta.body);

      return datos['ok'] == true;
    } catch (e) {
      debugPrint(
        'ERROR REGISTRANDO DISPOSITIVO: $e',
      );

      return false;
    }
  }
  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final progreso =
    _tieneCuenta
        ? _segundosRestantes / 30
        : 0.0;

    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            _buildHeader(),
            Expanded(
              child: SingleChildScrollView(
                padding:
                const EdgeInsets.fromLTRB(
                  20,
                  24,
                  20,
                  30,
                ),
                child: Column(
                  crossAxisAlignment:
                  CrossAxisAlignment.stretch,
                  children: [
                    _buildWelcome(),
                    const SizedBox(height: 24),
                    _buildCodeCard(progreso),
                    const SizedBox(height: 22),
                    _buildScanButton(),
                    const SizedBox(height: 12),
                    _buildManualButton(),
                    const SizedBox(height: 28),
                    _buildSecurityInfo(),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar:
      _buildBottomBar(),
    );
  }

  Widget _buildHeader() {
    return Container(
      padding: const EdgeInsets.fromLTRB(
        20,
        18,
        12,
        18,
      ),
      decoration: const BoxDecoration(
        color: Colors.white,
        boxShadow: [
          BoxShadow(
            color: Color(0x0D000000),
            blurRadius: 18,
            offset: Offset(0, 6),
          ),
        ],
      ),
      child: Row(
        children: [
          Container(
            width: 46,
            height: 46,
            decoration: BoxDecoration(
              color: const Color(0xFFEAF2FF),
              borderRadius: BorderRadius.circular(14),
            ),
            child: const Icon(
              Icons.shield_rounded,
              color: Color(0xFF0F4C81),
              size: 28,
            ),
          ),

          const SizedBox(width: 12),

          const Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'CrediPlus',
                  style: TextStyle(
                    fontSize: 21,
                    fontWeight: FontWeight.w800,
                    color: Color(0xFF101828),
                  ),
                ),
                SizedBox(height: 2),
                Text(
                  'Authenticator',
                  style: TextStyle(
                    fontSize: 14,
                    color: Color(0xFF667085),
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ],
            ),
          ),

          IconButton(
            onPressed: () {},
            icon: const Icon(
              Icons.notifications_none_rounded,
              color: Color(0xFF475467),
            ),
          ),
          IconButton(
            onPressed: () {},
            icon: const Icon(
              Icons.settings_outlined,
              color: Color(0xFF475467),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildWelcome() {
    return const Column(
      crossAxisAlignment:
      CrossAxisAlignment.start,
      children: [
        Text(
          'Tu acceso seguro',
          style: TextStyle(
            fontSize: 28,
            fontWeight:
            FontWeight.w800,
            color:
            Color(0xFF101828),
          ),
        ),
        SizedBox(height: 8),
        Text(
          'Genera códigos temporales para proteger tus cuentas y accesos.',
          style: TextStyle(
            fontSize: 15,
            height: 1.45,
            color:
            Color(0xFF667085),
          ),
        ),
      ],
    );
  }

  Widget _buildCodeCard(
      double progreso,
      ) {
    return Container(
      decoration: BoxDecoration(
        gradient:
        const LinearGradient(
          begin: Alignment.topLeft,
          end:
          Alignment.bottomRight,
          colors: [
            Color(0xFF123E68),
            Color(0xFF1E5F9E),
            Color(0xFF2F80ED),
          ],
        ),
        borderRadius:
        BorderRadius.circular(28),
        boxShadow: const [
          BoxShadow(
            color:
            Color(0x332F80ED),
            blurRadius: 28,
            offset:
            Offset(0, 14),
          ),
        ],
      ),
      padding:
      const EdgeInsets.all(24),
      child: Column(
        children: [
          Row(
            children: [
              Container(
                width: 52,
                height: 52,
                decoration:
                BoxDecoration(
                  color: Colors.white
                      .withOpacity(0.15),
                  borderRadius:
                  BorderRadius.circular(
                    16,
                  ),
                ),
                child: const Icon(
                  Icons
                      .account_balance_wallet_outlined,
                  color: Colors.white,
                  size: 28,
                ),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment:
                  CrossAxisAlignment
                      .start,
                  children: [
                    const Text(
                      'CrediPlus',
                      style: TextStyle(
                        color:
                        Colors.white,
                        fontSize: 20,
                        fontWeight:
                        FontWeight
                            .w800,
                      ),
                    ),
                    const SizedBox(
                      height: 3,
                    ),
                    Text(
                      _cargando
                          ? 'Cargando...'
                          : _tieneCuenta
                          ? (_nombreCuenta
                          .isEmpty
                          ? 'Cuenta principal'
                          : _nombreCuenta)
                          : 'Sin cuenta configurada',
                      style:
                      const TextStyle(
                        color:
                        Color(
                          0xFFD6E7FA,
                        ),
                        fontSize: 14,
                      ),
                    ),
                  ],
                ),
              ),
              Container(
                padding:
                const EdgeInsets
                    .symmetric(
                  horizontal: 10,
                  vertical: 6,
                ),
                decoration:
                BoxDecoration(
                  color: Colors.white
                      .withOpacity(0.14),
                  borderRadius:
                  BorderRadius.circular(
                    20,
                  ),
                ),
                child: Row(
                  children: [
                    Icon(
                      _tieneCuenta
                          ? Icons
                          .verified_user_outlined
                          : Icons
                          .shield_outlined,
                      color: Colors.white,
                      size: 16,
                    ),
                    const SizedBox(
                      width: 5,
                    ),
                    Text(
                      _tieneCuenta
                          ? 'Protegido'
                          : 'Sin configurar',
                      style:
                      const TextStyle(
                        color:
                        Colors.white,
                        fontSize: 12,
                        fontWeight:
                        FontWeight
                            .w600,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 34),
          const Text(
            'CÓDIGO DE VERIFICACIÓN',
            style: TextStyle(
              color:
              Color(0xFFD6E7FA),
              fontSize: 12,
              fontWeight:
              FontWeight.w700,
              letterSpacing: 1.2,
            ),
          ),
          const SizedBox(height: 12),
          Text(
            _cargando
                ? '... ...'
                : _codigoTotp,
            style: const TextStyle(
              color: Colors.white,
              fontSize: 52,
              fontWeight:
              FontWeight.w900,
              letterSpacing: 5,
            ),
          ),
          const SizedBox(height: 24),
          Container(
            padding:
            const EdgeInsets
                .symmetric(
              horizontal: 16,
              vertical: 12,
            ),
            decoration:
            BoxDecoration(
              color: Colors.white
                  .withOpacity(0.12),
              borderRadius:
              BorderRadius.circular(
                16,
              ),
            ),
            child: Row(
              children: [
                SizedBox(
                  width: 34,
                  height: 34,
                  child:
                  CircularProgressIndicator(
                    value:
                    _tieneCuenta
                        ? progreso
                        : 0,
                    strokeWidth: 4,
                    backgroundColor:
                    Colors.white
                        .withOpacity(
                      0.18,
                    ),
                    valueColor:
                    const AlwaysStoppedAnimation<
                        Color>(
                      Colors.white,
                    ),
                  ),
                ),
                const SizedBox(
                  width: 12,
                ),
                Expanded(
                  child: Text(
                    _cargando
                        ? 'Cargando cuenta...'
                        : _tieneCuenta
                        ? 'Nuevo código en $_segundosRestantes segundos'
                        : 'Agrega una cuenta para comenzar',
                    style:
                    const TextStyle(
                      color:
                      Colors.white,
                      fontSize: 14,
                      fontWeight:
                      FontWeight
                          .w600,
                    ),
                  ),
                ),
                Icon(
                  _tieneCuenta
                      ? Icons
                      .timer_outlined
                      : Icons
                      .add_circle_outline,
                  color: Colors.white,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildScanButton() {
    return FilledButton.icon(
      onPressed: () async {
        final resultado =
        await Navigator.push<String>(
          context,
          MaterialPageRoute(
            builder: (context) =>
            const QrScannerPage(),
          ),
        );

        if (!mounted) return;

        if (resultado == null ||
            resultado.isEmpty) {
          return;
        }

        debugPrint(
          'QR leído: $resultado',
        );

        try {
          final uri =
          Uri.parse(resultado);

          if (uri.scheme
              .toLowerCase() !=
              'otpauth' ||
              uri.host
                  .toLowerCase() !=
                  'totp') {
            throw const FormatException(
              'El QR no es un código TOTP válido',
            );
          }

          final secret =
          uri.queryParameters[
          'secret'];

          if (secret == null ||
              secret.trim().isEmpty) {
            throw const FormatException(
              'El QR no contiene una clave secreta',
            );
          }

          final cuenta =
          _obtenerCuentaDesdeQr(
            uri,
          );

          await _configurarCuenta(
            secret: secret,
            cuenta: cuenta,
          );

          final dispositivoRegistrado =
          await _registrarDispositivoBackend();

          debugPrint(
            dispositivoRegistrado
                ? 'DISPOSITIVO REGISTRADO CORRECTAMENTE'
                : 'NO SE PUDO REGISTRAR EL DISPOSITIVO',
          );

          if (!mounted) return;

          ScaffoldMessenger.of(
            context,
          ).showSnackBar(
            const SnackBar(
              content: Text(
                'Authenticator configurado y guardado correctamente',
              ),
              behavior:
              SnackBarBehavior
                  .floating,
              backgroundColor:
              Color(
                0xFF0F4C81,
              ),
            ),
          );
        } catch (e) {
          debugPrint(
            'Error procesando QR: $e',
          );

          if (!mounted) return;

          ScaffoldMessenger.of(
            context,
          ).showSnackBar(
            const SnackBar(
              content: Text(
                'El código QR no es un código TOTP válido',
              ),
              behavior:
              SnackBarBehavior
                  .floating,
              backgroundColor:
              Colors.red,
            ),
          );
        }
      },
      style:
      FilledButton.styleFrom(
        backgroundColor:
        const Color(
          0xFF0F4C81,
        ),
        foregroundColor:
        Colors.white,
        minimumSize:
        const Size.fromHeight(
          58,
        ),
        shape:
        RoundedRectangleBorder(
          borderRadius:
          BorderRadius.circular(
            18,
          ),
        ),
        elevation: 0,
      ),
      icon: const Icon(
        Icons
            .qr_code_scanner_rounded,
        size: 24,
      ),
      label: const Text(
        'Escanear código QR',
        style: TextStyle(
          fontSize: 16,
          fontWeight:
          FontWeight.w700,
        ),
      ),
    );
  }

  Widget _buildManualButton() {
    return OutlinedButton.icon(
      onPressed: () async {
        final cuentaController =
        TextEditingController();

        final secretController =
        TextEditingController();

        final resultado =
        await showDialog<bool>(
          context: context,
          builder: (context) {
            return AlertDialog(
              title: const Text(
                'Agregar cuenta',
                style: TextStyle(
                  fontWeight:
                  FontWeight.w800,
                ),
              ),
              content: Column(
                mainAxisSize:
                MainAxisSize.min,
                children: [
                  TextField(
                    controller:
                    cuentaController,
                    decoration:
                    const InputDecoration(
                      labelText:
                      'Nombre de cuenta',
                      hintText:
                      'Ejemplo: 87654321',
                      border:
                      OutlineInputBorder(),
                    ),
                  ),
                  const SizedBox(
                    height: 16,
                  ),
                  TextField(
                    controller:
                    secretController,
                    autocorrect: false,
                    enableSuggestions:
                    false,
                    textCapitalization:
                    TextCapitalization
                        .characters,
                    decoration:
                    const InputDecoration(
                      labelText:
                      'Clave secreta',
                      hintText:
                      'Ejemplo: NB2W45DFOIZA',
                      border:
                      OutlineInputBorder(),
                    ),
                  ),
                ],
              ),
              actions: [
                TextButton(
                  onPressed: () {
                    Navigator.pop(
                      context,
                      false,
                    );
                  },
                  child: const Text(
                    'CANCELAR',
                  ),
                ),
                FilledButton(
                  onPressed: () {
                    Navigator.pop(
                      context,
                      true,
                    );
                  },
                  child: const Text(
                    'AGREGAR',
                  ),
                ),
              ],
            );
          },
        );

        if (!mounted) return;

        if (resultado != true) {
          return;
        }

        final cuenta =
        cuentaController.text
            .trim();

        final secret =
        secretController.text
            .trim()
            .replaceAll(
          ' ',
          '',
        )
            .toUpperCase();

        if (secret.isEmpty) {
          ScaffoldMessenger.of(
            context,
          ).showSnackBar(
            const SnackBar(
              content: Text(
                'Debes ingresar una clave secreta',
              ),
              behavior:
              SnackBarBehavior
                  .floating,
              backgroundColor:
              Colors.red,
            ),
          );

          return;
        }

        try {
          await _configurarCuenta(
            secret: secret,
            cuenta: cuenta,
          );

          final dispositivoRegistrado =
          await _registrarDispositivoBackend();

          debugPrint(
            dispositivoRegistrado
                ? 'DISPOSITIVO REGISTRADO CORRECTAMENTE'
                : 'NO SE PUDO REGISTRAR EL DISPOSITIVO',
          );

          if (!mounted) return;

          ScaffoldMessenger.of(
            context,
          ).showSnackBar(
            const SnackBar(
              content: Text(
                'Cuenta agregada y guardada correctamente',
              ),
              behavior:
              SnackBarBehavior
                  .floating,
              backgroundColor:
              Color(
                0xFF0F4C81,
              ),
            ),
          );
        } catch (e) {
          debugPrint(
            'Error agregando clave manual: $e',
          );

          if (!mounted) return;

          ScaffoldMessenger.of(
            context,
          ).showSnackBar(
            const SnackBar(
              content: Text(
                'La clave secreta no es válida',
              ),
              behavior:
              SnackBarBehavior
                  .floating,
              backgroundColor:
              Colors.red,
            ),
          );
        }
      },
      style:
      OutlinedButton.styleFrom(
        minimumSize:
        const Size.fromHeight(
          56,
        ),
        side: const BorderSide(
          color:
          Color(
            0xFFD0D5DD,
          ),
        ),
        shape:
        RoundedRectangleBorder(
          borderRadius:
          BorderRadius.circular(
            18,
          ),
        ),
        foregroundColor:
        const Color(
          0xFF344054,
        ),
      ),
      icon: const Icon(
        Icons.key_rounded,
      ),
      label: const Text(
        'Agregar clave manualmente',
        style: TextStyle(
          fontSize: 15,
          fontWeight:
          FontWeight.w700,
        ),
      ),
    );
  }

  Widget _buildSecurityInfo() {
    return Container(
      padding:
      const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius:
        BorderRadius.circular(20),
        border: Border.all(
          color:
          const Color(
            0xFFE4E7EC,
          ),
        ),
      ),
      child: const Row(
        crossAxisAlignment:
        CrossAxisAlignment.start,
        children: [
          Icon(
            Icons
                .lock_outline_rounded,
            color:
            Color(
              0xFF0F4C81,
            ),
            size: 26,
          ),
          SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment:
              CrossAxisAlignment
                  .start,
              children: [
                Text(
                  'Tus códigos permanecen privados',
                  style:
                  TextStyle(
                    fontWeight:
                    FontWeight
                        .w800,
                    fontSize: 15,
                    color:
                    Color(
                      0xFF101828,
                    ),
                  ),
                ),
                SizedBox(height: 6),
                Text(
                  'Los códigos se generan en tu dispositivo y cambian automáticamente cada 30 segundos.',
                  style:
                  TextStyle(
                    fontSize: 14,
                    height: 1.4,
                    color:
                    Color(
                      0xFF667085,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBottomBar() {
    return NavigationBar(
      selectedIndex: 0,
      destinations: const [
        NavigationDestination(
          icon:
          Icon(
            Icons.shield_outlined,
          ),
          selectedIcon:
          Icon(
            Icons.shield_rounded,
          ),
          label: 'Códigos',
        ),
        NavigationDestination(
          icon:
          Icon(
            Icons
                .qr_code_2_outlined,
          ),
          label: 'Agregar',
        ),
        NavigationDestination(
          icon:
          Icon(
            Icons.person_outline,
          ),
          label: 'Cuenta',
        ),
      ],
    );
  }
}