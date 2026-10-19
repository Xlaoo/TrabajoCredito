using System;
using System.Collections.Generic;
using System.Linq;
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

            runner.VerificarTodosAprobados();
        }
    }
}
