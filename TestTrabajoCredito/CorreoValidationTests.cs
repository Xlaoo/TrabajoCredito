using System;
using System.Reflection;
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
    public class CorreoValidationTests
    {
        private readonly ITestOutputHelper _output;

        public CorreoValidationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static bool InvocarEsCorreoValidoDominio(string? correo)
        {
            var metodo = typeof(LoginController).GetMethod(
                "EsCorreoValidoDominio",
                BindingFlags.NonPublic | BindingFlags.Static
            );

            Assert.NotNull(metodo);
            return (bool)metodo.Invoke(null, new object?[] { correo })!;
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
        public async Task ProbarTodasLasValidacionesDeCorreo()
        {
            var runner = new EscenarioRunner(_output, "Validaciones de Correo Electrónico (CorreoValidation)");

            // 1. Correos válidos estándar
            string[] correosValidos = new[]
            {
                "cliente@gmail.com",
                "cliente.nombre+tag@gmail.com",
                "usuario@hotmail.com",
                "usuario@hotmail.es",
                "empresa@outlook.com",
                "persona@outlook.es",
                "cuenta@live.com"
            };

            foreach (var correo in correosValidos)
            {
                runner.Evaluar(
                    nombreEscenario: $"Dominio permitido válido: {correo}",
                    datoEntrada: correo,
                    esperado: true,
                    accion: () => InvocarEsCorreoValidoDominio(correo));
            }

            // 2. Correo con mayúsculas y formato mixto (debe ser normalizado / admitido)
            runner.Evaluar(
                nombreEscenario: "Correo totalmente en mayúsculas",
                datoEntrada: "CLIENTE@GMAIL.COM",
                esperado: true,
                accion: () => InvocarEsCorreoValidoDominio("CLIENTE@GMAIL.COM"));

            runner.Evaluar(
                nombreEscenario: "Correo en formato mixto mayúsculas/minúsculas",
                datoEntrada: "Usuario@HotMail.Es",
                esperado: true,
                accion: () => InvocarEsCorreoValidoDominio("Usuario@HotMail.Es"));

            // 3. Correo nulo y vacío
            runner.Evaluar(
                nombreEscenario: "Correo nulo",
                datoEntrada: "[null]",
                esperado: false,
                accion: () => InvocarEsCorreoValidoDominio(null));

            runner.Evaluar(
                nombreEscenario: "Correo vacío",
                datoEntrada: "\"\"",
                esperado: false,
                accion: () => InvocarEsCorreoValidoDominio(""));

            runner.Evaluar(
                nombreEscenario: "Correo con solo espacios",
                datoEntrada: "\"   \"",
                esperado: false,
                accion: () => InvocarEsCorreoValidoDominio("   "));

            // 4. Correo sin @
            runner.Evaluar(
                nombreEscenario: "Correo sin arroba (@)",
                datoEntrada: "correo_sin_arroba.com",
                esperado: false,
                accion: () => InvocarEsCorreoValidoDominio("correo_sin_arroba.com"));

            // 5. Correo sin nombre de usuario
            runner.Evaluar(
                nombreEscenario: "Correo sin nombre de usuario antes del @",
                datoEntrada: "@gmail.com",
                esperado: false,
                accion: () => InvocarEsCorreoValidoDominio("@gmail.com"));

            // 6. Correo sin dominio
            runner.Evaluar(
                nombreEscenario: "Correo sin dominio tras el @",
                datoEntrada: "usuario@",
                esperado: false,
                accion: () => InvocarEsCorreoValidoDominio("usuario@"));

            // 7. Correo con dominio incompleto (sin TLD .com / .es)
            runner.Evaluar(
                nombreEscenario: "Correo con dominio incompleto",
                datoEntrada: "usuario@gmail",
                esperado: false,
                accion: () => InvocarEsCorreoValidoDominio("usuario@gmail"));

            // 8. Correo con dominios no permitidos (solo se admiten Gmail, Hotmail, Outlook, Live)
            string[] dominiosNoPermitidos = new[]
            {
                "usuario@yahoo.com",
                "usuario@empresa.pe",
                "usuario@icloud.com"
            };

            foreach (var correoNoPermitido in dominiosNoPermitidos)
            {
                runner.Evaluar(
                    nombreEscenario: $"Correo con dominio no admitido: {correoNoPermitido}",
                    datoEntrada: correoNoPermitido,
                    esperado: false,
                    accion: () => InvocarEsCorreoValidoDominio(correoNoPermitido));
            }

            // 9. Correo con espacios intermedios
            runner.Evaluar(
                nombreEscenario: "Correo con espacio intermedio",
                datoEntrada: "usuario @gmail.com",
                esperado: false,
                accion: () => InvocarEsCorreoValidoDominio("usuario @gmail.com"));

            // 10-12. Validaciones a nivel de Controller (EnviarCodigo y detección de duplicados)
            var options = new DbContextOptionsBuilder<UsuarioContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new UsuarioContext(options);
            context.Usuario.Add(new Usuario
            {
                Dni = "12345678",
                Nombre = "Ana",
                Apellido = "García",
                Celular = "999888777",
                Correo = "ana@gmail.com",
                Genero = "Femenino",
                clave = utilidades.EncriptarClave("Password#1234"),
                Rol = "Cliente"
            });
            await context.SaveChangesAsync();

            var controller = CrearControlador(context);

            // EnviarCodigo con correo vacío
            await runner.EvaluarAsync(
                nombreEscenario: "EnviarCodigo con correo vacío",
                datoEntrada: "\"\"",
                esperado: "Rechazado: Primero debes ingresar tu correo.",
                accionAsync: async () =>
                {
                    var res = await controller.EnviarCodigo("");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // EnviarCodigo con dominio no permitido
            await runner.EvaluarAsync(
                nombreEscenario: "EnviarCodigo con dominio Yahoo no permitido",
                datoEntrada: "usuario@yahoo.com",
                esperado: "Rechazado: Ingresa un correo válido (Gmail, Hotmail o Outlook).",
                accionAsync: async () =>
                {
                    var res = await controller.EnviarCodigo("usuario@yahoo.com");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            // EnviarCodigo con correo duplicado
            await runner.EvaluarAsync(
                nombreEscenario: "EnviarCodigo con correo ya registrado en el sistema",
                datoEntrada: "ana@gmail.com",
                esperado: "Rechazado: Este correo electrónico ya está registrado en CrediPlus.",
                accionAsync: async () =>
                {
                    var res = await controller.EnviarCodigo("ana@gmail.com");
                    var json = (JsonResult)res;
                    bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");
                    return !ok ? $"Rechazado: {msg}" : "Aceptado";
                });

            runner.VerificarTodosAprobados();
        }
    }
}
