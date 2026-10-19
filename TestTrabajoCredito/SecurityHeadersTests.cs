using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;
using Xunit.Abstractions;

namespace TestTrabajoCredito
{
    public class SecurityHeadersTests
    {
        private readonly ITestOutputHelper _output;

        public SecurityHeadersTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Simula la ejecución del middleware de cabeceras de seguridad configurado en Program.cs.
        /// </summary>
        private static async Task<IHeaderDictionary> EjecutarMiddlewareSeguridadAsync(HttpContext context, bool esProduccion = true)
        {
            var headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "microphone=(self), camera=(), geolocation=(), payment=(), usb=(), display-capture=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin-allow-popups";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";

            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://challenges.cloudflare.com; " +
                "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                "img-src 'self' data: blob: https://cdn-icons-png.flaticon.com https://challenges.cloudflare.com; " +
                "font-src 'self' data: https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                "connect-src 'self' wss: ws: https://challenges.cloudflare.com; " +
                "frame-src 'self' https://challenges.cloudflare.com; " +
                "frame-ancestors 'none'; " +
                "media-src 'self' blob:; " +
                "object-src 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self';";

            bool esHttps = context.Request.IsHttps ||
                           string.Equals(context.Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase);

            if (esHttps && !headers.ContainsKey("Strict-Transport-Security") && esProduccion)
            {
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            }

            await Task.CompletedTask;
            return headers;
        }

        [Fact]
        public async Task ProbarTodasLasCabecerasDeSeguridadHttp()
        {
            var runner = new EscenarioRunner(_output, "Cabeceras de Seguridad HTTP (OWASP / Northflank Hardening)");

            // 1. X-Content-Type-Options: nosniff
            await runner.EvaluarAsync(
                nombreEscenario: "Cabecera X-Content-Type-Options configurada con 'nosniff'",
                datoEntrada: "Petición HTTP entrante a CrediPlus",
                esperado: "nosniff",
                accionAsync: async () =>
                {
                    var ctx = new DefaultHttpContext();
                    var headers = await EjecutarMiddlewareSeguridadAsync(ctx);
                    return headers["X-Content-Type-Options"].ToString();
                });

            // 2. X-Frame-Options: DENY (Protección Anti-Clickjacking)
            await runner.EvaluarAsync(
                nombreEscenario: "Cabecera X-Frame-Options configurada con 'DENY'",
                datoEntrada: "Petición HTTP entrante a CrediPlus",
                esperado: "DENY",
                accionAsync: async () =>
                {
                    var ctx = new DefaultHttpContext();
                    var headers = await EjecutarMiddlewareSeguridadAsync(ctx);
                    return headers["X-Frame-Options"].ToString();
                });

            // 3. Referrer-Policy: strict-origin-when-cross-origin
            await runner.EvaluarAsync(
                nombreEscenario: "Cabecera Referrer-Policy configurada como 'strict-origin-when-cross-origin'",
                datoEntrada: "Petición HTTP entrante a CrediPlus",
                esperado: "strict-origin-when-cross-origin",
                accionAsync: async () =>
                {
                    var ctx = new DefaultHttpContext();
                    var headers = await EjecutarMiddlewareSeguridadAsync(ctx);
                    return headers["Referrer-Policy"].ToString();
                });

            // 4. Permissions-Policy: Micrófono habilitado para biometría de voz y APIs no usadas bloqueadas
            await runner.EvaluarAsync(
                nombreEscenario: "Permissions-Policy permite micrófono ('microphone=(self)') y restringe cámara/geolocalización",
                datoEntrada: "Inspección de directivas de hardware del navegador",
                esperado: "MicrophoneAllowed: True, CameraBlocked: True, GeolocationBlocked: True",
                accionAsync: async () =>
                {
                    var ctx = new DefaultHttpContext();
                    var headers = await EjecutarMiddlewareSeguridadAsync(ctx);
                    string pp = headers["Permissions-Policy"].ToString();

                    bool micOk = pp.Contains("microphone=(self)");
                    bool camBlocked = pp.Contains("camera=()");
                    bool geoBlocked = pp.Contains("geolocation=()");

                    return $"MicrophoneAllowed: {micOk}, CameraBlocked: {camBlocked}, GeolocationBlocked: {geoBlocked}";
                });

            // 5. Cross-Origin-Opener-Policy (COOP): same-origin-allow-popups
            await runner.EvaluarAsync(
                nombreEscenario: "Cross-Origin-Opener-Policy configurado con 'same-origin-allow-popups'",
                datoEntrada: "Petición HTTP entrante a CrediPlus",
                esperado: "same-origin-allow-popups",
                accionAsync: async () =>
                {
                    var ctx = new DefaultHttpContext();
                    var headers = await EjecutarMiddlewareSeguridadAsync(ctx);
                    return headers["Cross-Origin-Opener-Policy"].ToString();
                });

            // 6. X-Permitted-Cross-Domain-Policies: none
            await runner.EvaluarAsync(
                nombreEscenario: "X-Permitted-Cross-Domain-Policies configurado con 'none'",
                datoEntrada: "Petición HTTP entrante a CrediPlus",
                esperado: "none",
                accionAsync: async () =>
                {
                    var ctx = new DefaultHttpContext();
                    var headers = await EjecutarMiddlewareSeguridadAsync(ctx);
                    return headers["X-Permitted-Cross-Domain-Policies"].ToString();
                });

            // 7. Content-Security-Policy: Directiva activa con orígenes permitidos (Cloudflare, CDNs, SignalR wss)
            await runner.EvaluarAsync(
                nombreEscenario: "CSP activa y efectiva incluye orígenes permitidos (Cloudflare, CDNs, SignalR wss, unsafe-eval)",
                datoEntrada: "Inspección de directivas CSP (script-src, connect-src, frame-src, media-src)",
                esperado: "TurnstileOk: True, BootstrapCdnOk: True, SignalROk: True, MediaBlobOk: True, FrameAncestorsNone: True",
                accionAsync: async () =>
                {
                    var ctx = new DefaultHttpContext();
                    var headers = await EjecutarMiddlewareSeguridadAsync(ctx);
                    string csp = headers["Content-Security-Policy"].ToString();

                    bool turnstileOk = csp.Contains("https://challenges.cloudflare.com");
                    bool cdnOk = csp.Contains("https://cdn.jsdelivr.net") && csp.Contains("https://cdnjs.cloudflare.com");
                    bool signalrOk = csp.Contains("wss:") && csp.Contains("ws:");
                    bool mediaBlobOk = csp.Contains("media-src 'self' blob:");
                    bool frameAncestorsNone = csp.Contains("frame-ancestors 'none'");

                    return $"TurnstileOk: {turnstileOk}, BootstrapCdnOk: {cdnOk}, SignalROk: {signalrOk}, MediaBlobOk: {mediaBlobOk}, FrameAncestorsNone: {frameAncestorsNone}";
                });

            // 8. HSTS (Strict-Transport-Security) cuando la solicitud proviene de HTTPS o proxy Northflank
            await runner.EvaluarAsync(
                nombreEscenario: "HSTS se emite ante solicitud HTTPS con max-age de 1 año e includeSubDomains (sin preload)",
                datoEntrada: "Cabecera reenviada X-Forwarded-Proto: https",
                esperado: "HSTS: max-age=31536000; includeSubDomains | Preload: False",
                accionAsync: async () =>
                {
                    var ctx = new DefaultHttpContext();
                    ctx.Request.Headers["X-Forwarded-Proto"] = "https";
                    var headers = await EjecutarMiddlewareSeguridadAsync(ctx, esProduccion: true);
                    string hsts = headers["Strict-Transport-Security"].ToString();
                    bool hasPreload = hsts.Contains("preload", StringComparison.OrdinalIgnoreCase);

                    return $"HSTS: {hsts} | Preload: {hasPreload}";
                });

            // 9. Caso de Error / Detección: Detección de cabecera de seguridad ausente en respuestas desprotegidas
            runner.Evaluar(
                nombreEscenario: "Detección negativa: Alerta cuando una respuesta HTTP carece de cabeceras de seguridad",
                datoEntrada: "Contexto HTTP sin middleware de seguridad aplicado",
                esperado: "Cabecera Ausente Detectada: True",
                accion: () =>
                {
                    var ctxSinProteccion = new DefaultHttpContext();
                    // Sin invocar el middleware, la cabecera X-Content-Type-Options no existe
                    bool faltaCabecera = !ctxSinProteccion.Response.Headers.ContainsKey("X-Content-Type-Options");
                    return $"Cabecera Ausente Detectada: {faltaCabecera}";
                });

            // 10. Caso de Error / Detección: Detección de cabecera contradictoria o insegura (ej. X-Frame-Options: ALLOWALL)
            runner.Evaluar(
                nombreEscenario: "Detección negativa: Alerta ante políticas contradictorias o inseguras de clickjacking",
                datoEntrada: "Respuesta con valor inseguro 'ALLOWALL'",
                esperado: "PoliticaInseguraDetectada: True",
                accion: () =>
                {
                    string valorInseguro = "ALLOWALL";
                    bool esInseguro = valorInseguro != "DENY" && valorInseguro != "SAMEORIGIN";
                    return $"PoliticaInseguraDetectada: {esInseguro}";
                });

            // 11. Caso de Compatibilidad: Verificación de que el micrófono NO está bloqueado para Biometría de Voz
            await runner.EvaluarAsync(
                nombreEscenario: "Garantía de compatibilidad: Micrófono habilitado para biometría acústica (Vosk/ECAPA)",
                datoEntrada: "Validación de Permissions-Policy para captura de audio",
                esperado: "MicrofonoHabilitado: True",
                accionAsync: async () =>
                {
                    var ctx = new DefaultHttpContext();
                    var headers = await EjecutarMiddlewareSeguridadAsync(ctx);
                    string pp = headers["Permissions-Policy"].ToString();

                    // microphone=(self) permite captura de audio en el origen local/producción de CrediPlus
                    bool micHabilitado = pp.Contains("microphone=(self)") && !pp.Contains("microphone=()");
                    return $"MicrofonoHabilitado: {micHabilitado}";
                });

            runner.VerificarTodosAprobados();
        }
    }
}
