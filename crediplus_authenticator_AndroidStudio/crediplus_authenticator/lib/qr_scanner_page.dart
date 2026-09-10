import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

class QrScannerPage extends StatefulWidget {
  const QrScannerPage({super.key});

  @override
  State<QrScannerPage> createState() => _QrScannerPageState();
}

class _QrScannerPageState extends State<QrScannerPage> {
  final MobileScannerController _controller =
  MobileScannerController();

  bool _codigoDetectado = false;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _procesarCodigo(BarcodeCapture capture) {
    if (_codigoDetectado) {
      return;
    }

    if (capture.barcodes.isEmpty) {
      return;
    }

    final String? contenido =
        capture.barcodes.first.rawValue;

    if (contenido == null || contenido.isEmpty) {
      return;
    }

    _codigoDetectado = true;

    Navigator.pop(
      context,
      contenido,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,

      appBar: AppBar(
        backgroundColor: Colors.black,
        foregroundColor: Colors.white,
        elevation: 0,
        title: const Text(
          'Escanear código QR',
          style: TextStyle(
            fontWeight: FontWeight.w700,
          ),
        ),
        actions: [
          IconButton(
            onPressed: () {
              _controller.toggleTorch();
            },
            icon: const Icon(
              Icons.flashlight_on_outlined,
            ),
          ),
          IconButton(
            onPressed: () {
              _controller.switchCamera();
            },
            icon: const Icon(
              Icons.cameraswitch_outlined,
            ),
          ),
        ],
      ),

      body: Stack(
        children: [
          MobileScanner(
            controller: _controller,
            onDetect: _procesarCodigo,
          ),

          Center(
            child: Container(
              width: 270,
              height: 270,
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(28),
                border: Border.all(
                  color: Colors.white,
                  width: 3,
                ),
              ),
            ),
          ),

          const Positioned(
            left: 30,
            right: 30,
            bottom: 70,
            child: Column(
              children: [
                Icon(
                  Icons.qr_code_scanner_rounded,
                  color: Colors.white,
                  size: 34,
                ),
                SizedBox(height: 14),
                Text(
                  'Escanea el código QR de CrediPlus',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 18,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                SizedBox(height: 8),
                Text(
                  'Coloca el código QR dentro del recuadro',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: Colors.white70,
                    fontSize: 14,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}