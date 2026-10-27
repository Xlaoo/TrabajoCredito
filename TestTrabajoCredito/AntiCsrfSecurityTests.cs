using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using trabajo.Controllers;

namespace TestTrabajoCredito
{
    public class AntiCsrfSecurityTests
    {
        private readonly ITestOutputHelper _output;

        public AntiCsrfSecurityTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ProbarProteccionAntiCsrfEnControladoresYVistas()
        {
            var runner = new EscenarioRunner(_output, "Protección Anti-CSRF (OWASP ZAP 10202 - Anti-CSRF Tokens)");

            // 1. Verificar [ValidateAntiForgeryToken] en RegistrarSolicitudCredito
            runner.Evaluar(
                nombreEscenario: "Acción POST RegistrarSolicitudCredito decorada con [ValidateAntiForgeryToken]",
                datoEntrada: "Reflexión sobre método LoginController.RegistrarSolicitudCredito",
                esperado: "TieneHttpPost: True, TieneValidateAntiForgeryToken: True",
                accion: () =>
                {
                    var metodo = typeof(LoginController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m => m.Name == "RegistrarSolicitudCredito" && m.GetCustomAttribute<HttpPostAttribute>() != null);

                    bool tienePost = metodo != null;
                    bool tieneAntiCsrf = metodo?.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() != null;

                    return $"TieneHttpPost: {tienePost}, TieneValidateAntiForgeryToken: {tieneAntiCsrf}";
                });

            // 2. Verificar [ValidateAntiForgeryToken] en EnviarCronograma
            runner.Evaluar(
                nombreEscenario: "Acción POST EnviarCronograma decorada con [ValidateAntiForgeryToken]",
                datoEntrada: "Reflexión sobre método LoginController.EnviarCronograma",
                esperado: "TieneHttpPost: True, TieneValidateAntiForgeryToken: True",
                accion: () =>
                {
                    var metodo = typeof(LoginController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m => m.Name == "EnviarCronograma" && m.GetCustomAttribute<HttpPostAttribute>() != null);

                    bool tienePost = metodo != null;
                    bool tieneAntiCsrf = metodo?.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() != null;

                    return $"TieneHttpPost: {tienePost}, TieneValidateAntiForgeryToken: {tieneAntiCsrf}";
                });

            // 3. Verificar que MisSolicitudes.cshtml contiene @Html.AntiForgeryToken()
            runner.Evaluar(
                nombreEscenario: "Vista MisSolicitudes.cshtml incluye token Anti-CSRF en formulario EnviarCronograma",
                datoEntrada: "Inspección de código fuente en Views/Login/MisSolicitudes.cshtml",
                esperado: "FormularioCronogramaPresente: True, TokenAntiCsrfPresente: True",
                accion: () =>
                {
                    string rutaVista = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "trabajo", "Views", "Login", "MisSolicitudes.cshtml");
                    if (!File.Exists(rutaVista))
                    {
                        rutaVista = @"d:\TrabajoCredito\trabajo\Views\Login\MisSolicitudes.cshtml";
                    }

                    string contenido = File.ReadAllText(rutaVista);
                    bool tieneForm = contenido.Contains("EnviarCronograma");
                    bool tieneToken = contenido.Contains("@Html.AntiForgeryToken()");

                    return $"FormularioCronogramaPresente: {tieneForm}, TokenAntiCsrfPresente: {tieneToken}";
                });

            // 4. Verificar que SolicitarCredito.cshtml contiene @Html.AntiForgeryToken()
            runner.Evaluar(
                nombreEscenario: "Vista SolicitarCredito.cshtml incluye token Anti-CSRF en formSolicitarCredito",
                datoEntrada: "Inspección de código fuente en Views/Login/SolicitarCredito.cshtml",
                esperado: "FormularioSolicitudPresente: True, TokenAntiCsrfPresente: True",
                accion: () =>
                {
                    string rutaVista = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "trabajo", "Views", "Login", "SolicitarCredito.cshtml");
                    if (!File.Exists(rutaVista))
                    {
                        rutaVista = @"d:\TrabajoCredito\trabajo\Views\Login\SolicitarCredito.cshtml";
                    }

                    string contenido = File.ReadAllText(rutaVista);
                    bool tieneForm = contenido.Contains("formSolicitarCredito");
                    bool tieneToken = contenido.Contains("@Html.AntiForgeryToken()");

                    return $"FormularioSolicitudPresente: {tieneForm}, TokenAntiCsrfPresente: {tieneToken}";
                });

            // 5. Configurar servicios de AntiForgery reales y validar rechazo de petición POST sin token
            await runner.EvaluarAsync(
                nombreEscenario: "Rechazo de solicitud HTTP POST sin token Anti-CSRF por el servicio IAntiforgery",
                datoEntrada: "Petición POST con HttpContext vacío sin cookie ni token de verificación",
                esperado: "ValidacionFallida: True, ExcepcionEsperada: AntiforgeryValidationException",
                accionAsync: async () =>
                {
                    var services = new ServiceCollection();
                    services.AddLogging();
                    services.AddAntiforgery(options =>
                    {
                        options.HeaderName = "RequestVerificationToken";
                        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                        options.Cookie.SameSite = SameSiteMode.Strict;
                    });

                    var provider = services.BuildServiceProvider();
                    var antiforgery = provider.GetRequiredService<IAntiforgery>();

                    var httpContext = new DefaultHttpContext();
                    httpContext.Request.IsHttps = true;
                    httpContext.Request.Method = "POST";

                    bool rechazoExitoso = false;
                    string nombreExcepcion = "";

                    try
                    {
                        await antiforgery.ValidateRequestAsync(httpContext);
                    }
                    catch (AntiforgeryValidationException ex)
                    {
                        rechazoExitoso = true;
                        nombreExcepcion = ex.GetType().Name;
                    }

                    return $"ValidacionFallida: {rechazoExitoso}, ExcepcionEsperada: {nombreExcepcion}";
                });

            // 6. Validación exitosa cuando se proporciona token legítimo
            await runner.EvaluarAsync(
                nombreEscenario: "Aceptación de solicitud legítima con par de tokens (Cookie y Header)",
                datoEntrada: "Petición POST con token Anti-CSRF válido emitido por IAntiforgery",
                esperado: "ValidacionExitosa: True",
                accionAsync: async () =>
                {
                    var services = new ServiceCollection();
                    services.AddLogging();
                    services.AddAntiforgery(options =>
                    {
                        options.HeaderName = "RequestVerificationToken";
                        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                        options.Cookie.SameSite = SameSiteMode.Strict;
                    });

                    var provider = services.BuildServiceProvider();
                    var antiforgery = provider.GetRequiredService<IAntiforgery>();

                    var httpContext = new DefaultHttpContext();
                    httpContext.Request.IsHttps = true;
                    httpContext.Request.Method = "POST";

                    var tokenSet = antiforgery.GetAndStoreTokens(httpContext);

                    // Simular siguiente petición enviando la cookie y el header
                    var requestContext = new DefaultHttpContext();
                    requestContext.Request.IsHttps = true;
                    requestContext.Request.Method = "POST";
                    if (httpContext.Response.Headers.TryGetValue("Set-Cookie", out var setCookieVal))
                    {
                        var cookiePart = setCookieVal.ToString().Split(';')[0];
                        requestContext.Request.Headers["Cookie"] = cookiePart;
                    }
                    if (tokenSet.RequestToken != null)
                    {
                        requestContext.Request.Headers["RequestVerificationToken"] = tokenSet.RequestToken;
                    }

                    bool esValido = await antiforgery.IsRequestValidAsync(requestContext);

                    return $"ValidacionExitosa: {esValido}";
                });

            runner.VerificarTodosAprobados();
        }
    }
}
