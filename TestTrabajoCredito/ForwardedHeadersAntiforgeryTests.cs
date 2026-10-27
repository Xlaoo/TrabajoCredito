using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace TestTrabajoCredito
{
    public class ForwardedHeadersAntiforgeryTests
    {
        private readonly ITestOutputHelper _output;

        public ForwardedHeadersAntiforgeryTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static IServiceProvider CrearServiceProvider(Action<ForwardedHeadersOptions>? configAdicional = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAntiforgery(options =>
            {
                options.HeaderName = "RequestVerificationToken";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Producción
            });

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();

                options.KnownProxies.Add(IPAddress.Loopback);
                options.KnownProxies.Add(IPAddress.IPv6Loopback);

                // Subred verificada del clúster Northflank
                var clusterSubnet = IPAddress.Parse("10.56.0.0");
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(clusterSubnet, 16));
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(clusterSubnet.MapToIPv6(), 16 + 96));

                configAdicional?.Invoke(options);
            });

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task ProbarConfiguracionDefinitivaHttpsYAntiCsrfNorthflank()
        {
            var runner = new EscenarioRunner(_output, "Auditoría Definitiva HTTPS y Reverse Proxy Northflank");

            // 1. Reconocimiento HTTPS desde proxy verificado (10.56.141.194)
            await runner.EvaluarAsync(
                nombreEscenario: "Reconocimiento HTTPS desde IP autorizada de Northflank (10.56.141.194)",
                datoEntrada: "RemoteIp=10.56.141.194, X-Forwarded-Proto=https",
                esperado: "EsHttps: True, Scheme: https",
                accionAsync: async () =>
                {
                    var provider = CrearServiceProvider();
                    var fho = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

                    var context = new DefaultHttpContext();
                    context.Connection.RemoteIpAddress = IPAddress.Parse("10.56.141.194");
                    context.Request.Scheme = "http";
                    context.Request.Headers["X-Forwarded-Proto"] = "https";
                    context.Request.Headers["X-Forwarded-For"] = "190.237.15.20";

                    var middleware = new ForwardedHeadersMiddleware(
                        (ctx) => Task.CompletedTask,
                        NullLoggerFactory.Instance,
                        Options.Create(fho));

                    await middleware.Invoke(context);

                    return $"EsHttps: {context.Request.IsHttps}, Scheme: {context.Request.Scheme}";
                });

            // 2. Generación exitosa de token Anti-CSRF en producción (CookieSecurePolicy.Always)
            await runner.EvaluarAsync(
                nombreEscenario: "Generación de token Anti-CSRF con CookieSecurePolicy.Always en producción",
                datoEntrada: "RemoteIp=10.56.141.194, X-Forwarded-Proto=https",
                esperado: "TokenGenerado: True, SinExcepcion: True",
                accionAsync: async () =>
                {
                    var provider = CrearServiceProvider();
                    var antiforgery = provider.GetRequiredService<IAntiforgery>();
                    var fho = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

                    var context = new DefaultHttpContext();
                    context.Connection.RemoteIpAddress = IPAddress.Parse("10.56.141.194");
                    context.Request.Scheme = "http";
                    context.Request.Headers["X-Forwarded-Proto"] = "https";

                    var middleware = new ForwardedHeadersMiddleware(
                        (ctx) => Task.CompletedTask,
                        NullLoggerFactory.Instance,
                        Options.Create(fho));

                    await middleware.Invoke(context);

                    bool tokenGenerado = false;
                    bool sinExcepcion = false;

                    try
                    {
                        var tokenSet = antiforgery.GetAndStoreTokens(context);
                        tokenGenerado = !string.IsNullOrEmpty(tokenSet.RequestToken);
                        sinExcepcion = true;
                    }
                    catch (Exception)
                    {
                        sinExcepcion = false;
                    }

                    return $"TokenGenerado: {tokenGenerado}, SinExcepcion: {sinExcepcion}";
                });

            // 3. Resiliencia: Rotación de IP de proxy al reiniciar/reemplazar contenedor en Kubernetes (10.56.142.134 y 10.56.200.75)
            await runner.EvaluarAsync(
                nombreEscenario: "Resiliencia ante cambio de IP de proxy en Kubernetes sin modificar configuración",
                datoEntrada: "Petición 1: 10.56.142.134 | Petición 2: 10.56.200.75 (ambas en 10.56.0.0/16)",
                esperado: "Ip1_Https: True, Ip2_Https: True, Ip1_Token: True, Ip2_Token: True",
                accionAsync: async () =>
                {
                    var provider = CrearServiceProvider();
                    var antiforgery = provider.GetRequiredService<IAntiforgery>();
                    var fho = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

                    var middleware = new ForwardedHeadersMiddleware(
                        (ctx) => Task.CompletedTask,
                        NullLoggerFactory.Instance,
                        Options.Create(fho));

                    // Caso 1: Proxy rota al nodo 10.56.142.134
                    var ctx1 = new DefaultHttpContext();
                    ctx1.Connection.RemoteIpAddress = IPAddress.Parse("10.56.142.134");
                    ctx1.Request.Scheme = "http";
                    ctx1.Request.Headers["X-Forwarded-Proto"] = "https";
                    await middleware.Invoke(ctx1);
                    var t1 = antiforgery.GetAndStoreTokens(ctx1);

                    // Caso 2: Proxy rota a un nuevo nodo 10.56.200.75
                    var ctx2 = new DefaultHttpContext();
                    ctx2.Connection.RemoteIpAddress = IPAddress.Parse("10.56.200.75");
                    ctx2.Request.Scheme = "http";
                    ctx2.Request.Headers["X-Forwarded-Proto"] = "https";
                    await middleware.Invoke(ctx2);
                    var t2 = antiforgery.GetAndStoreTokens(ctx2);

                    return $"Ip1_Https: {ctx1.Request.IsHttps}, Ip2_Https: {ctx2.Request.IsHttps}, " +
                           $"Ip1_Token: {!string.IsNullOrEmpty(t1.RequestToken)}, Ip2_Token: {!string.IsNullOrEmpty(t2.RequestToken)}";
                });

            // 4. Seguridad: Rechazo de cabeceras falsificadas desde IP pública no autorizada
            await runner.EvaluarAsync(
                nombreEscenario: "Rechazo estricto de cabeceras X-Forwarded-* de IP pública externa (Previene spoofing)",
                datoEntrada: "RemoteIp=203.0.113.50 (Atacante de internet), X-Forwarded-Proto=https",
                esperado: "EsHttps: False, ExcepcionEsperada: InvalidOperationException",
                accionAsync: async () =>
                {
                    var provider = CrearServiceProvider();
                    var antiforgery = provider.GetRequiredService<IAntiforgery>();
                    var fho = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

                    var context = new DefaultHttpContext();
                    context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.50"); // Atacante externo
                    context.Request.Scheme = "http";
                    context.Request.Headers["X-Forwarded-Proto"] = "https";

                    var middleware = new ForwardedHeadersMiddleware(
                        (ctx) => Task.CompletedTask,
                        NullLoggerFactory.Instance,
                        Options.Create(fho));

                    await middleware.Invoke(context);

                    bool esHttps = context.Request.IsHttps;
                    string nombreExcepcion = "";

                    try
                    {
                        antiforgery.GetAndStoreTokens(context);
                    }
                    catch (InvalidOperationException ex)
                    {
                        nombreExcepcion = ex.GetType().Name;
                    }

                    return $"EsHttps: {esHttps}, ExcepcionEsperada: {nombreExcepcion}";
                });

            // 5. Seguridad: Rechazo de cabeceras desde otra IP privada fuera de la subred del clúster
            await runner.EvaluarAsync(
                nombreEscenario: "Rechazo de cabeceras desde IP privada no perteneciente al clúster (no se confía en todo RFC 1918)",
                datoEntrada: "RemoteIp=192.168.1.100 y 10.20.0.1, X-Forwarded-Proto=https",
                esperado: "Privada1_Https: False, Privada2_Https: False",
                accionAsync: async () =>
                {
                    var provider = CrearServiceProvider();
                    var fho = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

                    var middleware = new ForwardedHeadersMiddleware(
                        (ctx) => Task.CompletedTask,
                        NullLoggerFactory.Instance,
                        Options.Create(fho));

                    var ctx1 = new DefaultHttpContext();
                    ctx1.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");
                    ctx1.Request.Scheme = "http";
                    ctx1.Request.Headers["X-Forwarded-Proto"] = "https";
                    await middleware.Invoke(ctx1);

                    var ctx2 = new DefaultHttpContext();
                    ctx2.Connection.RemoteIpAddress = IPAddress.Parse("10.20.0.1");
                    ctx2.Request.Scheme = "http";
                    ctx2.Request.Headers["X-Forwarded-Proto"] = "https";
                    await middleware.Invoke(ctx2);

                    return $"Privada1_Https: {ctx1.Request.IsHttps}, Privada2_Https: {ctx2.Request.IsHttps}";
                });

            // 6. Compatibilidad con conexiones dual-stack IPv4-mapped IPv6 en contenedores Linux
            await runner.EvaluarAsync(
                nombreEscenario: "Compatibilidad con conexiones IPv4-mapped IPv6 de sockets Linux (::ffff:10.56.141.194)",
                datoEntrada: "RemoteIp=::ffff:10.56.141.194, X-Forwarded-Proto=https",
                esperado: "EsHttps: True, TokenGenerado: True",
                accionAsync: async () =>
                {
                    var provider = CrearServiceProvider();
                    var antiforgery = provider.GetRequiredService<IAntiforgery>();
                    var fho = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

                    var context = new DefaultHttpContext();
                    context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:10.56.141.194");
                    context.Request.Scheme = "http";
                    context.Request.Headers["X-Forwarded-Proto"] = "https";

                    var middleware = new ForwardedHeadersMiddleware(
                        (ctx) => Task.CompletedTask,
                        NullLoggerFactory.Instance,
                        Options.Create(fho));

                    await middleware.Invoke(context);

                    var tokenSet = antiforgery.GetAndStoreTokens(context);

                    return $"EsHttps: {context.Request.IsHttps}, TokenGenerado: {!string.IsNullOrEmpty(tokenSet.RequestToken)}";
                });

            // 7. Compatibilidad con Localhost en desarrollo (HTTP puro sin proxy)
            runner.Evaluar(
                nombreEscenario: "Localhost en desarrollo funciona correctamente sobre HTTP (CookieSecurePolicy.SameAsRequest)",
                datoEntrada: "RemoteIp=127.0.0.1, Scheme=http, CookieSecurePolicy=SameAsRequest",
                esperado: "EsHttps: False, TokenGenerado: True",
                accion: () =>
                {
                    var services = new ServiceCollection();
                    services.AddLogging();
                    services.AddAntiforgery(options =>
                    {
                        options.HeaderName = "RequestVerificationToken";
                        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Modo Desarrollo
                    });

                    var provider = services.BuildServiceProvider();
                    var antiforgery = provider.GetRequiredService<IAntiforgery>();

                    var context = new DefaultHttpContext();
                    context.Connection.RemoteIpAddress = IPAddress.Loopback;
                    context.Request.Scheme = "http";

                    var tokenSet = antiforgery.GetAndStoreTokens(context);

                    return $"EsHttps: {context.Request.IsHttps}, TokenGenerado: {!string.IsNullOrEmpty(tokenSet.RequestToken)}";
                });

            // 8. Conservación de Cookies Secure y comportamiento sin estado (idempotencia tras reinicio)
            await runner.EvaluarAsync(
                nombreEscenario: "Conservación de Cookies Secure e idempotencia tras reinicios sucesivos",
                datoEntrada: "Reinicio simulado de pipeline y generación de cookies en dos instancias independientes",
                esperado: "Instancia1_Valida: True, Instancia2_Valida: True",
                accionAsync: async () =>
                {
                    // Instancia A (primer arranque)
                    var providerA = CrearServiceProvider();
                    var fhoA = providerA.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
                    var ctxA = new DefaultHttpContext();
                    ctxA.Connection.RemoteIpAddress = IPAddress.Parse("10.56.141.194");
                    ctxA.Request.Scheme = "http";
                    ctxA.Request.Headers["X-Forwarded-Proto"] = "https";
                    await new ForwardedHeadersMiddleware((c) => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(fhoA)).Invoke(ctxA);
                    var tA = providerA.GetRequiredService<IAntiforgery>().GetAndStoreTokens(ctxA);

                    // Instancia B (simulación de reinicio de contenedor)
                    var providerB = CrearServiceProvider();
                    var fhoB = providerB.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
                    var ctxB = new DefaultHttpContext();
                    ctxB.Connection.RemoteIpAddress = IPAddress.Parse("10.56.142.134");
                    ctxB.Request.Scheme = "http";
                    ctxB.Request.Headers["X-Forwarded-Proto"] = "https";
                    await new ForwardedHeadersMiddleware((c) => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(fhoB)).Invoke(ctxB);
                    var tB = providerB.GetRequiredService<IAntiforgery>().GetAndStoreTokens(ctxB);

                    return $"Instancia1_Valida: {ctxA.Request.IsHttps && !string.IsNullOrEmpty(tA.RequestToken)}, " +
                           $"Instancia2_Valida: {ctxB.Request.IsHttps && !string.IsNullOrEmpty(tB.RequestToken)}";
                });

            runner.VerificarTodosAprobados();
        }
    }
}
