using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;
using Xunit.Abstractions;
using trabajo.Service;

namespace TestTrabajoCredito
{
    public class TurnstileServiceTests
    {
        private readonly ITestOutputHelper _output;

        public TurnstileServiceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private (TurnstileService Service, Mock<HttpMessageHandler> HttpMock) CrearServicio(
            string? siteKey = "0x4AAAAAA", string? secretKey = "0x4BBBBBB")
        {
            var configData = new Dictionary<string, string?>();
            if (siteKey != null) configData["TURNSTILE_SITE_KEY"] = siteKey;
            if (secretKey != null) configData["TURNSTILE_SECRET_KEY"] = secretKey;

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var httpMock = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(httpMock.Object);
            var logger = NullLogger<TurnstileService>.Instance;

            var service = new TurnstileService(httpClient, configuration, logger);
            return (service, httpMock);
        }

        [Fact]
        public async Task ProbarTodasLasValidacionesDeCaptchaTurnstile()
        {
            var runner = new EscenarioRunner(_output, "Verificación CAPTCHA Cloudflare Turnstile (TurnstileService)");

            // 1. Servicio no configurado (sin claves en configuración)
            runner.Evaluar(
                nombreEscenario: "Detección de estado no configurado cuando faltan claves",
                datoEntrada: "SiteKey=null, SecretKey=null",
                esperado: "Configurado: False, SiteKey: ''",
                accion: () =>
                {
                    var (service, _) = CrearServicio(siteKey: null, secretKey: null);
                    return $"Configurado: {service.EstaConfigurado()}, SiteKey: '{service.ObtenerSiteKey()}'";
                });

            // 2. Servicio configurado con claves válidas
            runner.Evaluar(
                nombreEscenario: "Detección de estado configurado con claves provistas",
                datoEntrada: "SiteKey='0x123SiteKey', SecretKey='0x456SecretKey'",
                esperado: "Configurado: True, SiteKey: '0x123SiteKey'",
                accion: () =>
                {
                    var (service, _) = CrearServicio(siteKey: "0x123SiteKey", secretKey: "0x456SecretKey");
                    return $"Configurado: {service.EstaConfigurado()}, SiteKey: '{service.ObtenerSiteKey()}'";
                });

            // 3. Modo fallback permisivo cuando no está configurado (no interrumpe desarrollo local)
            await runner.EvaluarAsync(
                nombreEscenario: "Modo simulado / Fallback permisivo cuando el servicio no está configurado",
                datoEntrada: "Token: 'tokenEnLocal' con servicio no configurado",
                esperado: "Valido: True, ModoFallback: True",
                accionAsync: async () =>
                {
                    var (service, _) = CrearServicio(siteKey: null, secretKey: null);
                    var res = await service.ValidarTokenAsync("tokenEnLocal");
                    return $"Valido: {res.Valido}, ModoFallback: {res.ModoSimuladoOFallback}";
                });

            // 4-6. Token nulo, vacío o con espacios con servicio configurado
            string?[] tokensVacios = new[] { null, "", "   " };
            foreach (var tok in tokensVacios)
            {
                await runner.EvaluarAsync(
                    nombreEscenario: $"Rechazo inmediato de token vacío: '{tok ?? "null"}'",
                    datoEntrada: tok ?? "[null]",
                    esperado: "Rechazado: Por favor completa la verificación de seguridad.",
                    accionAsync: async () =>
                    {
                        var (service, _) = CrearServicio();
                        var res = await service.ValidarTokenAsync(tok);
                        return !res.Valido ? $"Rechazado: {res.Mensaje}" : "Aceptado";
                    });
            }

            // 7. Validación exitosa: API de Cloudflare devuelve success=true
            await runner.EvaluarAsync(
                nombreEscenario: "Verificación exitosa con respuesta positiva de Cloudflare Turnstile",
                datoEntrada: "Token válido con IP cliente: '190.237.1.50'",
                esperado: "Valido: True, Errores: 0",
                accionAsync: async () =>
                {
                    var (service, httpMock) = CrearServicio();
                    var jsonRespuesta = JsonSerializer.Serialize(new
                    {
                        success = true,
                        challenge_ts = "2026-10-19T14:00:00Z",
                        hostname = "crediplus.com"
                    });

                    httpMock.Protected()
                        .Setup<Task<HttpResponseMessage>>(
                            "SendAsync",
                            ItExpr.IsAny<HttpRequestMessage>(),
                            ItExpr.IsAny<CancellationToken>()
                        )
                        .ReturnsAsync(new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(jsonRespuesta)
                        });

                    var res = await service.ValidarTokenAsync("token_valido_cloudflare", "190.237.1.50");
                    return $"Valido: {res.Valido}, Errores: {res.CodigosError.Length}";
                });

            // 8. Token duplicado o expirado (timeout-or-duplicate)
            await runner.EvaluarAsync(
                nombreEscenario: "Rechazo de token expirado o reutilizado (timeout-or-duplicate)",
                datoEntrada: "error-codes: ['timeout-or-duplicate']",
                esperado: "Rechazado: El token de seguridad ha expirado o ya fue utilizado. Por favor intenta de nuevo.",
                accionAsync: async () =>
                {
                    var (service, httpMock) = CrearServicio();
                    var jsonRespuesta = "{\"success\":false,\"error-codes\":[\"timeout-or-duplicate\"]}";

                    httpMock.Protected()
                        .Setup<Task<HttpResponseMessage>>(
                            "SendAsync",
                            ItExpr.IsAny<HttpRequestMessage>(),
                            ItExpr.IsAny<CancellationToken>()
                        )
                        .ReturnsAsync(new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(jsonRespuesta)
                        });

                    var res = await service.ValidarTokenAsync("token_duplicado");
                    return !res.Valido ? $"Rechazado: {res.Mensaje}" : "Aceptado";
                });

            // 9. Tolerancia ante timeout de red de Cloudflare (Fallback no bloqueante)
            await runner.EvaluarAsync(
                nombreEscenario: "Resiliencia ante corte o timeout de red con Cloudflare (Fallback seguro)",
                datoEntrada: "Simulación de TaskCanceledException (Timeout HTTP)",
                esperado: "Valido: True, ModoFallback: True",
                accionAsync: async () =>
                {
                    var (service, httpMock) = CrearServicio();

                    httpMock.Protected()
                        .Setup<Task<HttpResponseMessage>>(
                            "SendAsync",
                            ItExpr.IsAny<HttpRequestMessage>(),
                            ItExpr.IsAny<CancellationToken>()
                        )
                        .ThrowsAsync(new TaskCanceledException("Timeout de red"));

                    var res = await service.ValidarTokenAsync("token_con_timeout");
                    return $"Valido: {res.Valido}, ModoFallback: {res.ModoSimuladoOFallback}";
                });

            runner.VerificarTodosAprobados();
        }
    }
}
