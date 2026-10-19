using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
    public class DniValidationTests
    {
        private readonly ITestOutputHelper _output;

        public DniValidationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private LoginController CrearControlador(UsuarioContext context)
        {
            var mockUsuarioService = new Mock<IusuarioServices>();
            var mockTurnstile = new Mock<ITurnstileService>();
            mockTurnstile.Setup(t => t.ValidarTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new TurnstileVerificationResult { Valido = true });

            var mockConfig = new Mock<IConfiguration>();
            var mockEmbeddingVoz = (ServicioEmbeddingVoz)RuntimeHelpers.GetUninitializedObject(typeof(ServicioEmbeddingVoz));

            var controller = new LoginController(
                mockUsuarioService.Object,
                context,
                mockEmbeddingVoz,
                mockConfig.Object,
                mockTurnstile.Object
            );

            var httpContext = new DefaultHttpContext();
            httpContext.Request.ContentType = "application/x-www-form-urlencoded";
            httpContext.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());
            httpContext.Session = new TestSession();
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static object? ObtenerPropiedad(object? objeto, string nombrePropiedad)
        {
            if (objeto == null) return null;
            return objeto.GetType().GetProperty(nombrePropiedad)?.GetValue(objeto);
        }

        [Fact]
        public async Task ProbarTodasLasValidacionesDeDni()
        {
            var runner = new EscenarioRunner(_output, "Validaciones de Documento Nacional de Identidad (DNI)");

            var options = new DbContextOptionsBuilder<UsuarioContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new UsuarioContext(options);

            // Pre-poblar un usuario para probar duplicados
            context.Usuario.Add(new Usuario
            {
                Dni = "87654321",
                Nombre = "Carlos",
                Apellido = "Mendoza",
                Celular = "987654321",
                Correo = "carlos@gmail.com",
                Genero = "Masculino",
                clave = utilidades.EncriptarClave("Password#1234"),
                Rol = "Cliente"
            });
            await context.SaveChangesAsync();

            var controller = CrearControlador(context);

            // 1. DNI nulo
            await runner.EvaluarAsync(
                nombreEscenario: "DNI nulo en IniciarSesion",
                datoEntrada: "[null]",
                esperado: "Rechazado: El DNI debe tener exactamente 8 números.",
                accionAsync: async () =>
                {
                    var res = await controller.IniciarSesion(null!, "Clave#12345678");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // 2. DNI vacío
            await runner.EvaluarAsync(
                nombreEscenario: "DNI vacío en IniciarSesion",
                datoEntrada: "\"\"",
                esperado: "Rechazado: El DNI debe tener exactamente 8 números.",
                accionAsync: async () =>
                {
                    var res = await controller.IniciarSesion("", "Clave#12345678");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // 3. DNI con solo espacios
            await runner.EvaluarAsync(
                nombreEscenario: "DNI con solo espacios en blanco",
                datoEntrada: "\"   \"",
                esperado: "Rechazado: El DNI debe tener exactamente 8 números.",
                accionAsync: async () =>
                {
                    var res = await controller.IniciarSesion("   ", "Clave#12345678");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // 4. DNI de 7 dígitos (longitud insuficiente)
            await runner.EvaluarAsync(
                nombreEscenario: "DNI de 7 dígitos (menor al requerido)",
                datoEntrada: "1234567",
                esperado: "Rechazado: El DNI debe tener exactamente 8 números.",
                accionAsync: async () =>
                {
                    var res = await controller.IniciarSesion("1234567", "Clave#12345678");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // 5. DNI de 9 dígitos (longitud excedida)
            await runner.EvaluarAsync(
                nombreEscenario: "DNI de 9 dígitos (mayor al requerido)",
                datoEntrada: "123456789",
                esperado: "Rechazado: El DNI debe tener exactamente 8 números.",
                accionAsync: async () =>
                {
                    var res = await controller.IniciarSesion("123456789", "Clave#12345678");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // 6. DNI con letras
            await runner.EvaluarAsync(
                nombreEscenario: "DNI con caracteres alfabéticos",
                datoEntrada: "1234567A",
                esperado: "Rechazado: El DNI debe tener exactamente 8 números.",
                accionAsync: async () =>
                {
                    var res = await controller.IniciarSesion("1234567A", "Clave#12345678");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // 7. DNI con símbolos especiales (guion)
            await runner.EvaluarAsync(
                nombreEscenario: "DNI con símbolos o guiones",
                datoEntrada: "1234-567",
                esperado: "Rechazado: El DNI debe tener exactamente 8 números.",
                accionAsync: async () =>
                {
                    var res = await controller.IniciarSesion("1234-567", "Clave#12345678");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // 8. DNI con espacios intercalados
            await runner.EvaluarAsync(
                nombreEscenario: "DNI con espacios intercalados",
                datoEntrada: "1234 567",
                esperado: "Rechazado: El DNI debe tener exactamente 8 números.",
                accionAsync: async () =>
                {
                    var res = await controller.IniciarSesion("1234 567", "Clave#12345678");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // 9. DNI válido de 8 dígitos con ceros iniciales
            await runner.EvaluarAsync(
                nombreEscenario: "DNI válido con ceros iniciales (formato superado)",
                datoEntrada: "00123456",
                esperado: "Formato Válido (Pasa a verificación en BD)",
                accionAsync: async () =>
                {
                    var res = await controller.IniciarSesion("00123456", "Clave#12345678");
                    var json = (JsonResult)res;
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    // Al superar el formato 8 dígitos numéricos, pasa a buscar en BD
                    return msg == "No estás registrado. Primero debes crear una cuenta."
                        ? "Formato Válido (Pasa a verificación en BD)"
                        : $"Error de formato: {msg}";
                });

            // 10. DNI válido estándar de 8 dígitos
            await runner.EvaluarAsync(
                nombreEscenario: "DNI válido estándar de 8 dígitos (formato superado)",
                datoEntrada: "76543210",
                esperado: "Formato Válido (Pasa a verificación en BD)",
                accionAsync: async () =>
                {
                    var res = await controller.IniciarSesion("76543210", "Clave#12345678");
                    var json = (JsonResult)res;
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return msg == "No estás registrado. Primero debes crear una cuenta."
                        ? "Formato Válido (Pasa a verificación en BD)"
                        : $"Error de formato: {msg}";
                });

            // 11. DNI duplicado en Registro
            await runner.EvaluarAsync(
                nombreEscenario: "DNI ya existente en Registro",
                datoEntrada: "87654321 (Ya registrado)",
                esperado: "Rechazado: Este DNI ya está registrado. (campo: dniRegistro)",
                accionAsync: async () =>
                {
                    var nuevoUsuario = new Usuario
                    {
                        Dni = "87654321",
                        Celular = "911223344",
                        Correo = "nuevo@gmail.com",
                        Genero = "Masculino",
                        clave = "ClaveSegura#2026"
                    };
                    var res = await controller.Registro(nuevoUsuario, "ClaveSegura#2026", "", null);
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? campo = (string?)ObtenerPropiedad(json.Value, "campo");
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg} (campo: {campo})" : "Aceptado";
                });

            runner.VerificarTodosAprobados();
        }
    }
}
