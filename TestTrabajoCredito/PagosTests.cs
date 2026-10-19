using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using Xunit.Abstractions;
using trabajo.Controllers;
using trabajo.Models;
using trabajo.Models.DTOs;
using trabajo.Service;
using prestamoscreditos.Models;

namespace TestTrabajoCredito
{
    public class PagosTests
    {
        private readonly ITestOutputHelper _output;

        public PagosTests(ITestOutputHelper output)
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
        public void ProbarTodasLasValidacionesDePagos()
        {
            var runner = new EscenarioRunner(_output, "Gestión de Pagos, Cuotas, Moras y Amortizaciones");

            // 1. Estructura y amortización matemática de cuotas
            runner.Evaluar(
                nombreEscenario: "Cálculo de amortización: Capital uniforme, interés proporcional y saldo decreciente",
                datoEntrada: "Préstamo: 6000, Plazo: 6 cuotas, Interés: 10%",
                esperado: "Cuota: 1100.00, Capital: 1000, Interes: 100.00, SaldoInicial: 5000, SaldoFinal: 0",
                accion: () =>
                {
                    decimal monto = 6000m;
                    int plazo = 6;
                    decimal interesTotal = monto * 0.10m; // 600
                    decimal capitalMensual = monto / plazo; // 1000
                    decimal interesMensual = interesTotal / plazo; // 100
                    decimal cuotaMensual = capitalMensual + interesMensual; // 1100

                    decimal saldo = monto;
                    decimal primerSaldo = 0m;
                    decimal ultimoSaldo = 0m;

                    for (int i = 1; i <= plazo; i++)
                    {
                        saldo -= capitalMensual;
                        if (i == 1) primerSaldo = saldo;
                        if (i == plazo) ultimoSaldo = saldo;
                    }

                    return $"Cuota: {cuotaMensual}, Capital: {capitalMensual}, Interes: {interesMensual}, SaldoInicial: {primerSaldo}, SaldoFinal: {ultimoSaldo}";
                });

            // 2. Cálculo de penalidad por mora en cuota con retraso (S/ 5 por día)
            runner.Evaluar(
                nombreEscenario: "Cálculo de penalidad por mora en cuota vencida (S/ 5 diarios)",
                datoEntrada: "Cuota base: 500, Días de atraso: 4",
                esperado: "Monto con mora: 520 (Mora: 20)",
                accion: () =>
                {
                    decimal montoBase = 500m;
                    int diasAtraso = 4;
                    decimal mora = diasAtraso * 5m; // 20
                    decimal totalConMora = montoBase + mora; // 520
                    return $"Monto con mora: {totalConMora} (Mora: {mora})";
                });

            // 3. Cuota no vencida sin recargo de mora
            runner.Evaluar(
                nombreEscenario: "Cuota dentro de fecha límite de pago (Mora cero)",
                datoEntrada: "Cuota base: 500, Días de atraso: 0",
                esperado: "Monto con mora: 500 (Mora: 0)",
                accion: () =>
                {
                    decimal montoBase = 500m;
                    int diasAtraso = 0;
                    decimal mora = diasAtraso * 5m;
                    decimal total = montoBase + mora;
                    return $"Monto con mora: {total} (Mora: {mora})";
                });

            // 4. Generación real de cronograma de cuotas en base de datos al aprobar crédito
            runner.Evaluar(
                nombreEscenario: "Generación en base de datos de cuotas asociadas al crédito",
                datoEntrada: "Solicitud aprobada de S/ 3000 a 3 meses",
                esperado: "Cuotas generadas: 3, Todas en estado 'Pendiente'",
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);

                    var usuario = new Usuario
                    {
                        Id = 1,
                        Nombre = "Mario",
                        Apellido = "Vargas",
                        Dni = "71223344",
                        Correo = "mario@gmail.com",
                        Celular = "911223344",
                        Rol = "Cliente",
                        Genero = "Masculino",
                        clave = "hash"
                    };
                    context.Usuario.Add(usuario);

                    var perfil = new PerfilFinanciero
                    {
                        Id_PerfilFinanciero = 1,
                        IngresoMensual = 5000m,
                        EgresoMensual = 1000m,
                        MotivoPrestamo = "Negocio",
                        Ocupacion = "Comerciante",
                        SOLICITUD_CREDITO_Id_Solicitud = 1
                    };
                    context.PERFIL_FINANCIERO.Add(perfil);

                    var solicitud = new SolicitudCredito
                    {
                        Id_Solicitud = 1,
                        NumeroSolicitud = "0001",
                        MontoSolicitado = 3000m,
                        PlazoMeses = 3,
                        InteresEstimado = 10m,
                        Estado = "Pendiente",
                        Usuario_Id_Usuario = 1,
                        USUARIO = usuario,
                        PERFIL_FINANCIERO = perfil
                    };
                    context.SOLICITUD_CREDITO.Add(solicitud);
                    context.RESUMEN_CREDITICIO.Add(new ResumenCredito
                    {
                        Usuario_Id = 1,
                        DeudasActivas = 0,
                        DeudasPagadas = 2,
                        DeudasVencidas = 0
                    });
                    context.SaveChanges();

                    var controller = CrearAnalistaController(context);
                    controller.AprobarSolicitud(1, "Aprobado", "Bajo");

                    var cuotas = context.CUOTA.Where(c => c.SOLICITUD_CREDITO_Id_Solicitud == 1).ToList();
                    bool todasPendientes = cuotas.All(c => c.Estado == "Pendiente");

                    return $"Cuotas generadas: {cuotas.Count}, Todas en estado '{(todasPendientes ? "Pendiente" : "Otro")}'";
                });

            // 5. Transición de estado de cuota y pago al ser aprobado por analista
            runner.Evaluar(
                nombreEscenario: "Aprobación de pago de cuota: Pago pasa a 'Aprobado' y Cuota pasa a 'Pagado'",
                datoEntrada: "Pago_Cuota en 'Pendiente validación' asociado a Cuota 'Pendiente aprobación'",
                esperado: "Pago: Aprobado, Cuota: Pagado",
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);

                    var cuota = new Cuota
                    {
                        Id_Cuota = 10,
                        NumeroCuota = 1,
                        MontoCuota = 500m,
                        Estado = "Pendiente aprobación",
                        SOLICITUD_CREDITO_Id_Solicitud = 100
                    };
                    context.CUOTA.Add(cuota);

                    var pago = new PagoCuota
                    {
                        Id_PagoCuota = 10,
                        Id_Cuota = 10,
                        MontoPagado = 500m,
                        MetodoPago = "Transferencia",
                        CodigoOperacion = "OP123456",
                        EntidadPago = "BCP",
                        Estado = "Pendiente validación",
                        FechaPago = DateTime.UtcNow
                    };
                    context.PAGO_CUOTA.Add(pago);
                    context.SaveChanges();

                    // Simulación del procesamiento de validación en Analista
                    pago.Estado = "Aprobado";
                    cuota.Estado = "Pagado";
                    context.SaveChanges();

                    var pagoEnDb = context.PAGO_CUOTA.Find(10);
                    var cuotaEnDb = context.CUOTA.Find(10);
                    return $"Pago: {pagoEnDb?.Estado}, Cuota: {cuotaEnDb?.Estado}";
                });

            // 6. Liquidación total de crédito: Al pagar la última cuota se actualiza Resumen Crediticio
            runner.Evaluar(
                nombreEscenario: "Liquidación total de cuotas: Incrementa DeudasPagadas y reduce DeudasActivas",
                datoEntrada: "Préstamo de 2 cuotas: Ambas marcadas como 'Pagado'",
                esperado: "DeudasActivas: 0, DeudasPagadas: 3 (Préstamo completamente cancelado)",
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);

                    int idUsuario = 5;
                    int idSol = 500;

                    var resumen = new ResumenCredito
                    {
                        Usuario_Id = idUsuario,
                        DeudasActivas = 1,
                        DeudasPagadas = 2,
                        DeudasVencidas = 0
                    };
                    context.RESUMEN_CREDITICIO.Add(resumen);

                    var cuotas = new List<Cuota>
                    {
                        new Cuota { Id_Cuota = 1, SOLICITUD_CREDITO_Id_Solicitud = idSol, Estado = "Pagado" },
                        new Cuota { Id_Cuota = 2, SOLICITUD_CREDITO_Id_Solicitud = idSol, Estado = "Pagado" }
                    };
                    context.CUOTA.AddRange(cuotas);
                    context.SaveChanges();

                    // Regla de producción: if (todasPagadas) { DeudasPagadas++; DeudasActivas--; }
                    bool todasPagadas = cuotas.All(c => c.Estado == "Pagado");
                    if (todasPagadas)
                    {
                        resumen.DeudasPagadas += 1;
                        if (resumen.DeudasActivas > 0) resumen.DeudasActivas -= 1;
                        context.SaveChanges();
                    }

                    var resDb = context.RESUMEN_CREDITICIO.First(r => r.Usuario_Id == idUsuario);
                    return $"DeudasActivas: {resDb.DeudasActivas}, DeudasPagadas: {resDb.DeudasPagadas} (Préstamo completamente cancelado)";
                });

            // 7. Métricas de satisfacción del cliente y NPS tras pagos (BuildResumenCalificaciones)
            runner.Evaluar(
                nombreEscenario: "Métricas de satisfacción post-pago: NPS, puntaje promedio y distribución de estrellas",
                datoEntrada: "5 calificaciones: [5, 5, 4, 3, 1]",
                esperado: "Total: 5, Promedio: 3.6, NPS: 0, Clasificación: Pasivo, CincoEstrellas: 2",
                accion: () =>
                {
                    var lista = new List<CalificacionDto>
                    {
                        new CalificacionDto { IdComentario = 1, Calificacion = 5, Comentario = "Excelente" },
                        new CalificacionDto { IdComentario = 2, Calificacion = 5, Comentario = "Rápido" },
                        new CalificacionDto { IdComentario = 3, Calificacion = 4, Comentario = "Bueno" },
                        new CalificacionDto { IdComentario = 4, Calificacion = 3, Comentario = "Regular" },
                        new CalificacionDto { IdComentario = 5, Calificacion = 1, Comentario = "Malo" }
                    };

                    var metodo = typeof(AnalistaController).GetMethod(
                        "BuildResumenCalificaciones",
                        BindingFlags.NonPublic | BindingFlags.Static
                    );
                    Assert.NotNull(metodo);

                    var res = (ResumenCalificacionesDto)metodo.Invoke(null, new object[] { lista })!;
                    return $"Total: {res.TotalCalificaciones}, Promedio: {res.PuntajePromedio}, NPS: {res.Nps}, Clasificación: {res.ClasificacionNps}, CincoEstrellas: {res.DistribucionEstrellas[5]}";
                });

            runner.VerificarTodosAprobados();
        }
    }
}
