using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
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
    public class CreditosTests
    {
        private readonly ITestOutputHelper _output;

        public CreditosTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static object? ObtenerPropiedad(object? objeto, string nombrePropiedad)
        {
            if (objeto == null) return null;
            return objeto.GetType().GetProperty(nombrePropiedad)?.GetValue(objeto);
        }

        private AnalistaController CrearAnalistaController(UsuarioContext context, IServicioEvaluacionCrediticia? servicioEvaluacion = null)
        {
            var mockConfig = new Mock<IConfiguration>();
            var controller = new AnalistaController(context, mockConfig.Object, servicioEvaluacion ?? new ServicioEvaluacionCrediticia());
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            return controller;
        }

        private LoginController CrearLoginController(UsuarioContext context)
        {
            var mockUsuarioService = new Mock<IusuarioServices>();
            var mockConfig = new Mock<IConfiguration>();
            var mockTurnstile = new Mock<ITurnstileService>();
            var servicioVoz = (ServicioEmbeddingVoz)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ServicioEmbeddingVoz));

            var controller = new LoginController(mockUsuarioService.Object, context, servicioVoz, mockConfig.Object, mockTurnstile.Object);

            var claims = new List<Claim>
            {
                new Claim("Dni", "72527818"),
                new Claim(ClaimTypes.Role, "Cliente")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
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
        public void ProbarTodasLasValidacionesDeCreditos()
        {
            var runner = new EscenarioRunner(_output, "Evaluación Crediticia y Reglas de Negocio de Créditos");

            var servicio = new ServicioEvaluacionCrediticia();

            // 1. Cálculo de fórmulas financieras de evaluación crediticia
            runner.Evaluar(
                nombreEscenario: "Cálculo financiero: Ingreso neto, capacidad de pago y cuota mensual",
                datoEntrada: "Ingreso: 3000, Gastos: 1000, Monto: 5000, Plazo: 12 meses",
                esperado: "Neto: 2000, Capacidad: 900.00, Disponible: 9450.00, Interes: 500.00, Cuota: 458.33, Viable: True",
                accion: () =>
                {
                    var res = servicio.Evaluar(3000m, 1000m, 5000m, 12, 0, 1, 0, false);
                    return $"Neto: {res.IngresoNeto}, Capacidad: {res.CapacidadPago}, Disponible: {res.DisponiblePrestamo}, Interes: {res.InteresTotal}, Cuota: {res.CuotaMensual}, Viable: {res.EsCuotaViable}";
                });

            // 2. Sanitización de entradas financieras negativas y plazo cero
            runner.Evaluar(
                nombreEscenario: "Sanitización de montos negativos y corrección de plazo cero",
                datoEntrada: "Ingreso: -500, Gastos: -200, Monto: -1000, Plazo: 0",
                esperado: "Ingreso: 0, Gastos: 0, Monto: 0, Plazo: 1",
                accion: () =>
                {
                    var res = servicio.Evaluar(-500m, -200m, -1000m, 0, 0, 0, 0, false);
                    return $"Ingreso: {res.IngresoMensual}, Gastos: {res.GastosMensuales}, Monto: {res.MontoSolicitado}, Plazo: {res.PlazoMeses}";
                });

            // 3. Nivel de Riesgo CRÍTICO por Deudas Vencidas (Regla 1 de seguridad)
            runner.Evaluar(
                nombreEscenario: "Riesgo CRÍTICO por mora o deudas vencidas registradas",
                datoEntrada: "Deudas vencidas: 1",
                esperado: "Riesgo: CRÍTICO | Color: #ef4444 | Aprobable: False",
                accion: () =>
                {
                    var res = servicio.Evaluar(5000m, 1000m, 3000m, 10, 0, 2, 1, false);
                    return $"Riesgo: {res.NivelRiesgo} | Color: {res.ColorHex} | Aprobable: {res.EsAprobableOrdinario}";
                });

            // 4. Nivel de Riesgo ALTO por Deudas Activas sin Vencidas (Regla 2)
            runner.Evaluar(
                nombreEscenario: "Riesgo ALTO por deudas activas preexistentes en curso",
                datoEntrada: "Deudas activas: 1, Deudas vencidas: 0",
                esperado: "Riesgo: ALTO | Color: #f97316 | Aprobable: False",
                accion: () =>
                {
                    var res = servicio.Evaluar(5000m, 1000m, 3000m, 10, 1, 2, 0, true);
                    return $"Riesgo: {res.NivelRiesgo} | Color: {res.ColorHex} | Aprobable: {res.EsAprobableOrdinario}";
                });

            // 5. Nivel de Riesgo MEDIO por falta de historial previo (Regla 3)
            runner.Evaluar(
                nombreEscenario: "Riesgo MEDIO por cliente nuevo sin historial financiero previo",
                datoEntrada: "Deudas pagadas: 0, Deudas activas: 0, Deudas vencidas: 0",
                esperado: "Riesgo: MEDIO | Color: #eab308 | Aprobable: True",
                accion: () =>
                {
                    var res = servicio.Evaluar(4000m, 1500m, 3000m, 12, 0, 0, 0, false);
                    return $"Riesgo: {res.NivelRiesgo} | Color: {res.ColorHex} | Aprobable: {res.EsAprobableOrdinario}";
                });

            // 6. Nivel de Riesgo MEDIO por monto solicitado mayor al disponible (Regla 4)
            runner.Evaluar(
                nombreEscenario: "Riesgo MEDIO cuando monto solicitado excede el disponible estimado",
                datoEntrada: "Capacidad Disponible: 6300, Monto Solicitado: 8000",
                esperado: "Riesgo: MEDIO con advertencia de monto excedido",
                accion: () =>
                {
                    var res = servicio.Evaluar(2000m, 500m, 8000m, 24, 0, 3, 0, false);
                    bool tieneAdvertencia = res.Advertencias.Any(a => a.Contains("Monto excedido"));
                    return (res.NivelRiesgo == "MEDIO" && tieneAdvertencia)
                        ? "Riesgo: MEDIO con advertencia de monto excedido"
                        : $"Riesgo: {res.NivelRiesgo}";
                });

            // 7. Nivel de Riesgo BAJO por historial solvente y monto dentro de capacidad (Regla 5)
            runner.Evaluar(
                nombreEscenario: "Riesgo BAJO con solvencia óptima e historial limpio",
                datoEntrada: "Deudas pagadas: 2, Sin moras, Monto dentro de capacidad",
                esperado: "Riesgo: BAJO | Color: #22c55e | Aprobable: True",
                accion: () =>
                {
                    var res = servicio.Evaluar(4000m, 1000m, 5000m, 12, 0, 2, 0, false);
                    return $"Riesgo: {res.NivelRiesgo} | Color: {res.ColorHex} | Aprobable: {res.EsAprobableOrdinario}";
                });

            // 8. Detección de inconsistencia declarativa (ocultamiento de créditos)
            runner.Evaluar(
                nombreEscenario: "Detección de inconsistencia: Declara no tener créditos pero registra deudas activas",
                datoEntrada: "declaroOtrosCreditos=false, deudasActivas=1",
                esperado: "Inconsistencia: True",
                accion: () =>
                {
                    var res = servicio.Evaluar(3000m, 1000m, 2000m, 6, 1, 0, 0, false);
                    return $"Inconsistencia: {res.TieneInconsistenciaDeclarativa}";
                });

            // 9. Consolidación de deudas internas CrediPlus con historial externo
            runner.Evaluar(
                nombreEscenario: "Consolidación de deudas internas CrediPlus con historial crediticio",
                datoEntrada: "Deudas pagadas externas: 1, Internas: 1 -> Total pagadas: 2",
                esperado: "Activas: 1, Pagadas: 2, Vencidas: 1, Riesgo: CRÍTICO",
                accion: () =>
                {
                    var res = servicio.Evaluar(
                        ingresoMensual: 4000m,
                        gastosMensuales: 1000m,
                        montoSolicitado: 2000m,
                        plazoMeses: 6,
                        deudasActivas: 0,
                        deudasPagadas: 1,
                        deudasVencidas: 0,
                        declaroOtrosCreditos: false,
                        totalFilasHistorialDetallado: -1,
                        deudasCrediPlusActivas: 1,
                        deudasCrediPlusPagadas: 1,
                        deudasCrediPlusVencidas: 1
                    );
                    return $"Activas: {res.DeudasActivas}, Pagadas: {res.DeudasPagadas}, Vencidas: {res.DeudasVencidas}, Riesgo: {res.NivelRiesgo}";
                });

            // 10. Bloqueo de aprobación para solicitudes no pendientes (ej. ya rechazada)
            runner.Evaluar(
                nombreEscenario: "Rechazo de aprobación a solicitudes en estado inválido (ya Rechazada)",
                datoEntrada: "Solicitud con Estado: 'Rechazado'",
                esperado: "Bloqueado: La solicitud no admite aprobación ordinaria",
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);

                    var usuario = new Usuario
                    {
                        Id = 1,
                        Nombre = "Juan",
                        Apellido = "Pérez",
                        Dni = "11223344",
                        Correo = "juan@gmail.com",
                        Celular = "911223344",
                        Genero = "Masculino",
                        Rol = "Cliente",
                        clave = "hash"
                    };
                    context.Usuario.Add(usuario);

                    var solicitud = new SolicitudCredito
                    {
                        Id_Solicitud = 50,
                        NumeroSolicitud = "0050",
                        MontoSolicitado = 2000m,
                        PlazoMeses = 6,
                        Estado = "Rechazado",
                        Usuario_Id_Usuario = 1,
                        USUARIO = usuario
                    };
                    context.SOLICITUD_CREDITO.Add(solicitud);
                    context.SaveChanges();

                    var controller = CrearAnalistaController(context);
                    var res = controller.AprobarSolicitud(50, "Aprobando rechazada", "Bajo");
                    var json = (JsonResult)res;
                    bool success = (bool)(ObtenerPropiedad(json.Value, "success") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");

                    return (!success && (msg?.Contains("no admite aprobación ordinaria") ?? false))
                        ? "Bloqueado: La solicitud no admite aprobación ordinaria"
                        : "Aprobado indebidamente";
                });

            // 11. Bloqueo de seguridad: No se puede aprobar solicitud con riesgo CRÍTICO
            runner.Evaluar(
                nombreEscenario: "Bloqueo estricto de seguridad: Prohibido aprobar solicitudes con riesgo CRÍTICO",
                datoEntrada: "Solicitud de cliente con riesgo CRÍTICO por deudas vencidas",
                esperado: "Bloqueado: BLOQUEO DE SEGURIDAD: No se puede aprobar una solicitud con nivel de riesgo CRÍTICO",
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);

                    var usuario = new Usuario
                    {
                        Id = 10,
                        Nombre = "Carlos",
                        Apellido = "Mendoza",
                        Dni = "44556677",
                        Correo = "carlos@gmail.com",
                        Celular = "988776655",
                        Rol = "Cliente",
                        Genero = "Masculino",
                        clave = "hash"
                    };
                    context.Usuario.Add(usuario);

                    var perfil = new PerfilFinanciero
                    {
                        Id_PerfilFinanciero = 10,
                        IngresoMensual = 4000m,
                        EgresoMensual = 1000m,
                        MotivoPrestamo = "Negocio",
                        Ocupacion = "Comerciante",
                        SOLICITUD_CREDITO_Id_Solicitud = 100
                    };
                    context.PERFIL_FINANCIERO.Add(perfil);

                    var solicitud = new SolicitudCredito
                    {
                        Id_Solicitud = 100,
                        NumeroSolicitud = "0100",
                        MontoSolicitado = 3000m,
                        PlazoMeses = 12,
                        InteresEstimado = 10,
                        Estado = "Pendiente",
                        Usuario_Id_Usuario = 10,
                        USUARIO = usuario,
                        PERFIL_FINANCIERO = perfil
                    };
                    context.SOLICITUD_CREDITO.Add(solicitud);

                    context.RESUMEN_CREDITICIO.Add(new ResumenCredito
                    {
                        Usuario_Id = 10,
                        DeudasActivas = 0,
                        DeudasPagadas = 1,
                        DeudasVencidas = 1
                    });
                    context.SaveChanges();

                    var controller = CrearAnalistaController(context);
                    var res = controller.AprobarSolicitud(100, "Forzar aprobación", "Critico");
                    var json = (JsonResult)res;
                    bool success = (bool)(ObtenerPropiedad(json.Value, "success") ?? true);
                    string? msg = (string?)ObtenerPropiedad(json.Value, "mensaje");

                    return (!success && (msg?.Contains("BLOQUEO DE SEGURIDAD: No se puede aprobar una solicitud con nivel de riesgo CRÍTICO") ?? false))
                        ? "Bloqueado: BLOQUEO DE SEGURIDAD: No se puede aprobar una solicitud con nivel de riesgo CRÍTICO"
                        : "Aprobado indebidamente";
                });

            // 12. Aprobación ordinaria exitosa con cliente solvente y riesgo bajo
            runner.Evaluar(
                nombreEscenario: "Aprobación exitosa: Transición de estado a 'Aprobado' y generación de cronograma",
                datoEntrada: "Solicitud Pendiente, Monto: 6000, Plazo: 6 meses, Historial limpio",
                esperado: "Aprobado exitoso: Estado 'Aprobado' con cronograma generado",
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);

                    var usuario = new Usuario
                    {
                        Id = 20,
                        Nombre = "Elena",
                        Apellido = "Ríos",
                        Dni = "55667788",
                        Correo = "elena@gmail.com",
                        Celular = "977889900",
                        Rol = "Cliente",
                        Genero = "Femenino",
                        clave = "hash"
                    };
                    context.Usuario.Add(usuario);

                    var perfil = new PerfilFinanciero
                    {
                        Id_PerfilFinanciero = 20,
                        IngresoMensual = 6000m,
                        EgresoMensual = 1000m,
                        MotivoPrestamo = "Construcción",
                        Ocupacion = "Ingeniero",
                        SOLICITUD_CREDITO_Id_Solicitud = 200
                    };
                    context.PERFIL_FINANCIERO.Add(perfil);

                    var solicitud = new SolicitudCredito
                    {
                        Id_Solicitud = 200,
                        NumeroSolicitud = "0200",
                        MontoSolicitado = 6000m,
                        PlazoMeses = 6,
                        InteresEstimado = 10m,
                        Estado = "Pendiente",
                        Usuario_Id_Usuario = 20,
                        USUARIO = usuario,
                        PERFIL_FINANCIERO = perfil
                    };
                    context.SOLICITUD_CREDITO.Add(solicitud);

                    context.RESUMEN_CREDITICIO.Add(new ResumenCredito
                    {
                        Usuario_Id = 20,
                        DeudasActivas = 0,
                        DeudasPagadas = 2,
                        DeudasVencidas = 0
                    });
                    context.SaveChanges();

                    var controller = CrearAnalistaController(context);
                    var res = controller.AprobarSolicitud(200, "Aprobación solvente", "Bajo");
                    var json = (JsonResult)res;
                    bool success = (bool)(ObtenerPropiedad(json.Value, "success") ?? false);

                    var solEnDb = context.SOLICITUD_CREDITO.Find(200);
                    var cronograma = context.CRONOGRAMA.FirstOrDefault(c => c.SOLICITUD_CREDITO_Id_Solicitud == 200);

                    return (success && solEnDb?.Estado == "Aprobado" && cronograma != null)
                        ? "Aprobado exitoso: Estado 'Aprobado' con cronograma generado"
                        : "Error en aprobación";
                });

            // ==========================================
            // BLINDAJE SERVER-SIDE CONTRA MANIPULACIÓN DOM (F12)
            // ==========================================

            // Helper para registrar un cliente de prueba en la BD en memoria
            Action<UsuarioContext> asegurarClientePrueba = (ctx) =>
            {
                if (!ctx.Usuario.Any(u => u.Dni == "72527818"))
                {
                    ctx.Usuario.Add(new Usuario
                    {
                        Id = 1,
                        Nombre = "Rafael",
                        Apellido = "Rosales",
                        Dni = "72527818",
                        Correo = "xlaforosalespapuico@gmail.com",
                        Celular = "981999121",
                        Rol = "Cliente",
                        EstadoActivo = true,
                        clave = "$2a$12$eXampleHashCrediPlus2026SecureBcryptHashValuePadding60"
                    });
                    ctx.SaveChanges();
                }
            };

            // Escenario 16: Blindaje DOM - Monto inferior a S/ 1,000
            runner.Evaluar(
                nombreEscenario: "Blindaje DOM: Rechazo de monto inferior al mínimo permitido (S/ 500)",
                datoEntrada: "Monto: 500, Plazo: 12",
                esperado: true,
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);
                    asegurarClientePrueba(context);

                    var controller = CrearLoginController(context);
                    var res = controller.RegistrarSolicitudCredito(
                        montoSolicitado: 500m,
                        plazoMeses: 12,
                        ingresoMensual: 3000m,
                        egresoMensual: 1000m,
                        tieneOtrosCreditos: "No",
                        motivoOtro: "",
                        ocupacion: "Ingeniero",
                        motivoPrestamo: "Negocio",
                        metodoPago: "Transferencia Bancaria",
                        entidadPago: "BCP",
                        numeroCuentaPago: "191-12345678-0-12",
                        titularCuenta: "Rafael Rosales"
                    );

                    var redirect = Assert.IsType<RedirectToActionResult>(res);
                    string? mensaje = controller.TempData["Mensaje"]?.ToString();
                    return redirect.ActionName == "SolicitarCredito" && mensaje != null && mensaje.Contains("monto solicitado debe estar entre");
                });

            // Escenario 17: Blindaje DOM - Monto superior a S/ 50,000 (manipulación con DevTools)
            runner.Evaluar(
                nombreEscenario: "Blindaje DOM: Rechazo de monto superior al máximo permitido (S/ 70,000)",
                datoEntrada: "Monto: 70000, Plazo: 24",
                esperado: true,
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);
                    asegurarClientePrueba(context);

                    var controller = CrearLoginController(context);
                    var res = controller.RegistrarSolicitudCredito(
                        montoSolicitado: 70000m,
                        plazoMeses: 24,
                        ingresoMensual: 10000m,
                        egresoMensual: 2000m,
                        tieneOtrosCreditos: "No",
                        motivoOtro: "",
                        ocupacion: "Ingeniero",
                        motivoPrestamo: "Negocio",
                        metodoPago: "Transferencia Bancaria",
                        entidadPago: "BCP",
                        numeroCuentaPago: "191-12345678-0-12",
                        titularCuenta: "Rafael Rosales"
                    );

                    var redirect = Assert.IsType<RedirectToActionResult>(res);
                    string? mensaje = controller.TempData["Mensaje"]?.ToString();
                    return redirect.ActionName == "SolicitarCredito" && mensaje != null && mensaje.Contains("monto solicitado debe estar entre");
                });

            // Escenario 18: Blindaje DOM - Plazo no permitido (ej. 10 meses)
            runner.Evaluar(
                nombreEscenario: "Blindaje DOM: Rechazo de plazo no admitido en políticas (10 meses)",
                datoEntrada: "Plazo: 10 meses",
                esperado: true,
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);
                    asegurarClientePrueba(context);

                    var controller = CrearLoginController(context);
                    var res = controller.RegistrarSolicitudCredito(
                        montoSolicitado: 5000m,
                        plazoMeses: 10,
                        ingresoMensual: 3000m,
                        egresoMensual: 1000m,
                        tieneOtrosCreditos: "No",
                        motivoOtro: "",
                        ocupacion: "Ingeniero",
                        motivoPrestamo: "Negocio",
                        metodoPago: "Transferencia Bancaria",
                        entidadPago: "BCP",
                        numeroCuentaPago: "191-12345678-0-12",
                        titularCuenta: "Rafael Rosales"
                    );

                    var redirect = Assert.IsType<RedirectToActionResult>(res);
                    string? mensaje = controller.TempData["Mensaje"]?.ToString();
                    return redirect.ActionName == "SolicitarCredito" && mensaje != null && mensaje.Contains("plazo seleccionado no es válido");
                });

            // Escenario 19: Blindaje DOM - Ingreso mensual cero o negativo
            runner.Evaluar(
                nombreEscenario: "Blindaje DOM: Rechazo de ingreso mensual menor o igual a cero (S/ 0)",
                datoEntrada: "Ingreso: 0, Egreso: 500",
                esperado: true,
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);
                    asegurarClientePrueba(context);

                    var controller = CrearLoginController(context);
                    var res = controller.RegistrarSolicitudCredito(
                        montoSolicitado: 5000m,
                        plazoMeses: 12,
                        ingresoMensual: 0m,
                        egresoMensual: 500m,
                        tieneOtrosCreditos: "No",
                        motivoOtro: "",
                        ocupacion: "Ingeniero",
                        motivoPrestamo: "Negocio",
                        metodoPago: "Transferencia Bancaria",
                        entidadPago: "BCP",
                        numeroCuentaPago: "191-12345678-0-12",
                        titularCuenta: "Rafael Rosales"
                    );

                    var redirect = Assert.IsType<RedirectToActionResult>(res);
                    string? mensaje = controller.TempData["Mensaje"]?.ToString();
                    return redirect.ActionName == "SolicitarCredito" && mensaje != null && mensaje.Contains("ingreso mensual debe ser mayor a cero");
                });

            // Escenario 20: Blindaje DOM - Egreso mensual negativo
            runner.Evaluar(
                nombreEscenario: "Blindaje DOM: Rechazo de egreso mensual negativo (S/ -200)",
                datoEntrada: "Ingreso: 3000, Egreso: -200",
                esperado: true,
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);
                    asegurarClientePrueba(context);

                    var controller = CrearLoginController(context);
                    var res = controller.RegistrarSolicitudCredito(
                        montoSolicitado: 5000m,
                        plazoMeses: 12,
                        ingresoMensual: 3000m,
                        egresoMensual: -200m,
                        tieneOtrosCreditos: "No",
                        motivoOtro: "",
                        ocupacion: "Ingeniero",
                        motivoPrestamo: "Negocio",
                        metodoPago: "Transferencia Bancaria",
                        entidadPago: "BCP",
                        numeroCuentaPago: "191-12345678-0-12",
                        titularCuenta: "Rafael Rosales"
                    );

                    var redirect = Assert.IsType<RedirectToActionResult>(res);
                    string? mensaje = controller.TempData["Mensaje"]?.ToString();
                    return redirect.ActionName == "SolicitarCredito" && mensaje != null && mensaje.Contains("egreso mensual no puede ser un valor negativo");
                });

            // Escenario 21: Blindaje DOM - Egreso superior o igual al ingreso
            runner.Evaluar(
                nombreEscenario: "Blindaje DOM: Rechazo de egreso superior al ingreso (Ingreso S/ 1,500 vs Egreso S/ 2,000)",
                datoEntrada: "Ingreso: 1500, Egreso: 2000",
                esperado: true,
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);
                    asegurarClientePrueba(context);

                    var controller = CrearLoginController(context);
                    var res = controller.RegistrarSolicitudCredito(
                        montoSolicitado: 5000m,
                        plazoMeses: 12,
                        ingresoMensual: 1500m,
                        egresoMensual: 2000m,
                        tieneOtrosCreditos: "No",
                        motivoOtro: "",
                        ocupacion: "Ingeniero",
                        motivoPrestamo: "Negocio",
                        metodoPago: "Transferencia Bancaria",
                        entidadPago: "BCP",
                        numeroCuentaPago: "191-12345678-0-12",
                        titularCuenta: "Rafael Rosales"
                    );

                    var redirect = Assert.IsType<RedirectToActionResult>(res);
                    string? mensaje = controller.TempData["Mensaje"]?.ToString();
                    return redirect.ActionName == "SolicitarCredito" && mensaje != null && mensaje.Contains("ingreso mensual debe ser estrictamente superior");
                });

            // Escenario 22: Blindaje DOM - Campos de método de pago faltantes
            runner.Evaluar(
                nombreEscenario: "Blindaje DOM: Rechazo por omisión de campos bancarios obligatorios",
                datoEntrada: "Campos de cuenta vacíos",
                esperado: true,
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);
                    asegurarClientePrueba(context);

                    var controller = CrearLoginController(context);
                    var res = controller.RegistrarSolicitudCredito(
                        montoSolicitado: 5000m,
                        plazoMeses: 12,
                        ingresoMensual: 3000m,
                        egresoMensual: 1000m,
                        tieneOtrosCreditos: "No",
                        motivoOtro: "",
                        ocupacion: "Ingeniero",
                        motivoPrestamo: "Negocio",
                        metodoPago: "", // Vacío
                        entidadPago: "BCP",
                        numeroCuentaPago: "191-12345678-0-12",
                        titularCuenta: "Rafael Rosales"
                    );

                    var redirect = Assert.IsType<RedirectToActionResult>(res);
                    string? mensaje = controller.TempData["Mensaje"]?.ToString();
                    return redirect.ActionName == "SolicitarCredito" && mensaje != null && mensaje.Contains("método de pago son obligatorios");
                });

            // Escenario 23: Blindaje DOM/API - Edición de solicitud con plazo no permitido
            runner.Evaluar(
                nombreEscenario: "Blindaje DOM: Rechazo de edición con plazo no permitido (15 meses)",
                datoEntrada: "Plazo: 15",
                esperado: true,
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);
                    asegurarClientePrueba(context);
                    var solicitud = new SolicitudCredito
                    {
                        Id_Solicitud = 10,
                        NumeroSolicitud = "SOL-TEST-010",
                        Usuario_Id_Usuario = 1,
                        MontoSolicitado = 5000m,
                        PlazoMeses = 12,
                        Estado = "En Evaluación",
                        FechaSolicitud = DateTime.UtcNow
                    };
                    context.SOLICITUD_CREDITO.Add(solicitud);
                    context.SaveChanges();

                    var controller = CrearLoginController(context);
                    var res = controller.EditarSolicitud(10, 6000m, 15);
                    var jsonRes = Assert.IsType<JsonResult>(res);
                    var ok = ObtenerPropiedad(jsonRes.Value, "ok") as bool?;
                    var mensaje = ObtenerPropiedad(jsonRes.Value, "mensaje") as string;
                    return ok == false && mensaje != null && mensaje.Contains("plazo seleccionado no es válido");
                });

            // Escenario 24: Blindaje DOM/API - Cancelación con crédito en estado no aprobado
            runner.Evaluar(
                nombreEscenario: "Blindaje DOM: Rechazo de cancelación sobre crédito en estado 'Pendiente'",
                datoEntrada: "Estado: Pendiente, Monto: 0",
                esperado: true,
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);
                    asegurarClientePrueba(context);
                    var solicitud = new SolicitudCredito
                    {
                        Id_Solicitud = 20,
                        NumeroSolicitud = "SOL-TEST-020",
                        Usuario_Id_Usuario = 1,
                        MontoSolicitado = 5000m,
                        PlazoMeses = 12,
                        Estado = "Pendiente",
                        FechaSolicitud = DateTime.UtcNow
                    };
                    context.SOLICITUD_CREDITO.Add(solicitud);
                    context.SaveChanges();

                    var controller = CrearLoginController(context);
                    var res = controller.RegistrarCancelacion(20, 0m, "Transferencia", "OP-123", "Ya no necesito el préstamo");
                    var jsonRes = Assert.IsType<JsonResult>(res);
                    var ok = ObtenerPropiedad(jsonRes.Value, "ok") as bool?;
                    var mensaje = ObtenerPropiedad(jsonRes.Value, "mensaje") as string;
                    return ok == false && mensaje != null && mensaje.Contains("Solo se puede solicitar cancelación sobre créditos aprobados");
                });

            // Escenario 25: Blindaje DOM/API - Pago de cuota con monto no positivo
            runner.Evaluar(
                nombreEscenario: "Blindaje DOM: Rechazo de pago de cuota con monto <= 0",
                datoEntrada: "MontoPagado: -50",
                esperado: true,
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);
                    asegurarClientePrueba(context);
                    var solicitud = new SolicitudCredito
                    {
                        Id_Solicitud = 30,
                        NumeroSolicitud = "SOL-TEST-030",
                        Usuario_Id_Usuario = 1,
                        MontoSolicitado = 5000m,
                        PlazoMeses = 12,
                        Estado = "Aprobado",
                        FechaSolicitud = DateTime.UtcNow
                    };
                    context.SOLICITUD_CREDITO.Add(solicitud);
                    var cuota = new Cuota
                    {
                        Id_Cuota = 101,
                        SOLICITUD_CREDITO_Id_Solicitud = 30,
                        NumeroCuota = 1,
                        MontoCuota = 450m,
                        Estado = "Pendiente",
                        FechaVencimiento = DateTime.UtcNow.AddDays(15),
                        FechaLimitePago = DateTime.UtcNow.AddDays(15),
                        SOLICITUD_CREDITO = solicitud
                    };
                    context.CUOTA.Add(cuota);
                    context.SaveChanges();

                    var controller = CrearLoginController(context);
                    var res = controller.RegistrarPagoCuota(101, -50m, "BCP", "BCP", "OP-999");
                    var jsonRes = Assert.IsType<JsonResult>(res);
                    var ok = ObtenerPropiedad(jsonRes.Value, "ok") as bool?;
                    var mensaje = ObtenerPropiedad(jsonRes.Value, "mensaje") as string;
                    return ok == false && mensaje != null && mensaje.Contains("monto pagado debe ser mayor a cero");
                });

            // Escenario 26: Blindaje Analista - GuardarPerfilFinanciero rechaza plazo no permitido
            runner.Evaluar(
                nombreEscenario: "Blindaje Analista: Rechazo de perfil financiero con plazo inválido (13 meses)",
                datoEntrada: "Plazo: 13 meses",
                esperado: true,
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);
                    asegurarClientePrueba(context);
                    var solicitud = new SolicitudCredito
                    {
                        Id_Solicitud = 40,
                        NumeroSolicitud = "SOL-TEST-040",
                        Usuario_Id_Usuario = 1,
                        MontoSolicitado = 5000m,
                        PlazoMeses = 12,
                        Estado = "Pendiente",
                        FechaSolicitud = DateTime.UtcNow
                    };
                    context.SOLICITUD_CREDITO.Add(solicitud);
                    context.SaveChanges();

                    var controller = CrearAnalistaController(context);
                    var res = controller.GuardarPerfilFinanciero(40, 5000m, 13, 3000m, 1000m);
                    var jsonRes = Assert.IsType<JsonResult>(res);
                    var ok = ObtenerPropiedad(jsonRes.Value, "ok") as bool?;
                    var mensaje = ObtenerPropiedad(jsonRes.Value, "mensaje") as string;
                    return ok == false && mensaje != null && mensaje.Contains("plazo seleccionado no es válido");
                });

            // Escenario 27: Blindaje DOM/API - Inmutabilidad de Crédito Aprobado
            runner.Evaluar(
                nombreEscenario: "Blindaje DOM: Rechazo de edición sobre crédito en estado 'Aprobado'",
                datoEntrada: "Estado: Aprobado, Monto: 7000, Plazo: 24",
                esperado: true,
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);
                    asegurarClientePrueba(context);
                    var solicitud = new SolicitudCredito
                    {
                        Id_Solicitud = 50,
                        NumeroSolicitud = "SOL-TEST-050",
                        Usuario_Id_Usuario = 1,
                        MontoSolicitado = 5000m,
                        PlazoMeses = 12,
                        Estado = "Aprobado",
                        FechaSolicitud = DateTime.UtcNow
                    };
                    context.SOLICITUD_CREDITO.Add(solicitud);
                    context.SaveChanges();

                    var controller = CrearLoginController(context);
                    var res = controller.EditarSolicitud(50, 7000m, 24);
                    var jsonRes = Assert.IsType<JsonResult>(res);
                    var ok = ObtenerPropiedad(jsonRes.Value, "ok") as bool?;
                    var mensaje = ObtenerPropiedad(jsonRes.Value, "mensaje") as string;
                    return ok == false && mensaje != null && mensaje.Contains("No puedes editar esta solicitud en su estado actual");
                });

            // Escenario 28: Blindaje DOM/API - Control de Importe de Cuota (Anti-pago parcial de céntimos)
            runner.Evaluar(
                nombreEscenario: "Blindaje DOM: Rechazo de pago con importe menor al de la cuota (S/ 0.50 vs S/ 450.00)",
                datoEntrada: "MontoCuota: 450, MontoPagado: 0.50",
                esperado: true,
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);
                    asegurarClientePrueba(context);
                    var solicitud = new SolicitudCredito
                    {
                        Id_Solicitud = 60,
                        NumeroSolicitud = "SOL-TEST-060",
                        Usuario_Id_Usuario = 1,
                        MontoSolicitado = 5000m,
                        PlazoMeses = 12,
                        Estado = "Aprobado",
                        FechaSolicitud = DateTime.UtcNow
                    };
                    context.SOLICITUD_CREDITO.Add(solicitud);
                    var cuota = new Cuota
                    {
                        Id_Cuota = 201,
                        SOLICITUD_CREDITO_Id_Solicitud = 60,
                        NumeroCuota = 1,
                        MontoCuota = 450m,
                        Estado = "Pendiente",
                        FechaVencimiento = DateTime.UtcNow.AddDays(15),
                        FechaLimitePago = DateTime.UtcNow.AddDays(15),
                        SOLICITUD_CREDITO = solicitud
                    };
                    context.CUOTA.Add(cuota);
                    context.SaveChanges();

                    var controller = CrearLoginController(context);
                    var res = controller.RegistrarPagoCuota(201, 0.50m, "BCP", "BCP", "OP-111");
                    var jsonRes = Assert.IsType<JsonResult>(res);
                    var ok = ObtenerPropiedad(jsonRes.Value, "ok") as bool?;
                    var mensaje = ObtenerPropiedad(jsonRes.Value, "mensaje") as string;
                    return ok == false && mensaje != null && mensaje.Contains("no puede ser menor al monto de la cuota");
                });

            // Escenario 29: Blindaje Analista - No-mutación en GET GestionDePago (Idempotencia)
            runner.Evaluar(
                nombreEscenario: "Blindaje Analista: Invocación GET de GestionDePago no auto-aprueba pagos pendientes",
                datoEntrada: "PAGO_CUOTA: Pendiente validación",
                esperado: true,
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);
                    asegurarClientePrueba(context);
                    var solicitud = new SolicitudCredito
                    {
                        Id_Solicitud = 70,
                        NumeroSolicitud = "SOL-TEST-070",
                        Usuario_Id_Usuario = 1,
                        MontoSolicitado = 5000m,
                        PlazoMeses = 12,
                        Estado = "Aprobado",
                        FechaSolicitud = DateTime.UtcNow
                    };
                    context.SOLICITUD_CREDITO.Add(solicitud);
                    var cuota = new Cuota
                    {
                        Id_Cuota = 301,
                        SOLICITUD_CREDITO_Id_Solicitud = 70,
                        NumeroCuota = 1,
                        MontoCuota = 450m,
                        Estado = "Pendiente aprobación",
                        FechaVencimiento = DateTime.UtcNow.AddDays(15),
                        FechaLimitePago = DateTime.UtcNow.AddDays(15),
                        SOLICITUD_CREDITO = solicitud
                    };
                    context.CUOTA.Add(cuota);
                    var pago = new prestamoscreditos.Models.PagoCuota
                    {
                        Id_PagoCuota = 501,
                        Id_Cuota = 301,
                        MontoPagado = 450m,
                        MetodoPago = "Transferencia",
                        EntidadPago = "BCP",
                        CodigoOperacion = "OP-777",
                        FechaPago = DateTime.UtcNow,
                        Estado = "Pendiente validación"
                    };
                    context.PAGO_CUOTA.Add(pago);
                    context.SaveChanges();

                    var controller = CrearAnalistaController(context);
                    var res = controller.GestionDePago();

                    // Comprobar que en la base de datos el pago siga en 'Pendiente validación' y NO haya mutado a 'Aprobado'
                    var pagoDb = context.PAGO_CUOTA.First(p => p.Id_PagoCuota == 501);
                    var cuotaDb = context.CUOTA.First(c => c.Id_Cuota == 301);

                    return pagoDb.Estado == "Pendiente validación" && cuotaDb.Estado == "Pendiente aprobación";
                });

            // Escenario 30: Blindaje DOM/API - Anti-IDOR en ObtenerInfoSolicitud
            runner.Evaluar(
                nombreEscenario: "Blindaje DOM: ObtenerInfoSolicitud rechaza ID de solicitud perteneciente a otro cliente",
                datoEntrada: "IdSolicitud: 999 (Ajena)",
                esperado: true,
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);
                    asegurarClientePrueba(context); // Cliente ID = 1
                    var solicitudAjena = new SolicitudCredito
                    {
                        Id_Solicitud = 999,
                        NumeroSolicitud = "SOL-TEST-999",
                        Usuario_Id_Usuario = 99, // Otro usuario
                        MontoSolicitado = 5000m,
                        PlazoMeses = 12,
                        Estado = "En Evaluación",
                        FechaSolicitud = DateTime.UtcNow
                    };
                    context.SOLICITUD_CREDITO.Add(solicitudAjena);
                    context.SaveChanges();

                    var controller = CrearLoginController(context);
                    var res = controller.ObtenerInfoSolicitud(999);
                    var jsonRes = Assert.IsType<JsonResult>(res);
                    var analista = ObtenerPropiedad(jsonRes.Value, "analista") as string;
                    var correo = ObtenerPropiedad(jsonRes.Value, "correo") as string;

                    return string.IsNullOrEmpty(analista) && string.IsNullOrEmpty(correo);
                });

            runner.VerificarTodosAprobados();
        }
    }
}
