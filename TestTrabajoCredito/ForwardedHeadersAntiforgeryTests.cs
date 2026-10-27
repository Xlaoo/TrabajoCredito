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

        [Fact]
        public async Task ProbarReconocimientoHttpsYProteccionAntiCsrfConProxyVerificado()
        {
            var runner = new EscenarioRunner(_output, "Forwarded Headers Estricto y Anti-CSRF con NORTHFLANK_PROXY_IP");

            // 1. Escenario: Proxy verificado por IP única en NORTHFLANK_PROXY_IP (ej. 10.244.0.1)
            await runner.EvaluarAsync(
                nombreEscenario: "UseForwardedHeaders() reconoce HTTPS desde IP verificada en NORTHFLANK_PROXY_IP",
                datoEntrada: "NORTHFLANK_PROXY_IP=10.244.0.1, RemoteIp=10.244.0.1, X-Forwarded-Proto=https",
                esperado: "EsHttps: True, TokenGenerado: True",
                accionAsync: async () =>
                {
                    var services = new ServiceCollection();
                    services.AddLogging();
                    services.AddAntiforgery(options =>
                    {
                        options.HeaderName = "RequestVerificationToken";
                        options.Cookie.HttpOnly = true;
                        options.Cookie.SameSite = SameSiteMode.Lax;
                        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Modo Producción
                    });

                    // Configuración estricta idéntica a Program.cs con IP verificada
                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                        options.ForwardLimit = 1;
                        options.KnownNetworks.Clear();
                        options.KnownProxies.Clear();
                        options.KnownProxies.Add(IPAddress.Loopback);
                        options.KnownProxies.Add(IPAddress.IPv6Loopback);

                        // Registro de la IP verificada configurada en Northflank
                        var proxyIp = IPAddress.Parse("10.244.0.1");
                        options.KnownProxies.Add(proxyIp);
                        options.KnownProxies.Add(proxyIp.MapToIPv6());
                    });

                    var provider = services.BuildServiceProvider();
                    var antiforgery = provider.GetRequiredService<IAntiforgery>();
                    var fho = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

                    var context = new DefaultHttpContext();
                    context.Connection.RemoteIpAddress = IPAddress.Parse("10.244.0.1");
                    context.Request.Scheme = "http";
                    context.Request.Headers["X-Forwarded-Proto"] = "https";
                    context.Request.Headers["X-Forwarded-For"] = "190.237.15.20";

                    // Ejecución exclusiva del middleware oficial de ASP.NET Core
                    var middleware = new ForwardedHeadersMiddleware(
                        (ctx) => Task.CompletedTask,
                        NullLoggerFactory.Instance,
                        Options.Create(fho));

                    await middleware.Invoke(context);

                    var tokenSet = antiforgery.GetAndStoreTokens(context);

                    return $"EsHttps: {context.Request.IsHttps}, TokenGenerado: {!string.IsNullOrEmpty(tokenSet.RequestToken)}";
                });

            // 2. Escenario: Proxy verificado con IPv4-mapped IPv6 (::ffff:10.244.0.1) en contenedor Linux
            await runner.EvaluarAsync(
                nombreEscenario: "UseForwardedHeaders() resuelve dual-stack IPv4-mapped IPv6 para el proxy verificado",
                datoEntrada: "NORTHFLANK_PROXY_IP=10.244.0.1, RemoteIp=::ffff:10.244.0.1, X-Forwarded-Proto=https",
                esperado: "EsHttps: True, TokenGenerado: True",
                accionAsync: async () =>
                {
                    var services = new ServiceCollection();
                    services.AddLogging();
                    services.AddAntiforgery(options =>
                    {
                        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    });

                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                        options.ForwardLimit = 1;
                        options.KnownNetworks.Clear();
                        options.KnownProxies.Clear();
                        options.KnownProxies.Add(IPAddress.Loopback);
                        options.KnownProxies.Add(IPAddress.IPv6Loopback);

                        var proxyIp = IPAddress.Parse("10.244.0.1");
                        options.KnownProxies.Add(proxyIp);
                        options.KnownProxies.Add(proxyIp.MapToIPv6());
                    });

                    var provider = services.BuildServiceProvider();
                    var antiforgery = provider.GetRequiredService<IAntiforgery>();
                    var fho = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

                    var context = new DefaultHttpContext();
                    context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:10.244.0.1");
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

            // 3. Escenario: Subred CIDR verificada en NORTHFLANK_PROXY_IP (ej. 10.244.0.0/16)
            await runner.EvaluarAsync(
                nombreEscenario: "UseForwardedHeaders() reconoce HTTPS desde subred CIDR verificada",
                datoEntrada: "NORTHFLANK_PROXY_IP=10.244.0.0/16, RemoteIp=10.244.3.45, X-Forwarded-Proto=https",
                esperado: "EsHttps: True, TokenGenerado: True",
                accionAsync: async () =>
                {
                    var services = new ServiceCollection();
                    services.AddLogging();
                    services.AddAntiforgery(options =>
                    {
                        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    });

                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                        options.ForwardLimit = 1;
                        options.KnownNetworks.Clear();
                        options.KnownProxies.Clear();
                        options.KnownProxies.Add(IPAddress.Loopback);
                        options.KnownProxies.Add(IPAddress.IPv6Loopback);

                        var redIp = IPAddress.Parse("10.244.0.0");
                        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(redIp, 16));
                        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(redIp.MapToIPv6(), 16 + 96));
                    });

                    var provider = services.BuildServiceProvider();
                    var antiforgery = provider.GetRequiredService<IAntiforgery>();
                    var fho = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

                    var context = new DefaultHttpContext();
                    context.Connection.RemoteIpAddress = IPAddress.Parse("10.244.3.45");
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

            // 4. Seguridad: Rechazo de cabecera X-Forwarded-Proto desde IP pública no verificada
            await runner.EvaluarAsync(
                nombreEscenario: "Rechazo estricto de cabeceras X-Forwarded-* de IP pública externa no autorizada",
                datoEntrada: "NORTHFLANK_PROXY_IP=10.244.0.1, RemoteIp=203.0.113.50 (No autorizada), X-Forwarded-Proto=https",
                esperado: "EsHttps: False, ExcepcionEsperada: InvalidOperationException",
                accionAsync: async () =>
                {
                    var services = new ServiceCollection();
                    services.AddLogging();
                    services.AddAntiforgery(options =>
                    {
                        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    });

                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                        options.ForwardLimit = 1;
                        options.KnownNetworks.Clear();
                        options.KnownProxies.Clear();
                        options.KnownProxies.Add(IPAddress.Loopback);
                        options.KnownProxies.Add(IPAddress.IPv6Loopback);

                        var proxyIp = IPAddress.Parse("10.244.0.1");
                        options.KnownProxies.Add(proxyIp);
                    });

                    var provider = services.BuildServiceProvider();
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

            // 5. Seguridad: Rechazo de cabecera desde IP privada NO registrada en NORTHFLANK_PROXY_IP
            await runner.EvaluarAsync(
                nombreEscenario: "Rechazo de cabecera desde IP privada no registrada (no se confía en todo RFC 1918)",
                datoEntrada: "NORTHFLANK_PROXY_IP=10.244.0.1, RemoteIp=192.168.1.50 (Privada no registrada), X-Forwarded-Proto=https",
                esperado: "EsHttps: False, ExcepcionEsperada: InvalidOperationException",
                accionAsync: async () =>
                {
                    var services = new ServiceCollection();
                    services.AddLogging();
                    services.AddAntiforgery(options =>
                    {
                        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    });

                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                        options.ForwardLimit = 1;
                        options.KnownNetworks.Clear();
                        options.KnownProxies.Clear();
                        options.KnownProxies.Add(IPAddress.Loopback);
                        options.KnownProxies.Add(IPAddress.IPv6Loopback);

                        // Solo se confía en la IP exacta configurada
                        var proxyIp = IPAddress.Parse("10.244.0.1");
                        options.KnownProxies.Add(proxyIp);
                    });

                    var provider = services.BuildServiceProvider();
                    var antiforgery = provider.GetRequiredService<IAntiforgery>();
                    var fho = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

                    var context = new DefaultHttpContext();
                    context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.50"); // Otra IP privada no autorizada
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

            // 6. Compatibilidad: Localhost en desarrollo sin HTTPS ni proxy funciona normalmente
            runner.Evaluar(
                nombreEscenario: "Localhost en desarrollo funciona correctamente con HTTP (CookieSecurePolicy.SameAsRequest)",
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

            runner.VerificarTodosAprobados();
        }
    }
}
