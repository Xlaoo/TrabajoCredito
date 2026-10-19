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
    public class CelularValidationTests
    {
        private readonly ITestOutputHelper _output;

        public CelularValidationTests(ITestOutputHelper output)
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
        public async Task ProbarTodasLasValidacionesDeCelular()
        {
            var runner = new EscenarioRunner(_output, "Validaciones de Número de Celular (CelularValidation)");

            var options = new DbContextOptionsBuilder<UsuarioContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new UsuarioContext(options);
            var controller = CrearControlador(context);

            // Función auxiliar para evaluar el campo Celular en Registro
            async Task<string> ValidarCelularAsync(string? celular)
            {
                var usuario = new Usuario
                {
                    Dni = "76543210", // DNI válido no duplicado
                    Celular = celular!
                };

                var res = await controller.Registro(usuario, "Clave#2026Segura", "", null);
                var json = (JsonResult)res;
                bool ok = (bool)(ObtenerPropiedad(json.Value, "ok") ?? true);
                string? campo = (string?)ObtenerPropiedad(json.Value, "campo");
                string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");

                if (!ok && campo == "celularRegistro")
                {
                    return $"Rechazado: {msg}";
                }
                else if (!ok)
                {
                    // Si falló por otro campo posterior (ej: correo), significa que Celular fue aceptado
                    return "Aceptado (Avanza a validación de siguiente campo)";
                }
                return "Aceptado";
            }

            // 1. Celular nulo
            await runner.EvaluarAsync(
                nombreEscenario: "Celular nulo",
                datoEntrada: "[null]",
                esperado: "Rechazado: El celular debe tener 9 números y empezar con 9.",
                accionAsync: async () => await ValidarCelularAsync(null));

            // 2. Celular vacío
            await runner.EvaluarAsync(
                nombreEscenario: "Celular vacío",
                datoEntrada: "\"\"",
                esperado: "Rechazado: El celular debe tener 9 números y empezar con 9.",
                accionAsync: async () => await ValidarCelularAsync(""));

            // 3. Celular con solo espacios
            await runner.EvaluarAsync(
                nombreEscenario: "Celular con solo espacios",
                datoEntrada: "\"   \"",
                esperado: "Rechazado: El celular debe tener 9 números y empezar con 9.",
                accionAsync: async () => await ValidarCelularAsync("   "));

            // 4. Celular de 8 dígitos (falta 1 dígito)
            await runner.EvaluarAsync(
                nombreEscenario: "Celular de 8 dígitos (longitud insuficiente)",
                datoEntrada: "98765432 (8 dígitos)",
                esperado: "Rechazado: El celular debe tener 9 números y empezar con 9.",
                accionAsync: async () => await ValidarCelularAsync("98765432"));

            // 5. Celular de 10 dígitos (sobra 1 dígito)
            await runner.EvaluarAsync(
                nombreEscenario: "Celular de 10 dígitos (longitud excedida)",
                datoEntrada: "9876543210 (10 dígitos)",
                esperado: "Rechazado: El celular debe tener 9 números y empezar con 9.",
                accionAsync: async () => await ValidarCelularAsync("9876543210"));

            // 6. Celular de 9 dígitos que no empieza con 9 (empieza con 8)
            await runner.EvaluarAsync(
                nombreEscenario: "Celular de 9 dígitos que inicia con dígito 8",
                datoEntrada: "887654321",
                esperado: "Rechazado: El celular debe tener 9 números y empezar con 9.",
                accionAsync: async () => await ValidarCelularAsync("887654321"));

            // 7. Celular de 9 dígitos que no empieza con 9 (empieza con 1)
            await runner.EvaluarAsync(
                nombreEscenario: "Celular de 9 dígitos que inicia con dígito 1",
                datoEntrada: "123456789",
                esperado: "Rechazado: El celular debe tener 9 números y empezar con 9.",
                accionAsync: async () => await ValidarCelularAsync("123456789"));

            // 8. Celular con letras
            await runner.EvaluarAsync(
                nombreEscenario: "Celular con caracteres alfabéticos",
                datoEntrada: "98765432A",
                esperado: "Rechazado: El celular debe tener 9 números y empezar con 9.",
                accionAsync: async () => await ValidarCelularAsync("98765432A"));

            // 9. Celular con símbolos
            await runner.EvaluarAsync(
                nombreEscenario: "Celular con símbolos o guiones",
                datoEntrada: "987-65432",
                esperado: "Rechazado: El celular debe tener 9 números y empezar con 9.",
                accionAsync: async () => await ValidarCelularAsync("987-65432"));

            // 10. Celular con espacios intercalados
            await runner.EvaluarAsync(
                nombreEscenario: "Celular con espacios intercalados",
                datoEntrada: "9876 5432",
                esperado: "Rechazado: El celular debe tener 9 números y empezar con 9.",
                accionAsync: async () => await ValidarCelularAsync("9876 5432"));

            // 11. Celular válido (9 dígitos comenzando con 9)
            await runner.EvaluarAsync(
                nombreEscenario: "Celular peruano válido (9 dígitos iniciando con 9)",
                datoEntrada: "987654321",
                esperado: "Aceptado (Avanza a validación de siguiente campo)",
                accionAsync: async () => await ValidarCelularAsync("987654321"));

            runner.VerificarTodosAprobados();
        }
    }
}
