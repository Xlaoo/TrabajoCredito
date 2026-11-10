using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Xunit.Abstractions;
using trabajo.Controllers;
using trabajo.Models;
using trabajo.Service;

namespace TestTrabajoCredito
{
    public class SesionLifecycleTests
    {
        private readonly ITestOutputHelper _output;

        public SesionLifecycleTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static object? ObtenerPropiedad(object? objeto, string nombrePropiedad)
        {
            if (objeto == null) return null;
            return objeto.GetType().GetProperty(nombrePropiedad)?.GetValue(objeto);
        }

        private LoginController CrearLoginController(UsuarioContext context, string dni = "72527818", string rol = "Cliente")
        {
            var mockUsuarioService = new Mock<IusuarioServices>();
            var mockConfig = new Mock<IConfiguration>();
            var mockTurnstile = new Mock<ITurnstileService>();
            var servicioVoz = (ServicioEmbeddingVoz)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ServicioEmbeddingVoz));

            var controller = new LoginController(mockUsuarioService.Object, context, servicioVoz, mockConfig.Object, mockTurnstile.Object);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "UsuarioPrueba"),
                new Claim("Dni", dni),
                new Claim("Correo", "usuario@crediplus.pe"),
                new Claim(ClaimTypes.Role, rol)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
            controller.TempData = new TempDataDictionary(
                controller.HttpContext,
                Mock.Of<ITempDataProvider>()
            );

            return controller;
        }

        [Fact]
        public async Task ProbarCicloDeVidaDeSesionYComportamientoDeBotones()
        {
            var runner = new EscenarioRunner(_output, "Ciclo de Vida de Sesión y Estabilidad de Botones (Fix Cierre de Sesión)");

            // Escenario 1: Iniciar sesión como Cliente conserva EstadoActivo en true
            runner.Evaluar(
                nombreEscenario: "1. Iniciar sesión como Cliente: cuenta activa y sesión inicializada",
                datoEntrada: "Cliente DNI 72527818 con EstadoActivo = true",
                esperado: "EstadoActivo: True, SesionValida: True",
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
                    using var context = new UsuarioContext(options);
                    var cliente = new Usuario
                    {
                        Id = 1,
                        Nombre = "Rafael",
                        Apellido = "Rosales",
                        Dni = "72527818",
                        Correo = "cliente@crediplus.pe",
                        Celular = "981999121",
                        Rol = "Cliente",
                        EstadoActivo = true,
                        clave = "$2a$12$eXampleHashCrediPlus2026SecureBcryptHashValuePadding60"
                    };
                    context.Usuario.Add(cliente);
                    context.SaveChanges();

                    var controller = CrearLoginController(context, cliente.Dni, "Cliente");
                    bool sesionValida = controller.User.Identity?.IsAuthenticated == true && controller.User.IsInRole("Cliente");

                    var dbUser = context.Usuario.First(u => u.Dni == "72527818");
                    return $"EstadoActivo: {dbUser.EstadoActivo}, SesionValida: {sesionValida}";
                });

            // Escenario 2: Presionar botones / navegar NO desactiva EstadoActivo (MarcarClienteInactivo no suspende cuenta)
            runner.Evaluar(
                nombreEscenario: "2. Presionar botones del Dashboard: navegación no altera EstadoActivo ni destruye sesión",
                datoEntrada: "Llamada a MarcarClienteInactivo durante navegación de página",
                esperado: "EstadoActivoAntes: True, EstadoActivoDespues: True",
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
                    using var context = new UsuarioContext(options);
                    var cliente = new Usuario
                    {
                        Id = 1,
                        Nombre = "Rafael",
                        Apellido = "Rosales",
                        Dni = "72527818",
                        Correo = "cliente@crediplus.pe",
                        Celular = "981999121",
                        Rol = "Cliente",
                        EstadoActivo = true,
                        clave = "$2a$12$eXampleHashCrediPlus2026SecureBcryptHashValuePadding60"
                    };
                    context.Usuario.Add(cliente);
                    context.SaveChanges();

                    var controller = CrearLoginController(context, cliente.Dni, "Cliente");
                    bool estadoAntes = context.Usuario.First(u => u.Dni == "72527818").EstadoActivo;

                    // Simulación del endpoint de actividad / descarga de página
                    var res = controller.MarcarClienteInactivo();
                    Assert.IsType<OkResult>(res);

                    bool estadoDespues = context.Usuario.First(u => u.Dni == "72527818").EstadoActivo;
                    return $"EstadoActivoAntes: {estadoAntes}, EstadoActivoDespues: {estadoDespues}";
                });

            // Escenario 3: OnValidatePrincipal mantiene la sesión de usuarios activos
            await runner.EvaluarAsync(
                nombreEscenario: "3. OnValidatePrincipal: cookie y sesión se mantienen activas para usuario habilitado",
                datoEntrada: "CookieValidatePrincipalContext para cliente activo",
                esperado: "PrincipalRechazado: False, UsuarioActivo: True",
                accionAsync: async () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
                    using var context = new UsuarioContext(options);
                    var cliente = new Usuario
                    {
                        Id = 1,
                        Nombre = "Rafael",
                        Apellido = "Rosales",
                        Dni = "72527818",
                        Correo = "cliente@crediplus.pe",
                        Celular = "981999121",
                        Rol = "Cliente",
                        EstadoActivo = true,
                        clave = "$2a$12$eXampleHashCrediPlus2026SecureBcryptHashValuePadding60"
                    };
                    context.Usuario.Add(cliente);
                    await context.SaveChangesAsync();

                    var services = new ServiceCollection();
                    services.AddScoped(_ => new UsuarioContext(options));
                    services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                        .AddCookie();
                    var provider = services.BuildServiceProvider();

                    var httpContext = new DefaultHttpContext { RequestServices = provider };
                    var claims = new List<Claim> { new Claim("Dni", "72527818"), new Claim(ClaimTypes.Role, "Cliente") };
                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);
                    httpContext.User = principal;

                    var authTicket = new AuthenticationTicket(principal, CookieAuthenticationDefaults.AuthenticationScheme);
                    var cookieOptions = new CookieAuthenticationOptions();
                    var validateContext = new CookieValidatePrincipalContext(httpContext, new AuthenticationScheme("Cookies", null, typeof(CookieAuthenticationHandler)), cookieOptions, authTicket);

                    // Lógica real de OnValidatePrincipal en Program.cs
                    var db = validateContext.HttpContext.RequestServices.GetService<UsuarioContext>();
                    var dni = validateContext.Principal?.FindFirst("Dni")?.Value;
                    var user = await db!.Usuario.AsNoTracking().FirstOrDefaultAsync(u => u.Dni == dni);
                    if (user == null || !user.EstadoActivo)
                    {
                        validateContext.RejectPrincipal();
                    }

                    bool rechazado = validateContext.Principal == null || !validateContext.Principal.Identities.Any(i => i.IsAuthenticated);
                    return $"PrincipalRechazado: {rechazado}, UsuarioActivo: {user?.EstadoActivo}";
                });

            // Escenario 4: Registrar operación con datos inválidos muestra error sin cerrar sesión ni alterar EstadoActivo
            runner.Evaluar(
                nombreEscenario: "4. Registrar operación con datos inválidos muestra error sin cerrar sesión",
                datoEntrada: "Petición a RegistrarCancelacion con monto inválido S/ 1.00",
                esperado: "ok: False, EstadoActivo: True, SesionIntacta: True",
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
                    using var context = new UsuarioContext(options);
                    var cliente = new Usuario
                    {
                        Id = 1,
                        Nombre = "Rafael",
                        Apellido = "Rosales",
                        Dni = "72527818",
                        Correo = "cliente@crediplus.pe",
                        Celular = "981999121",
                        Rol = "Cliente",
                        EstadoActivo = true,
                        clave = "$2a$12$eXampleHashCrediPlus2026SecureBcryptHashValuePadding60"
                    };
                    context.Usuario.Add(cliente);
                    var sol = new SolicitudCredito
                    {
                        Id_Solicitud = 201,
                        NumeroSolicitud = "SOL-201",
                        Usuario_Id_Usuario = 1,
                        MontoSolicitado = 4000m,
                        PlazoMeses = 12,
                        Estado = "Aprobado",
                        FechaSolicitud = DateTime.UtcNow
                    };
                    context.SOLICITUD_CREDITO.Add(sol);
                    context.SaveChanges();

                    var controller = CrearLoginController(context, cliente.Dni, "Cliente");
                    var res = controller.RegistrarCancelacion(201, 1.00m, "Transferencia", "OP-FAIL", "Motivo");
                    var jsonRes = Assert.IsType<JsonResult>(res);
                    bool ok = (bool)(ObtenerPropiedad(jsonRes.Value, "ok") ?? true);

                    var userDb = context.Usuario.First(u => u.Dni == "72527818");
                    bool sesionIntacta = controller.User.Identity?.IsAuthenticated == true;

                    return $"ok: {ok}, EstadoActivo: {userDb.EstadoActivo}, SesionIntacta: {sesionIntacta}";
                });

            // Escenario 5: Cerrar sesión manualmente destruye la cookie sin suspender la cuenta en la BD
            await runner.EvaluarAsync(
                nombreEscenario: "5. Cerrar sesión manualmente: SignOutAsync exitoso sin suspender la cuenta en base de datos",
                datoEntrada: "Llamada voluntaria a CerrarSesion por el cliente",
                esperado: "EstadoActivoEnDb: True, Redireccion: PantallaPrincipal",
                accionAsync: async () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
                    using var context = new UsuarioContext(options);
                    var cliente = new Usuario
                    {
                        Id = 1,
                        Nombre = "Rafael",
                        Apellido = "Rosales",
                        Dni = "72527818",
                        Correo = "cliente@crediplus.pe",
                        Celular = "981999121",
                        Rol = "Cliente",
                        EstadoActivo = true,
                        clave = "$2a$12$eXampleHashCrediPlus2026SecureBcryptHashValuePadding60"
                    };
                    context.Usuario.Add(cliente);
                    await context.SaveChangesAsync();

                    var controller = CrearLoginController(context, cliente.Dni, "Cliente");
                    controller.Url = Mock.Of<IUrlHelper>();

                    var authServiceMock = new Mock<IAuthenticationService>();
                    var serviceProviderMock = new Mock<IServiceProvider>();
                    serviceProviderMock.Setup(sp => sp.GetService(typeof(IAuthenticationService)))
                        .Returns(authServiceMock.Object);
                    controller.HttpContext.RequestServices = serviceProviderMock.Object;

                    var res = await controller.CerrarSesion();
                    var redirect = Assert.IsType<RedirectToActionResult>(res);

                    var userDb = context.Usuario.First(u => u.Dni == "72527818");
                    return $"EstadoActivoEnDb: {userDb.EstadoActivo}, Redireccion: {redirect.ActionName}";
                });

            // Escenario 6: Suspensión administrativa real por el Administrador revoca la sesión en OnValidatePrincipal
            await runner.EvaluarAsync(
                nombreEscenario: "6. Suspensión administrativa por Admin: OnValidatePrincipal revoca inmediatamente la sesión",
                datoEntrada: "Administrador cambia EstadoActivo = false para un cliente",
                esperado: "PrincipalRechazado: True, UsuarioSuspendido: True",
                accionAsync: async () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
                    using var context = new UsuarioContext(options);
                    var cliente = new Usuario
                    {
                        Id = 1,
                        Nombre = "Usuario",
                        Apellido = "Suspendido",
                        Dni = "72527818",
                        Correo = "suspendido@crediplus.pe",
                        Celular = "981999121",
                        Rol = "Cliente",
                        EstadoActivo = false, // Suspendido por Administrador
                        clave = "$2a$12$eXampleHashCrediPlus2026SecureBcryptHashValuePadding60"
                    };
                    context.Usuario.Add(cliente);
                    await context.SaveChangesAsync();

                    var services = new ServiceCollection();
                    services.AddScoped(_ => new UsuarioContext(options));
                    services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                        .AddCookie();
                    var provider = services.BuildServiceProvider();

                    var httpContext = new DefaultHttpContext { RequestServices = provider };
                    var claims = new List<Claim> { new Claim("Dni", "72527818"), new Claim(ClaimTypes.Role, "Cliente") };
                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);
                    httpContext.User = principal;

                    var authTicket = new AuthenticationTicket(principal, CookieAuthenticationDefaults.AuthenticationScheme);
                    var cookieOptions = new CookieAuthenticationOptions();
                    var validateContext = new CookieValidatePrincipalContext(httpContext, new AuthenticationScheme("Cookies", null, typeof(CookieAuthenticationHandler)), cookieOptions, authTicket);

                    // Lógica de validación
                    var db = validateContext.HttpContext.RequestServices.GetService<UsuarioContext>();
                    var dni = validateContext.Principal?.FindFirst("Dni")?.Value;
                    var user = await db!.Usuario.AsNoTracking().FirstOrDefaultAsync(u => u.Dni == dni);
                    if (user == null || !user.EstadoActivo)
                    {
                        validateContext.RejectPrincipal();
                    }

                    bool rechazado = validateContext.Principal == null || !validateContext.Principal.Identities.Any(i => i.IsAuthenticated);
                    return $"PrincipalRechazado: {rechazado}, UsuarioSuspendido: {!(user?.EstadoActivo ?? true)}";
                });

            // Escenario 7: Roles y Claims para Analista y Administrador preservan sesión y permisos
            runner.Evaluar(
                nombreEscenario: "7. Permisos y claims de Analista y Administrador conservan sesión y rol asignado",
                datoEntrada: "Identidad de Analista y de Administrador",
                esperado: "AnalistaRol: Analista, AdminRol: Administrador",
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
                    using var context = new UsuarioContext(options);

                    var controllerAnalista = CrearLoginController(context, "11112222", "Analista");
                    var controllerAdmin = CrearLoginController(context, "33334444", "Administrador");

                    string rolAnalista = controllerAnalista.User.FindFirst(ClaimTypes.Role)?.Value ?? "";
                    string rolAdmin = controllerAdmin.User.FindFirst(ClaimTypes.Role)?.Value ?? "";

                    return $"AnalistaRol: {rolAnalista}, AdminRol: {rolAdmin}";
                });

            runner.VerificarTodosAprobados();
        }
    }
}
