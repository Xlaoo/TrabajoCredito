using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Xunit.Abstractions;
using trabajo.Models;
using trabajo.Models.Patterns.Observer;

namespace TestTrabajoCredito
{
    public class ObserverPatternTests
    {
        private readonly ITestOutputHelper _output;

        public ObserverPatternTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ProbarTodasLasNotificacionesDelPatronObserver()
        {
            var runner = new EscenarioRunner(_output, "Patrón de Diseño Observer (Auditoría e Historial de Estados)");

            // 1. Notificación a múltiples observadores registrados
            runner.Evaluar(
                nombreEscenario: "SolicitudSubject notifica simultáneamente a todos los observadores registrados",
                datoEntrada: "2 observadores suscritos a SolicitudSubject",
                esperado: "Notificados: 2 de 2 observadores",
                accion: () =>
                {
                    var subject = new SolicitudSubject();
                    var mockObs1 = new Mock<ISolicitudObserver>();
                    var mockObs2 = new Mock<ISolicitudObserver>();

                    subject.AgregarObservador(mockObs1.Object);
                    subject.AgregarObservador(mockObs2.Object);

                    var solicitud = new SolicitudCredito
                    {
                        Id_Solicitud = 10,
                        NumeroSolicitud = "0010",
                        Estado = "Aprobado"
                    };
                    string motivo = "Aprobación regular por analista";

                    subject.Notificar(solicitud, motivo);

                    mockObs1.Verify(x => x.Actualizar(solicitud, motivo), Times.Once);
                    mockObs2.Verify(x => x.Actualizar(solicitud, motivo), Times.Once);

                    return "Notificados: 2 de 2 observadores";
                });

            // 2. Propagación exacta de datos de solicitud y motivo del cambio
            runner.Evaluar(
                nombreEscenario: "SolicitudSubject propaga entidad y motivo con fidelidad al observador",
                datoEntrada: "Solicitud 55 ('Rechazado'), Motivo: 'Capacidad de pago insuficiente'",
                esperado: "Entidad recibida: 55 | Motivo recibido: 'Capacidad de pago insuficiente'",
                accion: () =>
                {
                    var subject = new SolicitudSubject();
                    SolicitudCredito? recibida = null;
                    string? motivoRecibido = null;

                    var mockObs = new Mock<ISolicitudObserver>();
                    mockObs.Setup(o => o.Actualizar(It.IsAny<SolicitudCredito>(), It.IsAny<string>()))
                        .Callback<SolicitudCredito, string>((sol, mot) =>
                        {
                            recibida = sol;
                            motivoRecibido = mot;
                        });

                    subject.AgregarObservador(mockObs.Object);

                    var sol = new SolicitudCredito { Id_Solicitud = 55, Estado = "Rechazado" };
                    subject.Notificar(sol, "Capacidad de pago insuficiente");

                    return $"Entidad recibida: {recibida?.Id_Solicitud} | Motivo recibido: '{motivoRecibido}'";
                });

            // 3. Creación inicial de historial en base de datos (HistorialEstadoObserver)
            runner.Evaluar(
                nombreEscenario: "HistorialEstadoObserver crea nuevo registro histórico en base de datos",
                datoEntrada: "Solicitud 101 pasa a 'En Evaluación'",
                esperado: "Historial creado: Estado 'En Evaluación' | Motivo: 'Inicio de revisión por comité'",
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);

                    var observer = new HistorialEstadoObserver(context);
                    var solicitud = new SolicitudCredito
                    {
                        Id_Solicitud = 101,
                        Estado = "En Evaluación"
                    };

                    observer.Actualizar(solicitud, "Inicio de revisión por comité");
                    context.SaveChanges();

                    var historial = context.HISTORIAL_ESTADO.FirstOrDefault(x => x.SOLICITUD_CREDITO_Id_Solicitud == 101);
                    return $"Historial creado: Estado '{historial?.EstadoActual}' | Motivo: '{historial?.MotivoCambio}'";
                });

            // 4. Actualización del registro existente sin duplicar filas (Idempotencia de auditoría)
            runner.Evaluar(
                nombreEscenario: "HistorialEstadoObserver actualiza registro preexistente sin duplicar filas",
                datoEntrada: "Solicitud 202 cambia de 'Pendiente' a 'Aprobado'",
                esperado: "Registros en BD: 1 | Estado: 'Aprobado' | Motivo: 'Aprobado con scoring bajo riesgo'",
                accion: () =>
                {
                    var options = new DbContextOptionsBuilder<UsuarioContext>()
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .Options;
                    using var context = new UsuarioContext(options);

                    var observer = new HistorialEstadoObserver(context);
                    var solicitud = new SolicitudCredito
                    {
                        Id_Solicitud = 202,
                        Estado = "Pendiente"
                    };

                    observer.Actualizar(solicitud, "Solicitud registrada");
                    context.SaveChanges();

                    solicitud.Estado = "Aprobado";
                    observer.Actualizar(solicitud, "Aprobado con scoring bajo riesgo");
                    context.SaveChanges();

                    var registros = context.HISTORIAL_ESTADO
                        .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == 202)
                        .ToList();

                    return $"Registros en BD: {registros.Count} | Estado: '{registros[0].EstadoActual}' | Motivo: '{registros[0].MotivoCambio}'";
                });

            runner.VerificarTodosAprobados();
        }
    }
}
