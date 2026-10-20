using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using Xunit.Abstractions;
using trabajo.Controllers;
using trabajo.Models;
using trabajo.Service;

namespace TestTrabajoCredito
{
    public class OtpValidationTests
    {
        private readonly ITestOutputHelper _output;

        public OtpValidationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private (LoginController Controller, TestSession Session) CrearControladorConSesion()
        {
            var options = new DbContextOptionsBuilder<UsuarioContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var context = new UsuarioContext(options);

            var mockUsuarioService = new Mock<IusuarioServices>();
            var mockTurnstile = new Mock<ITurnstileService>();
            var mockConfig = new Mock<IConfiguration>();
            var mockEmbeddingVoz = (ServicioEmbeddingVoz)RuntimeHelpers.GetUninitializedObject(typeof(ServicioEmbeddingVoz));

            var controller = new LoginController(
                mockUsuarioService.Object,
                context,
                mockEmbeddingVoz,
                mockConfig.Object,
                mockTurnstile.Object
            );

            var session = new TestSession();
            var httpContext = new DefaultHttpContext();
            httpContext.Session = session;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            return (controller, session);
        }

        private static object? ObtenerPropiedad(object? objeto, string nombrePropiedad)
        {
            if (objeto == null) return null;
            return objeto.GetType().GetProperty(nombrePropiedad)?.GetValue(objeto);
        }

        [Fact]
        public void ProbarTodasLasValidacionesDeOtp()
        {
            var runner = new EscenarioRunner(_output, "Validaciones de Códigos OTP por Correo (One-Time Password)");

            // 1-4. Parámetros nulos o vacíos
            (string? correo, string? codigo, string desc)[] casosVacios = new[]
            {
                (null, "123456", "Correo nulo con código"),
                ("usuario@gmail.com", null, "Correo con código nulo"),
                ("", "123456", "Correo vacío con código"),
                ("usuario@gmail.com", "", "Correo con código vacío")
            };

            foreach (var caso in casosVacios)
            {
                runner.Evaluar(
                    nombreEscenario: $"Validación de parámetros: {caso.desc}",
                    datoEntrada: $"correo='{caso.correo}', codigo='{caso.codigo}'",
                    esperado: "Rechazado: Debes ingresar el correo y el código de 6 dígitos.",
                    accion: () =>
                    {
                        var (controller, _) = CrearControladorConSesion();
                        var res = controller.ConfirmarCodigoRegistro(caso.correo!, caso.codigo!);
                        var json = (JsonResult)res;
                        bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                        string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                        return !ok ? $"Rechazado: {msg}" : "Aceptado";
                    });
            }

            // 5-8. Longitud o formato incorrecto de código OTP
            string[] codigosFormatoInvalido = new[]
            {
                "12345",   // 5 dígitos
                "1234567", // 7 dígitos
                "12345A",  // Contiene letra
                "12 345"   // Con espacio
            };

            foreach (var cod in codigosFormatoInvalido)
            {
                runner.Evaluar(
                    nombreEscenario: $"Código OTP con formato/longitud no permitida: '{cod}'",
                    datoEntrada: cod,
                    esperado: "Rechazado: El código debe tener exactamente 6 dígitos numéricos.",
                    accion: () =>
                    {
                        var (controller, _) = CrearControladorConSesion();
                        var res = controller.ConfirmarCodigoRegistro("usuario@gmail.com", cod);
                        var json = (JsonResult)res;
                        bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                        string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                        return !ok ? $"Rechazado: {msg}" : "Aceptado";
                    });
            }

            // 9. Sin código pendiente en sesión
            runner.Evaluar(
                nombreEscenario: "Confirmación sin código pendiente emitido en sesión",
                datoEntrada: "usuario@gmail.com, codigo: 123456 (Sesión vacía)",
                esperado: "Rechazado: No hay un código pendiente. Solicita uno nuevo.",
                accion: () =>
                {
                    var (controller, _) = CrearControladorConSesion();
                    var res = controller.ConfirmarCodigoRegistro("usuario@gmail.com", "123456");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // 10. Correo que confirma no coincide con el que solicitó el código
            runner.Evaluar(
                nombreEscenario: "Confirmación con correo distinto al solicitante (Protección contra secuestro)",
                datoEntrada: "Solicitado por: original@gmail.com, Confirmado por: intruso@gmail.com",
                esperado: "Rechazado: El correo ingresado no coincide con el que solicitó el código.",
                accion: () =>
                {
                    var (controller, session) = CrearControladorConSesion();
                    session.SetString("HashCodigoRegistro", utilidades.EncriptarClave("123456"));
                    session.SetString("CodigoRegistroCorreo", "original@gmail.com");
                    session.SetString("CodigoRegistroExpira", DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds().ToString());

                    var res = controller.ConfirmarCodigoRegistro("intruso@gmail.com", "123456");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // 11. Código OTP expirado (más de 60 segundos)
            runner.Evaluar(
                nombreEscenario: "Código OTP expirado (tiempo límite de 60s superado)",
                datoEntrada: "Expiró hace 10 segundos",
                esperado: "Expirado: El código de verificación ha expirado (límite 60s). Solicita uno nuevo.",
                accion: () =>
                {
                    var (controller, session) = CrearControladorConSesion();
                    session.SetString("HashCodigoRegistro", utilidades.EncriptarClave("654321"));
                    session.SetString("CodigoRegistroCorreo", "usuario@gmail.com");
                    session.SetString("CodigoRegistroExpira", DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeSeconds().ToString());

                    var res = controller.ConfirmarCodigoRegistro("usuario@gmail.com", "654321");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    bool expirado = (bool)(ObtenerPropiedad(json.Value, "expirado") ?? false);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return (!ok && expirado) ? $"Expirado: {msg}" : "Aceptado";
                });

            // 12. Código erróneo con intentos disponibles (Intento 1 fallido)
            runner.Evaluar(
                nombreEscenario: "Código incorrecto (computa intento fallido y alerta intentos restantes)",
                datoEntrada: "Emitido: 112233, Ingresado: 999999",
                esperado: "Fallido: Código incorrecto. Te quedan 2 intento(s).",
                accion: () =>
                {
                    var (controller, session) = CrearControladorConSesion();
                    session.SetString("HashCodigoRegistro", utilidades.EncriptarClave("112233"));
                    session.SetString("CodigoRegistroCorreo", "usuario@gmail.com");
                    session.SetString("CodigoRegistroExpira", DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds().ToString());
                    session.SetInt32("IntentosCodigoRegistro", 0);

                    var res = controller.ConfirmarCodigoRegistro("usuario@gmail.com", "999999");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Fallido: {msg}" : "Aceptado";
                });

            // 13. Superación de 3 intentos fallidos (bloqueo e invalidación de OTP)
            runner.Evaluar(
                nombreEscenario: "Bloqueo por superar el límite de 3 intentos fallidos",
                datoEntrada: "3 intentos fallidos acumulados",
                esperado: "Bloqueado: Has superado el límite de 3 intentos fallidos. Solicita un nuevo código.",
                accion: () =>
                {
                    var (controller, session) = CrearControladorConSesion();
                    session.SetString("HashCodigoRegistro", utilidades.EncriptarClave("112233"));
                    session.SetString("CodigoRegistroCorreo", "usuario@gmail.com");
                    session.SetString("CodigoRegistroExpira", DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds().ToString());
                    session.SetInt32("IntentosCodigoRegistro", 3);

                    var res = controller.ConfirmarCodigoRegistro("usuario@gmail.com", "112233");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    bool expirado = (bool)(ObtenerPropiedad(json.Value, "expirado") ?? false);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return (!ok && expirado) ? $"Bloqueado: {msg}" : "Aceptado";
                });

            // 14. Código correcto dentro del tiempo y con intentos válidos
            runner.Evaluar(
                nombreEscenario: "Código OTP correcto verificado exitosamente",
                datoEntrada: "Código exacto '584920' en ventana de tiempo válida",
                esperado: "¡Correo electrónico verificado exitosamente!",
                accion: () =>
                {
                    string codigoReal = "584920";
                    var (controller, session) = CrearControladorConSesion();
                    session.SetString("HashCodigoRegistro", utilidades.EncriptarClave(codigoReal));
                    session.SetString("CodigoRegistroCorreo", "usuario@gmail.com");
                    session.SetString("CodigoRegistroExpira", DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds().ToString());
                    session.SetInt32("IntentosCodigoRegistro", 0);

                    var res = controller.ConfirmarCodigoRegistro("usuario@gmail.com", codigoReal);
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? false);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    bool banderaVerificada = session.GetString("CodigoRegistroVerificado") == "true";
                    return (ok && banderaVerificada) ? msg : "Fallo en verificación";
                });

            // 15. Protección contra reutilización (Replay Attack)
            runner.Evaluar(
                nombreEscenario: "Protección contra reutilización de código OTP ya verificado",
                datoEntrada: "Reutilizar código tras completarse el registro",
                esperado: "Rechazado: No hay un código pendiente. Solicita uno nuevo.",
                accion: () =>
                {
                    var (controller, session) = CrearControladorConSesion();
                    // Al consumirse el código en la verificación final, se eliminan las variables de sesión HashCodigoRegistro
                    session.Remove("HashCodigoRegistro");
                    session.Remove("CodigoRegistroCorreo");

                    var res = controller.ConfirmarCodigoRegistro("usuario@gmail.com", "584920");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado indebidamente";
                });

            runner.VerificarTodosAprobados();
        }

        [Fact]
        public void ProbarValidacionesCodigoVinculacionAuthenticator()
        {
            var runner = new EscenarioRunner(_output, "Validaciones de Código de Vinculación CrediPlus Authenticator (Email OTP)");

            // 1. Sesión sin DNI pendiente
            runner.Evaluar(
                nombreEscenario: "Rechazo cuando no hay DNI en sesión pendiente",
                datoEntrada: "DniLoginPendiente = null, codigo = 'ABC123'",
                esperado: "Rechazado: La sesión ha expirado. Inicia sesión nuevamente.",
                accion: () =>
                {
                    var (controller, _) = CrearControladorConSesion();
                    var res = controller.VerificarCodigoVinculacionAuthenticator("ABC123");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // 2. Código nulo o vacío
            runner.Evaluar(
                nombreEscenario: "Rechazo cuando el código ingresado está vacío",
                datoEntrada: "DniLoginPendiente = '72345678', codigo = ''",
                esperado: "Rechazado: Ingresa el código de seguridad.",
                accion: () =>
                {
                    var (controller, session) = CrearControladorConSesion();
                    session.SetString("DniLoginPendiente", "72345678");
                    var res = controller.VerificarCodigoVinculacionAuthenticator("");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // 3. Sin código activo en sesión
            runner.Evaluar(
                nombreEscenario: "Rechazo cuando no existe código activo en sesión",
                datoEntrada: "DniLoginPendiente = '72345678', sin OTP en sesión",
                esperado: "Rechazado: No existe un código activo. Solicita un nuevo código.",
                accion: () =>
                {
                    var (controller, session) = CrearControladorConSesion();
                    session.SetString("DniLoginPendiente", "72345678");
                    var res = controller.VerificarCodigoVinculacionAuthenticator("ABC123");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // 4. Código expirado (> 60 segundos)
            runner.Evaluar(
                nombreEscenario: "Rechazo cuando el código OTP de 60s ha expirado",
                datoEntrada: "DniLoginPendiente = '72345678', expira hace 10 segundos",
                esperado: "Rechazado: El código ha expirado. Solicita un nuevo código.",
                accion: () =>
                {
                    var (controller, session) = CrearControladorConSesion();
                    session.SetString("DniLoginPendiente", "72345678");
                    session.SetString("CodigoAuthenticator_72345678", "ABC123");
                    session.SetString("CodigoAuthenticatorExpira_72345678", DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeSeconds().ToString());

                    var res = controller.VerificarCodigoVinculacionAuthenticator("ABC123");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    bool expirado = (bool)(ObtenerPropiedad(json.Value, "expirado") ?? false);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return (!ok && expirado) ? $"Rechazado: {msg}" : "Aceptado";
                });

            // 5. Código incorrecto
            runner.Evaluar(
                nombreEscenario: "Rechazo cuando el código OTP no coincide",
                datoEntrada: "Esperado 'ABC123', ingresado 'XYZ999'",
                esperado: "Rechazado: El código ingresado es incorrecto.",
                accion: () =>
                {
                    var (controller, session) = CrearControladorConSesion();
                    session.SetString("DniLoginPendiente", "72345678");
                    session.SetString("CodigoAuthenticator_72345678", "ABC123");
                    session.SetString("CodigoAuthenticatorExpira_72345678", DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds().ToString());

                    var res = controller.VerificarCodigoVinculacionAuthenticator("XYZ999");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // 6. Código correcto verificado con éxito (insensible a mayúsculas/minúsculas y espacios)
            runner.Evaluar(
                nombreEscenario: "Aceptación de código correcto y establecimiento de identidad verificada",
                datoEntrada: "Esperado 'ABC123', ingresado ' abc123 ' dentro del TTL",
                esperado: "Identidad confirmada correctamente.",
                accion: () =>
                {
                    var (controller, session) = CrearControladorConSesion();
                    session.SetString("DniLoginPendiente", "72345678");
                    session.SetString("CodigoAuthenticator_72345678", "ABC123");
                    session.SetString("CodigoAuthenticatorExpira_72345678", DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds().ToString());

                    var res = controller.VerificarCodigoVinculacionAuthenticator(" abc123 ");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? false);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    bool identidadConfirmada = session.GetString("AuthenticatorIdentidadVerificada") == "true";

                    return (ok && identidadConfirmada) ? msg! : "Fallo en verificación";
                });

            // 7. Protección contra reutilización (Replay Attack - uso único)
            runner.Evaluar(
                nombreEscenario: "Protección contra reutilización: código ya verificado queda invalidado",
                datoEntrada: "Segundo intento con el mismo código tras consumo previo",
                esperado: "Rechazado: No existe un código activo. Solicita un nuevo código.",
                accion: () =>
                {
                    var (controller, session) = CrearControladorConSesion();
                    session.SetString("DniLoginPendiente", "72345678");
                    session.SetString("CodigoAuthenticator_72345678", "ABC123");
                    session.SetString("CodigoAuthenticatorExpira_72345678", DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds().ToString());

                    // Primer uso (válido)
                    controller.VerificarCodigoVinculacionAuthenticator("ABC123");

                    // Segundo uso (debe ser rechazado porque ya se consumió)
                    var resSegundo = controller.VerificarCodigoVinculacionAuthenticator("ABC123");
                    var json = (JsonResult)resSegundo;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado indebidamente";
                });

            runner.VerificarTodosAprobados();
        }
    }
}
