    using ClosedXML.Excel;
    using DocumentFormat.OpenXml.Bibliography;
    using DocumentFormat.OpenXml.InkML;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using QuestPDF.Fluent;
    using QuestPDF.Helpers;
    using QuestPDF.Infrastructure;
    using System.IO;
using System.Net;
using System.Net.Mail;
    using System.Security.Claims;
    using trabajo.Models;
    using trabajo.Models.ViewModels;
    using trabajo.Service;

namespace trabajo.Controllers
    {
        [Authorize]
        public class AdministradorController : Controller
        {
            private readonly UsuarioContext _context;
            private static readonly Random rnd = new Random();
            private readonly EmailService _emailService = new EmailService();
            private static string codigoPerfilAdmin = "";
            public AdministradorController(UsuarioContext context)
            {
                _context = context;
            }
        public IActionResult Analistas()
        {
            CargarDatosAdministrador();

            var admin = _context.Usuario.FirstOrDefault(x => x.Rol == "Administrador");
            var analista = _context.Usuario.FirstOrDefault(x => x.Rol == "Analista");

            var modelo = new AdminAnalistaViewModel
            {
                IdAdministrador = admin?.Id ?? 0,
                IdAnalista = analista?.Id ?? 0,
                NombreAnalista = analista != null ? $"{analista.Nombre} {analista.Apellido}" : "Sin analista",
                DniAnalista = analista?.Dni ?? "-",
                CelularAnalista = analista?.Celular ?? "-",
                CorreoAnalista = analista?.Correo ?? "-",
                EstadoActivo = analista != null &&
               analista.EstadoActivo &&
               analista.UltimaConexion >= DateTime.Now.AddMinutes(-2),
                FechaAsignacion = analista?.FechaRegistro ?? DateTime.Now,

                SolicitudesAsignadas = analista == null ? 0 :
    _context.SOLICITUD_CREDITO.Count(x => x.FechaSolicitud >= analista.FechaRegistro),

                SolicitudesCompletadas = analista == null ? 0 :
    _context.SOLICITUD_CREDITO.Count(x =>
        x.FechaSolicitud >= analista.FechaRegistro &&
        (x.Estado == "Aprobado" || x.Estado == "Cancelado")),

                SolicitudesEnProceso = analista == null ? 0 :
    _context.SOLICITUD_CREDITO.Count(x =>
        x.FechaSolicitud >= analista.FechaRegistro &&
        x.Estado == "En Evaluación"),

                SolicitudesPendientes = analista == null ? 0 :
    _context.SOLICITUD_CREDITO.Count(x =>
        x.FechaSolicitud >= analista.FechaRegistro &&
        x.Estado == "Pendiente"),

                TasaEfectividad = analista == null ? 0 :
(
    _context.SOLICITUD_CREDITO.Count(x => x.FechaSolicitud >= analista.FechaRegistro) == 0
    ? 0
    : (int)Math.Round(
        (double)_context.SOLICITUD_CREDITO.Count(x =>
            x.FechaSolicitud >= analista.FechaRegistro &&
            (x.Estado == "Aprobado" || x.Estado == "Cancelado")) * 100 /
        _context.SOLICITUD_CREDITO.Count(x =>
            x.FechaSolicitud >= analista.FechaRegistro)
    )
),

                Actividades = new List<AdminAnalistaActividadViewModel>
{
    new AdminAnalistaActividadViewModel
    {
        Titulo = "Panel de analista activo",
        Descripcion = analista != null
            ? $"El analista {analista.Nombre} {analista.Apellido} está asignado al sistema."
            : "No existe analista asignado.",
        Fecha = DateTime.Now,
        Icono = "fas fa-user-check"
    }
},

                Mensajes = _context.MENSAJE_ADMIN_ANALISTA
    .OrderBy(x => x.FechaEnvio)
    .Select(x => new AdminAnalistaMensajeViewModel
    {
        IdMensaje = x.IdMensaje,
        IdAdministrador = x.IdAdministrador,
        IdAnalista = x.IdAnalista,
        RemitenteRol = x.RemitenteRol,
        Mensaje = x.Mensaje,
        FechaEnvio = x.FechaEnvio,
        Leido = x.Leido
    })
    .ToList()
            };
            ViewBag.ClientesDisponibles = _context.Usuario
                .Where(x => x.Rol == "Cliente")
                .OrderBy(x => x.Nombre)
                .Select(x => new
                {
                    x.Id,
                    x.Nombre,
                    x.Apellido,
                    x.Dni,
                    TieneSolicitudActiva = _context.SOLICITUD_CREDITO.Any(s =>
                        s.Usuario_Id_Usuario == x.Id &&
                        (s.Estado == "Pendiente" ||
                         s.Estado == "En Evaluación" ||
                         s.Estado == "Aprobado" ||
                         s.Estado == "Rechazado"))
                })
                .ToList();

            ViewBag.SolicitudSoporte = _context.SOLICITUD_SOPORTE
                .Where(x => x.Estado == "Pendiente" && x.Leido == false)
                .OrderByDescending(x => x.FechaEnvio)
                .FirstOrDefault();

            return View(modelo);
        }
        [HttpPost]
        public IActionResult ConfirmarSolicitudSoporte(int id)
        {
            var solicitud = _context.SOLICITUD_SOPORTE
                .FirstOrDefault(x => x.Id_Soporte == id);

            if (solicitud != null)
            {
                solicitud.Estado = "Vista";
                solicitud.Leido = true;
                _context.SaveChanges();
            }

            return RedirectToAction("Analistas");
        }
        private void LimpiarSolicitudesDelAnalistaAnterior(int idAnalista)
        {
            var solicitudes = _context.SOLICITUD_CREDITO
                .Where(x => x.Usuario_Id_Usuario == idAnalista)
                .ToList();

            foreach (var solicitud in solicitudes)
            {
                solicitud.Estado = "ArchivadoPorCambioAnalista";
            }

            var usuario = _context.Usuario.FirstOrDefault(x => x.Id == idAnalista);

            if (usuario != null)
            {
                usuario.EstadoActivo = false;
                usuario.UltimaConexion = null;
            }
        }
        [HttpPost]
        public IActionResult ReemplazarAnalistaExistente(int idCliente)
        {
            var analistaActual = _context.Usuario.FirstOrDefault(x => x.Rol == "Analista");
            var nuevoAnalista = _context.Usuario.FirstOrDefault(x => x.Id == idCliente && x.Rol == "Cliente");

            if (nuevoAnalista == null)
            {
                TempData["ErrorAnalista"] = "Seleccione un cliente válido.";
                return RedirectToAction("Analistas");
            }
            bool tieneSolicitudActiva = _context.SOLICITUD_CREDITO.Any(s =>
    s.Usuario_Id_Usuario == nuevoAnalista.Id &&
    (s.Estado == "Pendiente" ||
     s.Estado == "En Evaluación" ||
     s.Estado == "Aprobado" ||
     s.Estado == "Rechazado"));

            if (tieneSolicitudActiva)
            {
                TempData["ErrorAnalista"] = "No puede cambiar este usuario a analista porque tiene una solicitud activa. Escoge otro usuario sin solicitud activa.";
                return RedirectToAction("Analistas");
            }

            if (analistaActual != null)
            {
                analistaActual.Rol = "Cliente";
                analistaActual.EstadoActivo = false;
                LimpiarSolicitudesDelAnalistaAnterior(analistaActual.Id);
                var soportesAnterior = _context.SOLICITUD_SOPORTE
    .Where(x => x.Id_Analista == analistaActual.Id);

                _context.SOLICITUD_SOPORTE.RemoveRange(soportesAnterior);
            }

            nuevoAnalista.Rol = "Analista";
            nuevoAnalista.EstadoActivo = false;
            nuevoAnalista.FechaRegistro = DateTime.Now;
            nuevoAnalista.UltimaConexion = null;

            _context.MENSAJE_ADMIN_ANALISTA.RemoveRange(_context.MENSAJE_ADMIN_ANALISTA);
            _context.SaveChanges();

            TempData["OkAnalista"] = "Analista reemplazado correctamente.";
            return RedirectToAction("Analistas");
        }

        [HttpPost]
        public IActionResult CrearReemplazarAnalista(
    string nombre,
    string apellido,
    string dni,
    string celular,
    string correo,
    string genero,
    string clave)
        {
            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(apellido) ||
                string.IsNullOrWhiteSpace(dni) || string.IsNullOrWhiteSpace(celular) ||
                string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(genero))
            {
                TempData["ErrorAnalista"] = "Complete todos los campos.";
                return RedirectToAction("Analistas");
            }

            var analistaActual = _context.Usuario.FirstOrDefault(x => x.Rol == "Analista");

            if (analistaActual != null)
            {
                analistaActual.Rol = "Cliente";
                analistaActual.EstadoActivo = false;
                LimpiarSolicitudesDelAnalistaAnterior(analistaActual.Id);
                var soportesAnterior = _context.SOLICITUD_SOPORTE
    .Where(x => x.Id_Analista == analistaActual.Id);

                _context.SOLICITUD_SOPORTE.RemoveRange(soportesAnterior);
            }

            var nuevo = new Usuario
            {
                Nombre = nombre,
                Apellido = apellido,
                Dni = dni,
                Celular = celular,
                Correo = correo,
                Genero = genero,
                Rol = "Analista",
                EstadoActivo = false,
                FechaRegistro = DateTime.Now,
                UltimaConexion = null,
                clave = utilidades.EncriptarClave(clave)
            };

            _context.Usuario.Add(nuevo);
            _context.MENSAJE_ADMIN_ANALISTA.RemoveRange(_context.MENSAJE_ADMIN_ANALISTA);
            _context.SaveChanges();

            TempData["OkAnalista"] = "Nuevo analista creado correctamente.";
            return RedirectToAction("Analistas");
        }

        public IActionResult ProgramaAdministrador()
            {
                CargarDatosAdministrador();
                DateTime primerDiaDelMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime primerDiaSiguienteMes = primerDiaDelMes.AddMonths(1);
                DateTime hoy = DateTime.Today;

                var solicitudes = _context.SOLICITUD_CREDITO
                    .Include(x => x.USUARIO)
                    .Include(x => x.HISTORIAL_ESTADOS)
                    .Where(x => x.FechaSolicitud >= primerDiaDelMes &&
                                x.FechaSolicitud < primerDiaSiguienteMes)
                    .ToList();

                int totalClientes = _context.Usuario.Count(x => x.Rol == "Cliente");

                int clientesEstesMes = _context.Usuario.Count(x =>
                    x.Rol == "Cliente" &&
                    x.FechaRegistro >= primerDiaDelMes &&
                    x.FechaRegistro < primerDiaSiguienteMes);

                var creditosAprobados = solicitudes
                    .Where(x => x.Estado == "Aprobado")
                    .ToList();

                int totalCreditosAprobados = creditosAprobados.Count;
                decimal montoOtorgado = creditosAprobados.Sum(x => x.MontoSolicitado);

                var creditosEnEvaluacion = solicitudes
        .Where(x => x.Estado == "Pendiente" || x.Estado == "En Evaluación")
        .ToList();

                int totalEnEvaluacion = creditosEnEvaluacion.Count;
                decimal montoEnEvaluacion = creditosEnEvaluacion.Sum(x => x.MontoSolicitado);

                var cuotasActivas = _context.CUOTA
         .Where(c => c.SOLICITUD_CREDITO_Id_Solicitud != null)
         .ToList();

            int pagosCompletadosMes = (
 from pago in _context.PAGO_CUOTA
 join cuota in _context.CUOTA on pago.Id_Cuota equals cuota.Id_Cuota
 join solicitud in _context.SOLICITUD_CREDITO
     on cuota.SOLICITUD_CREDITO_Id_Solicitud equals solicitud.Id_Solicitud
 where solicitud.Estado == "Aprobado"
       && pago.Estado == "Aprobado"
       && pago.FechaPago >= primerDiaDelMes
       && pago.FechaPago < primerDiaSiguienteMes
 select pago
).Count();

            int pagosPendientesMes = (
                from cuota in _context.CUOTA
                join solicitud in _context.SOLICITUD_CREDITO
                    on cuota.SOLICITUD_CREDITO_Id_Solicitud equals solicitud.Id_Solicitud
                where solicitud.Estado == "Aprobado"
                      && cuota.Estado == "Pendiente"
                      && cuota.FechaLimitePago >= primerDiaDelMes
                      && cuota.FechaLimitePago < primerDiaSiguienteMes
                      && cuota.FechaLimitePago.Date >= hoy
                select cuota
            ).Count();

            int pagosMoraMes = (
                from cuota in _context.CUOTA
                join solicitud in _context.SOLICITUD_CREDITO
                    on cuota.SOLICITUD_CREDITO_Id_Solicitud equals solicitud.Id_Solicitud
                where solicitud.Estado == "Aprobado"
                      && cuota.Estado == "Pendiente"
                      && cuota.FechaLimitePago >= primerDiaDelMes
                      && cuota.FechaLimitePago < primerDiaSiguienteMes
                      && cuota.FechaLimitePago.Date < hoy
                select cuota
            ).Count();

            int totalCuotasEnMora = (
       from cuota in _context.CUOTA
       join solicitud in _context.SOLICITUD_CREDITO
           on cuota.SOLICITUD_CREDITO_Id_Solicitud equals solicitud.Id_Solicitud
       where solicitud.Estado == "Aprobado"
             && cuota.Estado == "Pendiente"
             && cuota.FechaLimitePago.Date < hoy
       select cuota
   ).Count();

            decimal montoEnMora = (
                from cuota in _context.CUOTA
                join solicitud in _context.SOLICITUD_CREDITO
                    on cuota.SOLICITUD_CREDITO_Id_Solicitud equals solicitud.Id_Solicitud
                where solicitud.Estado == "Aprobado"
                      && cuota.Estado == "Pendiente"
                      && cuota.FechaLimitePago.Date < hoy
                select cuota.MontoCuota ?? 0
            ).Sum();
            int totalPagosMes = pagosCompletadosMes + pagosPendientesMes + pagosMoraMes;

                int porcentajePagosCompletados = totalPagosMes > 0
                    ? (int)Math.Round((double)pagosCompletadosMes * 100 / totalPagosMes)
                    : 0;

                int porcentajePagosPendientes = totalPagosMes > 0
                    ? (int)Math.Round((double)pagosPendientesMes * 100 / totalPagosMes)
                    : 0;

                int porcentajePagosMora = totalPagosMes > 0
                    ? (int)Math.Round((double)pagosMoraMes * 100 / totalPagosMes)
                    : 0;

                int creditosRechazados = solicitudes.Count(x => x.Estado == "Rechazado");

                int creditosDesembolsados = solicitudes.Count(x =>
                    x.Estado == "Cancelado");

                int totalResumenCreditos =
                    totalCreditosAprobados +
                    totalEnEvaluacion +
                    creditosDesembolsados +
                    creditosRechazados;

                var actividades = new List<ActividadRecienteAdminViewModel>();

                var ultimosClientes = _context.Usuario
                    .Where(x => x.Rol == "Cliente" &&
                                x.FechaRegistro >= primerDiaDelMes &&
                                x.FechaRegistro < primerDiaSiguienteMes)
                    .OrderByDescending(x => x.FechaRegistro)
                    .Take(2)
                    .ToList();

                foreach (var cliente in ultimosClientes)
                {
                    actividades.Add(new ActividadRecienteAdminViewModel
                    {
                        Tipo = "Cliente",
                        Titulo = "Nuevo cliente registrado",
                        Descripcion = $"{cliente.Nombre} {cliente.Apellido} se registró en el sistema",
                        Fecha = cliente.FechaRegistro,
                        Icono = "fas fa-user",
                        Color = "purple"
                    });
                }

                foreach (var solicitud in solicitudes)
                {
                    actividades.Add(new ActividadRecienteAdminViewModel
                    {
                        Tipo = "Solicitud",
                        Titulo = "Solicitud registrada",
                        Descripcion = $"Solicitud {solicitud.NumeroSolicitud} - {solicitud.USUARIO?.Nombre} {solicitud.USUARIO?.Apellido}",
                        Fecha = solicitud.FechaSolicitud,
                        Icono = "fas fa-file-alt",
                        Color = "orange"
                    });

                    if (solicitud.HISTORIAL_ESTADOS != null)
                    {
                        foreach (var historial in solicitud.HISTORIAL_ESTADOS)
                        {
                            actividades.Add(new ActividadRecienteAdminViewModel
                            {
                                Tipo = "Estado",
                                Titulo = $"Crédito {historial.EstadoActual}",
                                Descripcion = $"Solicitud {solicitud.NumeroSolicitud} - {solicitud.USUARIO?.Nombre} {solicitud.USUARIO?.Apellido}",
                                Fecha = historial.FechaCambio,
                                Icono = historial.EstadoActual == "Aprobado" ? "fas fa-check" :
                                        historial.EstadoActual == "Rechazado" ? "fas fa-times" :
                                        historial.EstadoActual == "Cancelado" ? "fas fa-ban" :
                                        "fas fa-clock",
                                Color = historial.EstadoActual == "Aprobado" ? "green" :
                                        historial.EstadoActual == "Rechazado" ? "red" :
                                        historial.EstadoActual == "Cancelado" ? "red" :
                                        "blue"
                            });
                        }
                    }
                }

                actividades = actividades
                    .OrderByDescending(x => x.Fecha)
                    .ToList();
                SincronizarReportesPendientes();

                var modelo = new AdminDashboardViewModel
                {
                    TotalClientes = totalClientes,
                    ClientesEstesMes = clientesEstesMes,

                    TotalCreditosAprobados = totalCreditosAprobados,
                    MontoOtorgado = montoOtorgado,

                    TotalEnEvaluacion = totalEnEvaluacion,
                    MontoEnEvaluacion = montoEnEvaluacion,

                    TotalCuotasEnMora = totalCuotasEnMora,
                    MontoEnMora = montoEnMora,

                    TotalReportesGenerados = _context.REPORTE_GENERADO.Count(x =>
                        x.Estado == "Completado" &&
                        x.FechaInicioReporte >= primerDiaDelMes &&
                        x.FechaInicioReporte < primerDiaSiguienteMes),

                    CreditosAprobados = totalCreditosAprobados,
                    CreditosEnEvaluacion = totalEnEvaluacion,
                    CreditosDesembolsados = creditosDesembolsados,
                    CreditosRechazados = creditosRechazados,
                    TotalResumenCreditos = totalResumenCreditos,

                    PagosCompletadosMes = pagosCompletadosMes,
                    PagosPendientesMes = pagosPendientesMes,
                    PagosMoraMes = pagosMoraMes,
                    PorcentajePagosCompletados = porcentajePagosCompletados,
                    PorcentajePagosPendientes = porcentajePagosPendientes,
                    PorcentajePagosMora = porcentajePagosMora,
                    ActividadesRecientes = actividades,

                    UsuariosSistemaActivos = _context.Usuario.Count(x =>
        x.Rol == "Cliente" &&
        x.EstadoActivo == true &&
        x.UltimaConexion >= DateTime.Now.AddMinutes(-2)),
                    EstadoBaseDatos = "Óptimo",
                    VersionSistema = "v2.1.0",
                    PorcentajeAlmacenamiento = 65,
                    UltimoRespaldo = DateTime.Now
                };
            
                int totalClientesActual = _context.Usuario.Count(x => x.Rol == "Cliente");
                int totalCreditosActual = _context.SOLICITUD_CREDITO.Count();
                int totalPagosActual = _context.PAGO_CUOTA.Count();

                var repClientes = _context.REPORTE_GENERADO
         .Where(x => x.TipoReporte == "Clientes" && x.Formato == "Excel")
         .OrderByDescending(x => x.Id)
         .FirstOrDefault();

                var repCreditos = _context.REPORTE_GENERADO
                    .Where(x => x.TipoReporte == "Creditos" && x.Formato == "Excel")
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefault();

                var repPagos = _context.REPORTE_GENERADO
                    .Where(x => x.TipoReporte == "Pagos" && x.Formato == "Excel")
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefault();

                DateTime ultimaFechaCliente = _context.Usuario
                    .Where(x => x.Rol == "Cliente")
                    .Max(x => x.FechaRegistro);

                ViewBag.ClientesPendiente = repClientes == null
                    || repClientes.Estado == "En Proceso"
                    || ultimaFechaCliente > repClientes.FechaGeneracion;

                DateTime ultimaFechaCredito = _context.SOLICITUD_CREDITO.Any()
                    ? _context.SOLICITUD_CREDITO.Max(x => x.FechaSolicitud)
                    : DateTime.MinValue;

                DateTime ultimaFechaPago = _context.PAGO_CUOTA.Any()
                    ? _context.PAGO_CUOTA.Max(x => x.FechaPago)
                    : DateTime.MinValue;

                ViewBag.CreditosPendiente = repCreditos == null
                    || repCreditos.Estado == "En Proceso"
                    || ultimaFechaCredito > repCreditos.FechaGeneracion;

                ViewBag.PagosPendiente = repPagos == null
                    || repPagos.Estado == "En Proceso"
                    || ultimaFechaPago > repPagos.FechaGeneracion;
                var correoAdmin =
                    User.FindFirst(ClaimTypes.Email)?.Value ??
                    User.FindFirst("Correo")?.Value ??
                    User.FindFirst("correo")?.Value ??
                    User.Identity?.Name;

                var adminActual = _context.Usuario
                    .FirstOrDefault(x => x.Correo == correoAdmin && x.Rol == "Administrador");

                ViewBag.MostrarRecordatorios = adminActual != null && adminActual.NotificacionRecordatorio;
                ViewBag.RecordatoriosAdmin = new List<string>();

                if (ViewBag.MostrarRecordatorios == true)
                {
                    if (totalEnEvaluacion > 0)
                        ViewBag.RecordatoriosAdmin.Add($"Tienes {totalEnEvaluacion} solicitudes pendientes o en evaluación.");

                    if (totalCuotasEnMora > 0)
                        ViewBag.RecordatoriosAdmin.Add($"Tienes {totalCuotasEnMora} cuotas en mora por revisar.");

                    if (ViewBag.ClientesPendiente == true)
                        ViewBag.RecordatoriosAdmin.Add("El reporte de clientes está pendiente de actualizar.");

                    if (ViewBag.CreditosPendiente == true)
                        ViewBag.RecordatoriosAdmin.Add("El reporte de créditos está pendiente de actualizar.");

                    if (ViewBag.PagosPendiente == true)
                        ViewBag.RecordatoriosAdmin.Add("El reporte de pagos está pendiente de actualizar.");
                }
                ViewBag.MostrarNotificacionesSistema = adminActual != null && adminActual.NotificacionSistema;
                ViewBag.NotificacionesSistemaAdmin = new List<string>();

                if (ViewBag.MostrarNotificacionesSistema == true)
                {
                    if (totalEnEvaluacion > 0)
                        ViewBag.NotificacionesSistemaAdmin.Add($"Hay {totalEnEvaluacion} créditos pendientes de revisión.");

                    if (totalCuotasEnMora > 0)
                        ViewBag.NotificacionesSistemaAdmin.Add($"Hay {totalCuotasEnMora} cuotas vencidas en mora.");

                    if (clientesEstesMes > 0)
                        ViewBag.NotificacionesSistemaAdmin.Add($"Se registraron {clientesEstesMes} clientes este mes.");
                }
                if (adminActual != null && adminActual.NotificacionCorreo)
                {
                    var alertasCorreo = new List<string>();

                    if (totalEnEvaluacion > 0)
                        alertasCorreo.Add($"Tienes {totalEnEvaluacion} créditos pendientes o en evaluación.");

                    if (totalCuotasEnMora > 0)
                        alertasCorreo.Add($"Tienes {totalCuotasEnMora} cuotas en mora por revisar.");

                    if (alertasCorreo.Any())
                    {
                        ViewBag.NotificacionCorreoActiva = true;
                        ViewBag.AlertasCorreoAdmin = alertasCorreo;
                    }
                }
                ViewBag.HayReportesRapidos =
        ViewBag.ClientesPendiente == true ||
        ViewBag.CreditosPendiente == true ||
        ViewBag.PagosPendiente == true;
                return View(modelo);

            }
            private IActionResult GenerarClientesExcel()
            {
                var clientes = _context.Usuario
                    .Where(x => x.Rol == "Cliente")
                    .OrderByDescending(x => x.FechaRegistro)
                    .ToList();

                using var workbook = new XLWorkbook();
                var hoja = workbook.Worksheets.Add("Clientes");

                hoja.Cell(1, 1).Value = "ID";
                hoja.Cell(1, 2).Value = "Nombre";
                hoja.Cell(1, 3).Value = "Apellido";
                hoja.Cell(1, 4).Value = "DNI";
                hoja.Cell(1, 5).Value = "Celular";
                hoja.Cell(1, 6).Value = "Correo";
                hoja.Cell(1, 7).Value = "Género";
                hoja.Cell(1, 8).Value = "Contraseña";
                hoja.Cell(1, 9).Value = "Fecha Registro";

                int filaDesempeno = 2;

                foreach (var c in clientes)
                {
                    hoja.Cell(filaDesempeno, 1).Value = c.Id;
                    hoja.Cell(filaDesempeno, 2).Value = c.Nombre;
                    hoja.Cell(filaDesempeno, 3).Value = c.Apellido;
                    hoja.Cell(filaDesempeno, 4).Value = c.Dni;
                    hoja.Cell(filaDesempeno, 5).Value = c.Celular;
                    hoja.Cell(filaDesempeno, 6).Value = c.Correo;
                    hoja.Cell(filaDesempeno, 7).Value = c.Genero;
                    hoja.Cell(filaDesempeno, 8).Value = "Protegida";
                    hoja.Cell(filaDesempeno, 9).Value = c.FechaRegistro.ToString("dd/MM/yyyy hh:mm tt");
                    filaDesempeno++;
                }

                hoja.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                return File(stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Reporte_Clientes.xlsx");
            }

            private IActionResult GenerarCreditosExcel()
            {
                var creditos = _context.SOLICITUD_CREDITO
                    .Include(x => x.USUARIO)
                    .OrderByDescending(x => x.FechaSolicitud)
                    .ToList();

                using var workbook = new XLWorkbook();
                var hoja = workbook.Worksheets.Add("Creditos");

                hoja.Cell(1, 1).Value = "Solicitud";
                hoja.Cell(1, 2).Value = "Cliente";
                hoja.Cell(1, 3).Value = "DNI";
                hoja.Cell(1, 4).Value = "Monto";
                hoja.Cell(1, 5).Value = "Plazo Meses";
                hoja.Cell(1, 6).Value = "Interés";
                hoja.Cell(1, 7).Value = "Estado";
                hoja.Cell(1, 8).Value = "Fecha Solicitud";

                int fila = 2;

                foreach (var c in creditos)
                {
                    hoja.Cell(fila, 1).Value = c.NumeroSolicitud;
                    hoja.Cell(fila, 2).Value = $"{c.USUARIO?.Nombre} {c.USUARIO?.Apellido}";
                    hoja.Cell(fila, 3).Value = c.USUARIO?.Dni;
                    hoja.Cell(fila, 4).Value = c.MontoSolicitado;
                    hoja.Cell(fila, 5).Value = c.PlazoMeses;
                    hoja.Cell(fila, 6).Value = c.InteresEstimado;
                    hoja.Cell(fila, 7).Value = c.Estado;
                    hoja.Cell(fila, 8).Value = c.FechaSolicitud.ToString("dd/MM/yyyy hh:mm tt");
                    fila++;
                }

                hoja.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Reporte_Creditos.xlsx"
                );
            }

            private IActionResult GenerarPagosExcel()
            {
          
                var solicitudes = _context.SOLICITUD_CREDITO
                    .Include(x => x.USUARIO)
                    .Where(x => x.Estado == "Aprobado")
                    .OrderByDescending(x => x.FechaSolicitud)
                    .ToList();

                var cuotas = _context.CUOTA.ToList();
                var pagos = _context.PAGO_CUOTA.ToList();

                using var workbook = new XLWorkbook();
                var hoja = workbook.Worksheets.Add("Pagos");

                hoja.Cell(1, 1).Value = "Solicitud";
                hoja.Cell(1, 2).Value = "Cliente";
                hoja.Cell(1, 3).Value = "DNI";
                hoja.Cell(1, 4).Value = "Cuotas Pagadas";
                hoja.Cell(1, 5).Value = "Cuotas Pendientes";
                hoja.Cell(1, 6).Value = "Cuotas en Mora";
                hoja.Cell(1, 7).Value = "Monto Pendiente";
                hoja.Cell(1, 8).Value = "Estado Pago";

                int fila = 2;
                DateTime hoy = DateTime.Today;

                foreach (var s in solicitudes)
                {
                    var cuotasSolicitud = cuotas
                        .Where(c => c.SOLICITUD_CREDITO_Id_Solicitud == s.Id_Solicitud)
                        .ToList();

                    int pagadas = pagos
                        .Where(p => p.Estado == "Aprobado")
                        .Join(cuotasSolicitud,
                            p => p.Id_Cuota,
                            c => c.Id_Cuota,
                            (p, c) => c.Id_Cuota)
                        .Distinct()
                        .Count();

                    int pendientes = cuotasSolicitud.Count(c => c.Estado == "Pendiente");

                    int mora = cuotasSolicitud.Count(c =>
                        c.Estado == "Pendiente" &&
                        c.FechaLimitePago.Date < hoy);

                    decimal montoPendiente = cuotasSolicitud
                        .Where(c => c.Estado == "Pendiente")
                        .Sum(c => c.MontoCuota ?? 0);

                    string estadoPago = pagadas > 0 ? "Con pagos registrados" : "Pendiente";

                    hoja.Cell(fila, 1).Value = s.NumeroSolicitud;
                    hoja.Cell(fila, 2).Value = $"{s.USUARIO?.Nombre} {s.USUARIO?.Apellido}";
                    hoja.Cell(fila, 3).Value = s.USUARIO?.Dni;
                    hoja.Cell(fila, 4).Value = pagadas;
                    hoja.Cell(fila, 5).Value = pendientes;
                    hoja.Cell(fila, 6).Value = mora;
                    hoja.Cell(fila, 7).Value = montoPendiente;
                    hoja.Cell(fila, 8).Value = estadoPago;

                    fila++;
                }

                hoja.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                return File(stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Reporte_Pagos.xlsx");
            }
            [HttpPost]
            public IActionResult GenerarReporte(
        string TipoReporte,
        DateTime FechaInicio,
        DateTime FechaFin,
        string Estado,
        string TipoCredito,
        string Formato)
            {
                DateTime desde = new DateTime(FechaInicio.Year, FechaInicio.Month, 1);

                DateTime hasta = new DateTime(
                    FechaFin.Year,
                    FechaFin.Month,
                    DateTime.DaysInMonth(FechaFin.Year, FechaFin.Month)
                );

                if (hasta < desde)
                {
                    TempData["ErrorReporte"] = "La fecha fin no puede ser menor que la fecha inicio.";
                    return RedirectToAction("GenerarReportes");
                }

                int mesReporte = desde.Month;
                int anioReporte = desde.Year;

                bool yaExiste = _context.REPORTE_GENERADO.Any(x =>
                    x.TipoReporte == TipoReporte &&
                    x.Formato == Formato &&
                    x.FechaInicioReporte.Date == desde.Date &&
                    x.FechaFinReporte.Date == hasta.Date);

                if (yaExiste)
                {
                    TempData["ErrorReporte"] =
                        $"Ya existe un reporte de {TipoReporte} en formato {Formato} para ese mes. Primero elimínalo para generar otro.";

                    return RedirectToAction("GenerarReportes");
                }

                var nuevoReporte = new ReporteGenerado
                {
                    TipoReporte = TipoReporte,
                    Mes = mesReporte,
                    Anio = anioReporte,
                    FechaGeneracion = DateTime.Now,
                    FechaInicioReporte = desde,
                    FechaFinReporte = hasta,
                    EstadoFiltro = string.IsNullOrWhiteSpace(Estado) ? "Todos" : Estado,
                    TipoCreditoFiltro = string.IsNullOrWhiteSpace(TipoCredito) ? "Todos" : TipoCredito,
                    Descripcion = $"Reporte de {TipoReporte}",
                    Formato = Formato,
                    SolicitadoPor = "Administrador",
                    FechaSolicitud = DateTime.Now,

                    Estado = "En Proceso",
                    Descargado = false,
                    CantidadDatos = ObtenerCantidadDatosReporte(TipoReporte, desde, hasta, Estado, TipoCredito)
                };

                _context.REPORTE_GENERADO.Add(nuevoReporte);
                _context.SaveChanges();

                TempData["OkReporte"] =
                    $"Reporte de {TipoReporte} en formato {Formato} generado correctamente.";

                return RedirectToAction("Exportaciones");
            }
            public IActionResult Exportaciones(
        DateTime? fechaInicio,
        DateTime? fechaFin,
        string tipoExportacion = "Todos",
        string estado = "Todos",
        string formato = "Todos")
            {
                CargarDatosAdministrador();
                ViewData["Title"] = "Exportaciones";
                ViewData["ActivePage"] = "Exportaciones";

                SincronizarReportesPendientes();

                DateTime inicio = fechaInicio ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime fin = fechaFin ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month));

                var query = _context.REPORTE_GENERADO.AsQueryable();

                int inicioPeriodo = inicio.Year * 100 + inicio.Month;
                int finPeriodo = fin.Year * 100 + fin.Month;

                query = query.Where(x =>
                    (x.Anio * 100 + x.Mes) >= inicioPeriodo &&
                    (x.Anio * 100 + x.Mes) <= finPeriodo);

                if (!string.IsNullOrEmpty(tipoExportacion) && tipoExportacion != "Todos")
                    query = query.Where(x => x.TipoReporte == tipoExportacion);

                if (!string.IsNullOrEmpty(estado) && estado != "Todos")
                    query = query.Where(x => x.Estado == estado);
                if (!string.IsNullOrEmpty(formato) && formato != "Todos")
                    query = query.Where(x => x.Formato == formato);

                var reportes = query
                    .OrderByDescending(r => r.FechaSolicitud)
                    .ToList();

                ViewBag.FechaInicio = inicio.ToString("yyyy-MM-dd");
                ViewBag.FechaFin = fin.ToString("yyyy-MM-dd");
                ViewBag.TipoExportacion = tipoExportacion ?? "Todos";
                ViewBag.Estado = estado ?? "Todos";
                ViewBag.Formato = formato ?? "Todos";

                return View(reportes);
            }
            public async Task<IActionResult> DescargarExportacion(int id)
            {
                var reporte = _context.REPORTE_GENERADO.FirstOrDefault(x => x.Id == id);

                if (reporte == null)
                    return RedirectToAction("Exportaciones");

                DateTime desde = reporte.FechaInicioReporte != DateTime.MinValue
                    ? reporte.FechaInicioReporte
                    : new DateTime(reporte.Anio, reporte.Mes, 1);

                DateTime hasta = reporte.FechaFinReporte != DateTime.MinValue
                    ? reporte.FechaFinReporte
                    : new DateTime(reporte.Anio, reporte.Mes, DateTime.DaysInMonth(reporte.Anio, reporte.Mes));
                string estadoFiltro = string.IsNullOrWhiteSpace(reporte.EstadoFiltro)
        ? "Todos"
        : reporte.EstadoFiltro;

                string tipoCreditoFiltro = string.IsNullOrWhiteSpace(reporte.TipoCreditoFiltro)
                    ? "Todos"
                    : reporte.TipoCreditoFiltro;

                reporte.Estado = "Completado";
                reporte.Descargado = true;
                reporte.FechaGeneracion = DateTime.Now;
                reporte.FechaSolicitud = DateTime.Now;
                reporte.CantidadDatos = ObtenerCantidadDatosReporte(
        reporte.TipoReporte,
        desde,
        hasta,
        estadoFiltro,
        tipoCreditoFiltro
    );

                _context.SaveChanges();

                if (reporte.Formato == "Excel")
                {
                    switch (reporte.TipoReporte)
                    {
                        case "Clientes":
                            return await ExportarClientesExcel("", estadoFiltro, desde, hasta);

                        case "Creditos":
                            return ExportarCreditosExcel("", estadoFiltro, desde, hasta);

                        case "Pagos":
                            return PagosExcel("", estadoFiltro, "Todos", desde, hasta);

                        case "Desempeno":
                            return ReporteAdministradorExcel(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Desempeno");

                        case "Mora":
                            return ReporteAdministradorExcel(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Mora");

                        case "Financiero":
                            return ReporteAdministradorExcel(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Financiero");

                        case "Sucursal":
                            return ReporteAdministradorExcel(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Sucursal");

                        case "TipoCredito":
                            return ReporteAdministradorExcel(desde, hasta, tipoCreditoFiltro, estadoFiltro, "TipoCredito");

                        case "Periodo":
                            return ReporteAdministradorExcel(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Periodo");

                        case "Actividad":
                            return ReporteAdministradorExcel(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Actividad");

                        case "Exportaciones":
                            return ReporteAdministradorExcel(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Exportaciones");

                        case "Personalizado":
                            return ReporteAdministradorExcel(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Personalizado");

                        default:
                            return ReporteAdministradorExcel(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Administrador");
                    }
                }

                if (reporte.Formato == "PDF")
                {
                    switch (reporte.TipoReporte)
                    {
                        case "Clientes":
                            return await ExportarClientesPdf("", estadoFiltro, desde, hasta);

                        case "Creditos":
                            return ExportarCreditosPdf("", estadoFiltro, desde, hasta);

                        case "Pagos":
                            return PagosPdf("", estadoFiltro, "Todos", desde, hasta);

                        case "Desempeno":
                            return ReporteAdministradorPdf(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Desempeno");

                        case "Mora":
                            return ReporteAdministradorPdf(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Mora");

                        case "Financiero":
                            return ReporteAdministradorPdf(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Financiero");

                        case "Sucursal":
                            return ReporteAdministradorPdf(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Sucursal");

                        case "TipoCredito":
                            return ReporteAdministradorPdf(desde, hasta, tipoCreditoFiltro, estadoFiltro, "TipoCredito");

                        case "Periodo":
                            return ReporteAdministradorPdf(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Periodo");

                        case "Actividad":
                            return ReporteAdministradorPdf(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Actividad");

                        case "Exportaciones":
                            return ReporteAdministradorPdf(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Exportaciones");

                        case "Personalizado":
                            return ReporteAdministradorPdf(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Personalizado");

                        default:
                            return ReporteAdministradorPdf(desde, hasta, tipoCreditoFiltro, estadoFiltro, "Administrador");
                    }
                }

                return RedirectToAction("Exportaciones");
            }

            public IActionResult EliminarExportacion(int id)
            {
                var reporte = _context.REPORTE_GENERADO
                    .FirstOrDefault(x => x.Id == id);

                if (reporte != null)
                {
                    _context.REPORTE_GENERADO.Remove(reporte);
                    _context.SaveChanges();
                }

                SincronizarReportesPendientes();

                return RedirectToAction("Exportaciones");
            }
            private int ObtenerCantidadDatosReporte(
        string tipoReporte,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        string estado = "Todos",
        string tipoCredito = "Todos")
            {
                DateTime desde = fechaInicio ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime hasta = fechaFin ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month));

                if (tipoReporte == "Clientes")
                {
                    return _context.Usuario.Count(x =>
                        x.Rol == "Cliente" &&
                        x.FechaRegistro.Date >= desde.Date &&
                        x.FechaRegistro.Date <= hasta.Date);
                }

                if (tipoReporte == "Pagos")
                {
                    return _context.PAGO_CUOTA.Count(x =>
                        x.FechaPago.Date >= desde.Date &&
                        x.FechaPago.Date <= hasta.Date &&
                        (estado == "Todos" || x.Estado == estado));
                }

                if (tipoReporte == "Mora")
                {
                    return _context.CUOTA.Count(x =>
                        x.Estado == "Pendiente" &&
                        x.FechaLimitePago.Date >= desde.Date &&
                        x.FechaLimitePago.Date <= hasta.Date &&
                        x.FechaLimitePago.Date < DateTime.Today);
                }

                if (tipoReporte == "Actividad")
                {
                    return _context.ACTIVIDAD_ADMINISTRADOR.Count(x =>
                        x.Fecha.Date >= desde.Date &&
                        x.Fecha.Date <= hasta.Date);
                }

                if (tipoReporte == "Exportaciones")
                {
                    return _context.REPORTE_GENERADO.Count(x =>
                        x.FechaInicioReporte.Date >= desde.Date &&
                        x.FechaFinReporte.Date <= hasta.Date);
                }

                var query =
                    from s in _context.SOLICITUD_CREDITO
                    join p in _context.PERFIL_FINANCIERO
                        on s.Id_Solicitud equals p.SOLICITUD_CREDITO_Id_Solicitud into perfilJoin
                    from p in perfilJoin.DefaultIfEmpty()
                    where s.FechaSolicitud.Date >= desde.Date &&
                          s.FechaSolicitud.Date <= hasta.Date
                    select new { Solicitud = s, Perfil = p };

                if (estado != "Todos")
                    query = query.Where(x => x.Solicitud.Estado == estado);

                if (tipoCredito != "Todos")
                    query = query.Where(x => x.Perfil != null &&
                                             x.Perfil.MotivoPrestamo == tipoCredito);

                return query.Count();
            }
            private void SincronizarReportesPendientes()
            {
                RevisarReportePendiente("Clientes");
                RevisarReportePendiente("Creditos");
                RevisarReportePendiente("Pagos");
                RevisarReportePendiente("Desempeno");
                RevisarReportePendiente("Mora");
                RevisarReportePendiente("Financiero");
                RevisarReportePendiente("Sucursal");
                RevisarReportePendiente("TipoCredito");
                RevisarReportePendiente("Periodo");
                RevisarReportePendiente("Actividad");
                RevisarReportePendiente("Exportaciones");
                RevisarReportePendiente("Personalizado");
            }

            private void RevisarReportePendiente(string tipoReporte)
            {
                var reporte = _context.REPORTE_GENERADO
                    .Where(x => x.TipoReporte == tipoReporte)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefault();

                if (reporte == null) return;

                DateTime ultimaFecha = DateTime.MinValue;

                if (tipoReporte == "Clientes")
                {
                    ultimaFecha = _context.Usuario.Any(x => x.Rol == "Cliente")
                        ? _context.Usuario.Where(x => x.Rol == "Cliente").Max(x => x.FechaRegistro)
                        : DateTime.MinValue;
                }
                else if (tipoReporte == "Pagos" || tipoReporte == "Mora")
                {
                    ultimaFecha = _context.PAGO_CUOTA.Any()
                        ? _context.PAGO_CUOTA.Max(x => x.FechaPago)
                        : DateTime.MinValue;
                }
                else
                {
                    ultimaFecha = _context.SOLICITUD_CREDITO.Any()
                        ? _context.SOLICITUD_CREDITO.Max(x => x.FechaSolicitud)
                        : DateTime.MinValue;
                }

                if (ultimaFecha > reporte.FechaGeneracion)
                {
                    reporte.Estado = "En Proceso";
                    reporte.Descargado = false;
                    reporte.FechaSolicitud = DateTime.Now;

                    _context.SaveChanges();
                }
            }
            private void CargarDatosAdministrador()
            {
                var correoAdmin =
                    User.FindFirst(ClaimTypes.Email)?.Value ??
                    User.FindFirst("Correo")?.Value ??
                    User.FindFirst("correo")?.Value ??
                    User.Identity?.Name;

                var admin = _context.Usuario
                    .FirstOrDefault(x => x.Correo == correoAdmin && x.Rol == "Administrador");

                ViewBag.AdminNombre = admin != null ? $"{admin.Nombre} {admin.Apellido}" : "Administrador";
                ViewBag.AdminCorreo = admin != null ? admin.Correo : "admin@crediplus.com";
                ViewBag.AdminInicial = admin != null ? admin.Nombre.Substring(0, 1).ToUpper() : "A";
            }
            public IActionResult CentroNotificaciones()
            {
                CargarDatosAdministrador();

                ViewData["Title"] = "Centro de Notificaciones";
                ViewData["ActivePage"] = "CentroNotificaciones";

                DateTime primerDiaMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime siguienteMes = primerDiaMes.AddMonths(1);

                var notificaciones = _context.NOTIFICACION_ADMIN
                    .OrderByDescending(x => x.FechaEnvio)
                    .ToList();

                ViewBag.TotalNotificaciones = _context.NOTIFICACION_ADMIN.Count(x =>
                    x.FechaEnvio >= primerDiaMes &&
                    x.FechaEnvio < siguienteMes);

                ViewBag.Entregadas = _context.NOTIFICACION_ADMIN.Count(x =>
                    x.Estado == "Entregada" &&
                    x.FechaEnvio >= primerDiaMes &&
                    x.FechaEnvio < siguienteMes);

                ViewBag.Programadas = _context.NOTIFICACION_ADMIN.Count(x =>
                    x.Estado == "Programada" &&
                    x.FechaEnvio >= primerDiaMes &&
                    x.FechaEnvio < siguienteMes);

                ViewBag.Fallidas = _context.NOTIFICACION_ADMIN.Count(x =>
                    x.Estado == "Fallida" &&
                    x.FechaEnvio >= primerDiaMes &&
                    x.FechaEnvio < siguienteMes);

                int total = ViewBag.TotalNotificaciones;

                ViewBag.PorcentajeEntregadas =
                    total == 0 ? 0 :
                    Math.Round((double)ViewBag.Entregadas * 100 / total, 1);
                ViewBag.Clientes = _context.Usuario
        .Where(x => x.Rol == "Cliente")
        .OrderBy(x => x.Nombre)
        .ToList();

                ViewBag.Analistas = _context.Usuario
                    .Where(x => x.Rol == "Analista")
                    .OrderBy(x => x.Nombre)
                    .ToList();

                ViewBag.AsuntosPlantilla = new List<string>
    {
        "Recordatorio de Pago",
        "Confirmación de Pago",
        "Aprobación de Crédito",
        "Alerta de Mora",
        "Promociones"
    };
            ViewBag.Plantillas = _context.PLANTILLA_NOTIFICACION
    .Where(x => x.Activo == true)
    .OrderByDescending(x => x.FechaCreacion)
    .ToList();

            return View(notificaciones);
            }
            [HttpPost]
            public async Task<IActionResult> EnviarNotificacionAdmin(
        string rolDestino,
        int idUsuarioDestino,
        string canal,
        string asunto,
        string mensaje)
            {
                if (string.IsNullOrWhiteSpace(rolDestino) ||
                    idUsuarioDestino <= 0 ||
                    string.IsNullOrWhiteSpace(asunto) ||
                    string.IsNullOrWhiteSpace(mensaje))
                {
                    TempData["ErrorNotificacion"] = "Debe completar todos los campos antes de enviar la notificación.";
                    return RedirectToAction("CentroNotificaciones");
                }

                var usuario = _context.Usuario
                    .FirstOrDefault(x => x.Id == idUsuarioDestino && x.Rol == rolDestino);

                if (usuario == null)
                    return RedirectToAction("CentroNotificaciones");

                string estado = "Entregada";
                canal = "Correo Electrónico";

            try
            {
                string destinoTexto = $"{usuario.Nombre} {usuario.Apellido} - {usuario.Correo}";

                string html = GenerarHtmlNotificacion(asunto, destinoTexto, mensaje);

                await _emailService.EnviarCorreoAsync(
                    usuario.Correo,
                    asunto,
                    html
                );
            }
            catch (Exception ex)
            {
                estado = "Fallida";
                TempData["ErrorNotificacion"] = "Error al enviar correo: " + ex.Message;
            }

            _context.NOTIFICACION_ADMIN.Add(new NotificacionAdmin
            {
                TipoNotificacion = asunto,
                Asunto = asunto,
                Mensaje = mensaje,
                Canal = "Correo Electrónico",
                Destinatario = $"{rolDestino}: {usuario.Nombre} {usuario.Apellido} - {usuario.Correo}",
                Estado = estado,
                FechaEnvio = DateTime.Now,
                Plantilla = true
            });

            _context.SaveChanges();
            if (estado == "Entregada")
            {
                TempData["OkNotificacion"] =
$"La notificación fue enviada correctamente a {usuario.Nombre} {usuario.Apellido} ({usuario.Correo}).";
            }

            return RedirectToAction("CentroNotificaciones");
            }

            public IActionResult reportesAdministrador(
            DateTime? fechaInicio = null,
            DateTime? fechaFin = null,
            string tipoCreditoFiltro = "Todos",
            string estadoFiltro = "Todos")
            {
                // Fechas por defecto: primer día del año hasta hoy
                DateTime desde = fechaInicio ?? new DateTime(DateTime.Now.Year, 1, 1);
                DateTime hasta = fechaFin ?? DateTime.Today;
                DateTime primerDiaDelMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime primerDiaSiguienteMes = primerDiaDelMes.AddMonths(1);
                DateTime hoy = DateTime.Today;

                var queryBase =
                    from s in _context.SOLICITUD_CREDITO.Include(x => x.USUARIO)
                    join p in _context.PERFIL_FINANCIERO
                        on s.Id_Solicitud equals p.SOLICITUD_CREDITO_Id_Solicitud into perfilJoin
                    from p in perfilJoin.DefaultIfEmpty()
                    where s.FechaSolicitud.Date >= desde.Date &&
                          s.FechaSolicitud.Date <= hasta.Date
                    select new
                    {
                        Solicitud = s,
                        Perfil = p
                    };

                if (estadoFiltro != "Todos")
                {
                    queryBase = queryBase.Where(x => x.Solicitud.Estado == estadoFiltro);
                }

                if (tipoCreditoFiltro != "Todos")
                {
                    queryBase = queryBase.Where(x => x.Perfil != null &&
                                                     x.Perfil.MotivoPrestamo == tipoCreditoFiltro);
                }

                // ─── Stats principales — COUNT y SUM en la BD, no en C# ──────────────
                int totalSolicitudes = queryBase.Count();
                int totalAprobados = queryBase.Count(x => x.Solicitud.Estado == "Aprobado");
                int totalRechazados = queryBase.Count(x => x.Solicitud.Estado == "Rechazado");
                int totalEnEvaluacion = queryBase.Count(x => x.Solicitud.Estado == "Pendiente" || x.Solicitud.Estado == "En Evaluación");
                int totalDesembolsados = queryBase.Count(x => x.Solicitud.Estado == "Cancelado");

                decimal montoOtorgado = queryBase
                    .Where(x => x.Solicitud.Estado == "Aprobado")
                    .Sum(x => (decimal?)x.Solicitud.MontoSolicitado) ?? 0;
                decimal montoEnEvaluacion = queryBase
        .Where(x => x.Solicitud.Estado == "Pendiente" || x.Solicitud.Estado == "En Evaluación")
        .Sum(x => (decimal?)x.Solicitud.MontoSolicitado) ?? 0;
                // ─── Clientes ─────────────────────────────────────────────────────────
                int totalClientes = _context.Usuario.Count(x => x.Rol == "Cliente");
                int clientesEstesMes = _context.Usuario.Count(x =>
                    x.Rol == "Cliente" &&
                    x.FechaRegistro >= primerDiaDelMes &&
                    x.FechaRegistro < primerDiaSiguienteMes);

                // ─── Mora ─────────────────────────────────────────────────────────────
                int totalCuotasEnMora = _context.CUOTA.Count(x =>
                    x.FechaLimitePago.Date < hoy && x.Estado == "Pendiente");

                decimal montoEnMora = _context.CUOTA
                    .Where(x => x.FechaLimitePago.Date < hoy && x.Estado == "Pendiente")
                    .Sum(x => (decimal?)x.MontoCuota) ?? 0;

                // ─── Resumen por mes — agrupado en BD ─────────────────────────────────
                var resumenPorMes = queryBase
        .GroupBy(x => new { x.Solicitud.FechaSolicitud.Year, x.Solicitud.FechaSolicitud.Month })
        .Select(g => new ResumenMesViewModel
        {
            Anio = g.Key.Year,
            Mes = g.Key.Month,
            Solicitudes = g.Count(),
            Aprobados = g.Count(x => x.Solicitud.Estado == "Aprobado"),
            Rechazados = g.Count(x => x.Solicitud.Estado == "Rechazado"),
            MontoDesembolsado = g.Where(x => x.Solicitud.Estado == "Aprobado")
                .Sum(x => (decimal?)x.Solicitud.MontoSolicitado) ?? 0
        })
        .OrderBy(x => x.Anio)
        .ThenBy(x => x.Mes)
        .ToList();
                var totalParaTipos = queryBase.Count();

                var creditosPorTipo = queryBase
                    .GroupBy(x => x.Perfil == null || string.IsNullOrEmpty(x.Perfil.MotivoPrestamo)
                        ? "Sin motivo"
                        : x.Perfil.MotivoPrestamo)
                    .Select(g => new TipoCreditoResumenViewModel
                    {
                        Tipo = g.Key,
                        Cantidad = g.Count(),
                        Monto = g.Sum(x => (decimal?)x.Solicitud.MontoSolicitado) ?? 0,
                        Porcentaje = totalParaTipos > 0
                            ? Math.Round((double)g.Count() * 100 / totalParaTipos, 1)
                            : 0
                    })
                    .OrderByDescending(x => x.Cantidad)
                    .ToList();

                // ─── Resumen total del modelo ──────────────────────────────────────────
                var modelo = new AdminReportesViewModel
                {
                    // Filtros activos (para mostrarlos en la vista)
                    FechaInicio = desde,
                    FechaFin = hasta,
                    TipoCreditoFiltro = tipoCreditoFiltro,
                    EstadoFiltro = estadoFiltro,

                    // Stats principales
                    TotalSolicitudes = totalSolicitudes,
                    TotalAprobados = totalAprobados,
                    TotalRechazados = totalRechazados,
                    MontoOtorgado = montoOtorgado,

                    // Para el resumen de créditos
                    TotalResumenCreditos = totalAprobados + totalEnEvaluacion + totalDesembolsados + totalRechazados,
                    CreditosAprobados = totalAprobados,
                    CreditosEnEvaluacion = totalEnEvaluacion,
                    CreditosDesembolsados = totalDesembolsados,
                    CreditosRechazados = totalRechazados,

                    // Mora y otros
                    TotalClientes = totalClientes,
                    ClientesEstesMes = clientesEstesMes,
                    TotalCuotasEnMora = totalCuotasEnMora,
                    MontoEnMora = montoEnMora,
                    TotalReportesGenerados = _context.REPORTE_GENERADO.Count(),

                    // Tabla resumen por mes
                    ResumenPorMes = resumenPorMes,
                    CreditosPorTipo = creditosPorTipo
                };

                return View(modelo);
            }


            public IActionResult ReporteClientesExcel()
            {
                var clientes = _context.Usuario
                    .Where(x => x.Rol == "Cliente")
                    .OrderByDescending(x => x.FechaRegistro)
                    .ToList();

                using var workbook = new XLWorkbook();
                var hoja = workbook.Worksheets.Add("Clientes");

                hoja.Cell(1, 1).Value = "ID";
                hoja.Cell(1, 2).Value = "Nombre";
                hoja.Cell(1, 3).Value = "Apellido";
                hoja.Cell(1, 4).Value = "DNI";
                hoja.Cell(1, 5).Value = "Celular";
                hoja.Cell(1, 6).Value = "Correo";
                hoja.Cell(1, 7).Value = "Género";
                hoja.Cell(1, 8).Value = "Contraseña";
                hoja.Cell(1, 9).Value = "Fecha Registro";

                int fila = 2;

                foreach (var c in clientes)
                {
                    hoja.Cell(fila, 1).Value = c.Id;
                    hoja.Cell(fila, 2).Value = c.Nombre;
                    hoja.Cell(fila, 3).Value = c.Apellido;
                    hoja.Cell(fila, 4).Value = c.Dni;
                    hoja.Cell(fila, 5).Value = c.Celular;
                    hoja.Cell(fila, 6).Value = c.Correo;
                    hoja.Cell(fila, 7).Value = c.Genero;
                    hoja.Cell(fila, 8).Value = "Protegida";
                    hoja.Cell(fila, 9).Value = c.FechaRegistro.ToString("dd/MM/yyyy hh:mm tt");
                    fila++;
                }

                hoja.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                return File(stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Reporte_Clientes.xlsx");
            }

            public IActionResult ReporteCreditosExcel()
            {
                var creditos = _context.SOLICITUD_CREDITO
                    .Include(x => x.USUARIO)
                    .OrderByDescending(x => x.FechaSolicitud)
                    .ToList();

                using var workbook = new XLWorkbook();
                var hoja = workbook.Worksheets.Add("Creditos");

                hoja.Cell(1, 1).Value = "Solicitud";
                hoja.Cell(1, 2).Value = "Cliente";
                hoja.Cell(1, 3).Value = "DNI";
                hoja.Cell(1, 4).Value = "Monto";
                hoja.Cell(1, 5).Value = "Plazo Meses";
                hoja.Cell(1, 6).Value = "Interés";
                hoja.Cell(1, 7).Value = "Estado";
                hoja.Cell(1, 8).Value = "Fecha Solicitud";

                int fila = 2;

                foreach (var c in creditos)
                {
                    hoja.Cell(fila, 1).Value = c.NumeroSolicitud;
                    hoja.Cell(fila, 2).Value = $"{c.USUARIO?.Nombre} {c.USUARIO?.Apellido}";
                    hoja.Cell(fila, 3).Value = c.USUARIO?.Dni;
                    hoja.Cell(fila, 4).Value = c.MontoSolicitado;
                    hoja.Cell(fila, 5).Value = c.PlazoMeses;
                    hoja.Cell(fila, 6).Value = c.InteresEstimado;
                    hoja.Cell(fila, 7).Value = c.Estado;
                    hoja.Cell(fila, 8).Value = c.FechaSolicitud.ToString("dd/MM/yyyy hh:mm tt");
                    fila++;
                }

                hoja.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Reporte_Creditos.xlsx"
                );
            }

            public IActionResult ReportePagosExcel()
            {
                var solicitudes = _context.SOLICITUD_CREDITO
                    .Include(x => x.USUARIO)
                    .Where(x => x.Estado == "Aprobado")
                    .OrderByDescending(x => x.FechaSolicitud)
                    .ToList();

                var cuotas = _context.CUOTA.ToList();
                var pagos = _context.PAGO_CUOTA.ToList();

                using var workbook = new XLWorkbook();
                var hoja = workbook.Worksheets.Add("Pagos");

                hoja.Cell(1, 1).Value = "Solicitud";
                hoja.Cell(1, 2).Value = "Cliente";
                hoja.Cell(1, 3).Value = "DNI";
                hoja.Cell(1, 4).Value = "Cuotas Pagadas";
                hoja.Cell(1, 5).Value = "Cuotas Pendientes";
                hoja.Cell(1, 6).Value = "Cuotas en Mora";
                hoja.Cell(1, 7).Value = "Monto Pendiente";
                hoja.Cell(1, 8).Value = "Estado Pago";

                int fila = 2;
                DateTime hoy = DateTime.Today;

                foreach (var s in solicitudes)
                {
                    var cuotasSolicitud = cuotas
                        .Where(c => c.SOLICITUD_CREDITO_Id_Solicitud == s.Id_Solicitud)
                        .ToList();

                    int pagadas = pagos
                        .Where(p => p.Estado == "Aprobado")
                        .Join(cuotasSolicitud,
                            p => p.Id_Cuota,
                            c => c.Id_Cuota,
                            (p, c) => c.Id_Cuota)
                        .Distinct()
                        .Count();

                    int pendientes = cuotasSolicitud.Count(c => c.Estado == "Pendiente");

                    int mora = cuotasSolicitud.Count(c =>
                        c.Estado == "Pendiente" &&
                        c.FechaLimitePago.Date < hoy);

                    decimal montoPendiente = cuotasSolicitud
                        .Where(c => c.Estado == "Pendiente")
                        .Sum(c => c.MontoCuota ?? 0);

                    string estadoPago = pagadas > 0 ? "Con pagos registrados" : "Pendiente";

                    hoja.Cell(fila, 1).Value = s.NumeroSolicitud;
                    hoja.Cell(fila, 2).Value = $"{s.USUARIO?.Nombre} {s.USUARIO?.Apellido}";
                    hoja.Cell(fila, 3).Value = s.USUARIO?.Dni;
                    hoja.Cell(fila, 4).Value = pagadas;
                    hoja.Cell(fila, 5).Value = pendientes;
                    hoja.Cell(fila, 6).Value = mora;
                    hoja.Cell(fila, 7).Value = montoPendiente;
                    hoja.Cell(fila, 8).Value = estadoPago;

                    fila++;
                }

                hoja.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                return File(stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Reporte_Pagos.xlsx");
            }
            public async Task<IActionResult> ReporteClientesPdfRapido()
            {

                DateTime desde = new DateTime(2000, 1, 1);
                DateTime hasta = DateTime.Today;

                return await ExportarClientesPdf("", "Todos", desde, hasta);
            }

            public IActionResult ReporteCreditosPdfRapido()
            {

                DateTime desde = new DateTime(2000, 1, 1);
                DateTime hasta = DateTime.Today;

                return ExportarCreditosPdf("", "Todos", desde, hasta);
            }

            public IActionResult ReportePagosPdfRapido()
            {

                DateTime desde = new DateTime(2000, 1, 1);
                DateTime hasta = DateTime.Today;

                return PagosPdf("", "Todos", "Todos", desde, hasta);
            }
            private void RegistrarReporteMensual(string tipoReporte)
            {
                int mes = DateTime.Now.Month;
                int anio = DateTime.Now.Year;

                bool existe = _context.REPORTE_GENERADO.Any(x =>
                    x.TipoReporte == tipoReporte &&
                    x.Mes == mes &&
                    x.Anio == anio);

                if (!existe)
                {
                    _context.REPORTE_GENERADO.Add(new ReporteGenerado
                    {
                        TipoReporte = tipoReporte,
                        Mes = mes,
                        Anio = anio,
                        FechaGeneracion = DateTime.Now
                    });

                    _context.SaveChanges();
                }
            }
            private void RegistrarReporteRapido(string tipoReporte, string formato)
            {
                var nuevo = new ReporteGenerado
                {
                    TipoReporte = tipoReporte,
                    Mes = DateTime.Now.Month,
                    Anio = DateTime.Now.Year,
                    FechaGeneracion = DateTime.Now,
                    FechaInicioReporte = new DateTime(2000, 1, 1),
                    FechaFinReporte = DateTime.Today,
                    EstadoFiltro = "Todos",
                    TipoCreditoFiltro = "Todos",
                    Descripcion = $"Reporte de {tipoReporte}",
                    Formato = formato,
                    SolicitadoPor = "Administrador",
                    FechaSolicitud = DateTime.Now,
                    Estado = "Completado",
                    Descargado = true,
                    CantidadDatos = ObtenerCantidadDatosReporte(tipoReporte, new DateTime(2000, 1, 1), DateTime.Today, "Todos", "Todos")
                };

                _context.REPORTE_GENERADO.Add(nuevo);
                _context.SaveChanges();
            }
            public IActionResult ReporteAdministradorExcel(
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        string tipoCreditoFiltro = "Todos",
        string estadoFiltro = "Todos",
        string nombreReporte = "Administrador")
            {
                DateTime fechaDesdeBase = fechaInicio ?? DateTime.Today;
                DateTime fechaHastaBase = fechaFin ?? DateTime.Today;

                DateTime desde = new DateTime(fechaDesdeBase.Year, fechaDesdeBase.Month, 1);
                DateTime hasta = new DateTime(
                    fechaHastaBase.Year,
                    fechaHastaBase.Month,
                    DateTime.DaysInMonth(fechaHastaBase.Year, fechaHastaBase.Month)
                );

                var query =
                    from s in _context.SOLICITUD_CREDITO.Include(x => x.USUARIO)
                    join p in _context.PERFIL_FINANCIERO
                        on s.Id_Solicitud equals p.SOLICITUD_CREDITO_Id_Solicitud into perfilJoin
                    from p in perfilJoin.DefaultIfEmpty()
                    where s.FechaSolicitud.Date >= desde.Date &&
                          s.FechaSolicitud.Date <= hasta.Date
                    select new { Solicitud = s, Perfil = p };

                if (estadoFiltro != "Todos")
                    query = query.Where(x => x.Solicitud.Estado == estadoFiltro);

                if (tipoCreditoFiltro != "Todos")
                    query = query.Where(x => x.Perfil != null &&
                                             x.Perfil.MotivoPrestamo == tipoCreditoFiltro);

                var datos = query.OrderByDescending(x => x.Solicitud.FechaSolicitud).ToList();

                int total = datos.Count;
                int aprobados = datos.Count(x => x.Solicitud.Estado == "Aprobado");
                int rechazados = datos.Count(x => x.Solicitud.Estado == "Rechazado");
                int pendientes = datos.Count(x => x.Solicitud.Estado == "Pendiente" || x.Solicitud.Estado == "En Evaluación");
                decimal montoDesembolsado = datos.Where(x => x.Solicitud.Estado == "Aprobado").Sum(x => x.Solicitud.MontoSolicitado);
                decimal ticketPromedio = aprobados > 0 ? montoDesembolsado / aprobados : 0;

                using var workbook = new XLWorkbook();
                if (nombreReporte == "Desempeno")
                {
                    var hoja = workbook.Worksheets.Add("Desempeño");

                    hoja.Cell(1, 1).Value = "Estado";
                    hoja.Cell(1, 2).Value = "Cantidad";
                    hoja.Cell(1, 3).Value = "Monto Total";

                    int filaDesempeno = 2;

                    foreach (var g in datos.GroupBy(x => x.Solicitud.Estado))
                    {
                        hoja.Cell(filaDesempeno, 1).Value = g.Key;
                        hoja.Cell(filaDesempeno, 2).Value = g.Count();
                        hoja.Cell(filaDesempeno, 3).Value = g.Sum(x => x.Solicitud.MontoSolicitado);
                        filaDesempeno++;
                    }

                    hoja.Columns().AdjustToContents();

                    using var streamDesempeno = new MemoryStream();
                    workbook.SaveAs(streamDesempeno);

                    return File(streamDesempeno.ToArray(),
                                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Reporte_Desempeno_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
                }

                if (nombreReporte == "Mora")
                {
                    var mora = (
                        from c in _context.CUOTA
                        join s in _context.SOLICITUD_CREDITO on c.SOLICITUD_CREDITO_Id_Solicitud equals s.Id_Solicitud
                        join u in _context.Usuario on s.Usuario_Id_Usuario equals u.Id
                        where c.Estado == "Pendiente"
                              && c.FechaLimitePago.Date >= desde.Date
                              && c.FechaLimitePago.Date <= hasta.Date
                              && c.FechaLimitePago.Date < DateTime.Today
                        select new
                        {
                            Cliente = u.Nombre + " " + u.Apellido,
                            Dni = u.Dni,
                            Solicitud = s.NumeroSolicitud,
                            Monto = c.MontoCuota ?? 0,
                            FechaVencimiento = c.FechaLimitePago,
                            DiasMora = EF.Functions.DateDiffDay(c.FechaLimitePago, DateTime.Today)
                        }
                    ).ToList();

                    var hoja = workbook.Worksheets.Add("Mora");

                    hoja.Cell(1, 1).Value = "Cliente";
                    hoja.Cell(1, 2).Value = "DNI";
                    hoja.Cell(1, 3).Value = "Solicitud";
                    hoja.Cell(1, 4).Value = "Monto Vencido";
                    hoja.Cell(1, 5).Value = "Fecha Vencimiento";
                    hoja.Cell(1, 6).Value = "Días en Mora";

                    int filaDesempeno = 2;

                    foreach (var item in mora)
                    {
                        hoja.Cell(filaDesempeno, 1).Value = item.Cliente;
                        hoja.Cell(filaDesempeno, 2).Value = item.Dni;
                        hoja.Cell(filaDesempeno, 3).Value = item.Solicitud;
                        hoja.Cell(filaDesempeno, 4).Value = item.Monto;
                        hoja.Cell(filaDesempeno, 5).Value = item.FechaVencimiento.ToString("dd/MM/yyyy");
                        hoja.Cell(filaDesempeno, 6).Value = item.DiasMora;
                        filaDesempeno++;
                    }

                    hoja.Columns().AdjustToContents();

                    using var streamDesempeno = new MemoryStream();
                    workbook.SaveAs(streamDesempeno);

                    return File(streamDesempeno.ToArray(),
                                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Reporte_Mora_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
                }
                if (nombreReporte == "Financiero")
                {
                    var hoja = workbook.Worksheets.Add("Financiero");

                    decimal totalSolicitado = datos.Sum(x => x.Solicitud.MontoSolicitado);
                    decimal montoAprobado = datos
                        .Where(x => x.Solicitud.Estado == "Aprobado")
                        .Sum(x => x.Solicitud.MontoSolicitado);

                    decimal montoEvaluacion = datos
                        .Where(x => x.Solicitud.Estado == "Pendiente" || x.Solicitud.Estado == "En Evaluación")
                        .Sum(x => x.Solicitud.MontoSolicitado);

                    decimal montoRechazado = datos
                        .Where(x => x.Solicitud.Estado == "Rechazado")
                        .Sum(x => x.Solicitud.MontoSolicitado);

                    hoja.Cell(1, 1).Value = "CREDIPLUS - REPORTE FINANCIERO";
                    hoja.Range("A1:D1").Merge();
                    hoja.Cell(1, 1).Style.Font.Bold = true;
                    hoja.Cell(1, 1).Style.Font.FontSize = 16;

                    hoja.Cell(3, 1).Value = "Fecha Inicio";
                    hoja.Cell(3, 2).Value = desde.ToString("dd/MM/yyyy");
                    hoja.Cell(4, 1).Value = "Fecha Fin";
                    hoja.Cell(4, 2).Value = hasta.ToString("dd/MM/yyyy");

                    hoja.Cell(6, 1).Value = "Indicador";
                    hoja.Cell(6, 2).Value = "Cantidad";
                    hoja.Cell(6, 3).Value = "Monto";

                    hoja.Cell(7, 1).Value = "Solicitudes totales";
                    hoja.Cell(7, 2).Value = total;
                    hoja.Cell(7, 3).Value = totalSolicitado;

                    hoja.Cell(8, 1).Value = "Créditos aprobados";
                    hoja.Cell(8, 2).Value = aprobados;
                    hoja.Cell(8, 3).Value = montoAprobado;

                    hoja.Cell(9, 1).Value = "Pendientes / En evaluación";
                    hoja.Cell(9, 2).Value = pendientes;
                    hoja.Cell(9, 3).Value = montoEvaluacion;

                    hoja.Cell(10, 1).Value = "Créditos rechazados";
                    hoja.Cell(10, 2).Value = rechazados;
                    hoja.Cell(10, 3).Value = montoRechazado;

                    hoja.Cell(11, 1).Value = "Ticket promedio aprobado";
                    hoja.Cell(11, 2).Value = aprobados > 0 ? montoAprobado / aprobados : 0;
                    hoja.Cell(11, 3).Value = "";

                    hoja.Range("A6:C6").Style.Font.Bold = true;
                    hoja.Range("A6:C6").Style.Fill.BackgroundColor = XLColor.FromHtml("#ede9fe");
                    hoja.Column(3).Style.NumberFormat.Format = "\"S/\" #,##0.00";
                    hoja.Cell(11, 2).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                    hoja.Columns().AdjustToContents();

                    using var streamFinanciero = new MemoryStream();
                    workbook.SaveAs(streamFinanciero);

                    return File(
                        streamFinanciero.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Reporte_Financiero_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                    );
                }

                if (nombreReporte == "Sucursal")
                {
                    var hoja = workbook.Worksheets.Add("Sucursal");

                    var datosSucursal = datos
                        .GroupBy(x => "Sucursal Principal")
                        .Select(g => new
                        {
                            Sucursal = g.Key,
                            Solicitudes = g.Count(),
                            Aprobados = g.Count(x => x.Solicitud.Estado == "Aprobado"),
                            Pendientes = g.Count(x => x.Solicitud.Estado == "Pendiente" || x.Solicitud.Estado == "En Evaluación"),
                            Rechazados = g.Count(x => x.Solicitud.Estado == "Rechazado"),
                            MontoAprobado = g.Where(x => x.Solicitud.Estado == "Aprobado").Sum(x => x.Solicitud.MontoSolicitado)
                        })
                        .ToList();

                    hoja.Cell(1, 1).Value = "CREDIPLUS - REPORTE POR SUCURSAL";
                    hoja.Range("A1:F1").Merge();
                    hoja.Cell(1, 1).Style.Font.Bold = true;
                    hoja.Cell(1, 1).Style.Font.FontSize = 16;

                    hoja.Cell(2, 1).Value = $"Periodo: {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}";

                    hoja.Cell(4, 1).Value = "Sucursal";
                    hoja.Cell(4, 2).Value = "Solicitudes";
                    hoja.Cell(4, 3).Value = "Aprobados";
                    hoja.Cell(4, 4).Value = "Pendientes / En Evaluación";
                    hoja.Cell(4, 5).Value = "Rechazados";
                    hoja.Cell(4, 6).Value = "Monto Aprobado";

                    int filaSucursal = 5;

                    foreach (var item in datosSucursal)
                    {
                        hoja.Cell(filaSucursal, 1).Value = item.Sucursal;
                        hoja.Cell(filaSucursal, 2).Value = item.Solicitudes;
                        hoja.Cell(filaSucursal, 3).Value = item.Aprobados;
                        hoja.Cell(filaSucursal, 4).Value = item.Pendientes;
                        hoja.Cell(filaSucursal, 5).Value = item.Rechazados;
                        hoja.Cell(filaSucursal, 6).Value = item.MontoAprobado;
                        filaSucursal++;
                    }

                    if (!datosSucursal.Any())
                    {
                        hoja.Cell(5, 1).Value = "No hay datos para este periodo.";
                    }

                    hoja.Range("A4:F4").Style.Font.Bold = true;
                    hoja.Range("A4:F4").Style.Fill.BackgroundColor = XLColor.FromHtml("#ede9fe");
                    hoja.Column(6).Style.NumberFormat.Format = "\"S/\" #,##0.00";
                    hoja.Columns().AdjustToContents();

                    using var streamSucursal = new MemoryStream();
                    workbook.SaveAs(streamSucursal);

                    return File(
                        streamSucursal.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Reporte_Sucursal_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                    );
                }

                if (nombreReporte == "TipoCredito")
                {
                    var hoja = workbook.Worksheets.Add("TipoCredito");

                    var datosTipo = datos
                        .GroupBy(x => string.IsNullOrEmpty(x.Perfil?.MotivoPrestamo) ? "Sin motivo" : x.Perfil.MotivoPrestamo)
                        .Select(g => new
                        {
                            Tipo = g.Key,
                            Solicitudes = g.Count(),
                            Aprobados = g.Count(x => x.Solicitud.Estado == "Aprobado"),
                            Rechazados = g.Count(x => x.Solicitud.Estado == "Rechazado"),
                            Pendientes = g.Count(x => x.Solicitud.Estado == "Pendiente" || x.Solicitud.Estado == "En Evaluación"),
                            MontoAprobado = g.Where(x => x.Solicitud.Estado == "Aprobado").Sum(x => x.Solicitud.MontoSolicitado)
                        })
                        .OrderByDescending(x => x.Solicitudes)
                        .ToList();

                    hoja.Cell(1, 1).Value = "CREDIPLUS - REPORTE POR TIPO DE CRÉDITO";
                    hoja.Range("A1:F1").Merge();
                    hoja.Cell(1, 1).Style.Font.Bold = true;
                    hoja.Cell(1, 1).Style.Font.FontSize = 16;

                    hoja.Cell(2, 1).Value = $"Periodo: {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}";

                    hoja.Cell(4, 1).Value = "Tipo de Crédito";
                    hoja.Cell(4, 2).Value = "Solicitudes";
                    hoja.Cell(4, 3).Value = "Aprobados";
                    hoja.Cell(4, 4).Value = "Pendientes / En Evaluación";
                    hoja.Cell(4, 5).Value = "Rechazados";
                    hoja.Cell(4, 6).Value = "Monto Aprobado";

                    int filaTipo = 5;

                    foreach (var item in datosTipo)
                    {
                        hoja.Cell(filaTipo, 1).Value = item.Tipo;
                        hoja.Cell(filaTipo, 2).Value = item.Solicitudes;
                        hoja.Cell(filaTipo, 3).Value = item.Aprobados;
                        hoja.Cell(filaTipo, 4).Value = item.Pendientes;
                        hoja.Cell(filaTipo, 5).Value = item.Rechazados;
                        hoja.Cell(filaTipo, 6).Value = item.MontoAprobado;
                        filaTipo++;
                    }

                    if (!datosTipo.Any())
                    {
                        hoja.Cell(5, 1).Value = "No hay datos para este periodo.";
                    }

                    hoja.Range("A4:F4").Style.Font.Bold = true;
                    hoja.Range("A4:F4").Style.Fill.BackgroundColor = XLColor.FromHtml("#ede9fe");
                    hoja.Column(6).Style.NumberFormat.Format = "\"S/\" #,##0.00";
                    hoja.Columns().AdjustToContents();

                    using var streamTipoCredito = new MemoryStream();
                    workbook.SaveAs(streamTipoCredito);

                    return File(
                        streamTipoCredito.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Reporte_TipoCredito_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                    );
                }

                if (nombreReporte == "Periodo")
                {
                    var hoja = workbook.Worksheets.Add("Periodo");

                    var datosPeriodo = datos
                        .GroupBy(x => new { x.Solicitud.FechaSolicitud.Year, x.Solicitud.FechaSolicitud.Month })
                        .Select(g => new
                        {
                            Anio = g.Key.Year,
                            Mes = g.Key.Month,
                            Solicitudes = g.Count(),
                            Aprobadas = g.Count(x => x.Solicitud.Estado == "Aprobado"),
                            Rechazadas = g.Count(x => x.Solicitud.Estado == "Rechazado"),
                            Pendientes = g.Count(x => x.Solicitud.Estado == "Pendiente" || x.Solicitud.Estado == "En Evaluación"),
                            MontoAprobado = g.Where(x => x.Solicitud.Estado == "Aprobado").Sum(x => x.Solicitud.MontoSolicitado)
                        })
                        .OrderBy(x => x.Anio)
                        .ThenBy(x => x.Mes)
                        .ToList();

                    hoja.Cell(1, 1).Value = "CREDIPLUS - REPORTE POR PERIODO";
                    hoja.Range("A1:F1").Merge();
                    hoja.Cell(1, 1).Style.Font.Bold = true;
                    hoja.Cell(1, 1).Style.Font.FontSize = 16;

                    hoja.Cell(2, 1).Value = $"Periodo: {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}";

                    hoja.Cell(4, 1).Value = "Periodo";
                    hoja.Cell(4, 2).Value = "Solicitudes";
                    hoja.Cell(4, 3).Value = "Aprobadas";
                    hoja.Cell(4, 4).Value = "Pendientes / En Evaluación";
                    hoja.Cell(4, 5).Value = "Rechazadas";
                    hoja.Cell(4, 6).Value = "Monto Aprobado";

                    int filaPeriodo = 5;

                    foreach (var item in datosPeriodo)
                    {
                        hoja.Cell(filaPeriodo, 1).Value = $"{item.Mes:00}/{item.Anio}";
                        hoja.Cell(filaPeriodo, 2).Value = item.Solicitudes;
                        hoja.Cell(filaPeriodo, 3).Value = item.Aprobadas;
                        hoja.Cell(filaPeriodo, 4).Value = item.Pendientes;
                        hoja.Cell(filaPeriodo, 5).Value = item.Rechazadas;
                        hoja.Cell(filaPeriodo, 6).Value = item.MontoAprobado;
                        filaPeriodo++;
                    }

                    if (!datosPeriodo.Any())
                    {
                        hoja.Cell(5, 1).Value = "No hay datos para este periodo.";
                    }

                    hoja.Range("A4:F4").Style.Font.Bold = true;
                    hoja.Range("A4:F4").Style.Fill.BackgroundColor = XLColor.FromHtml("#ede9fe");
                    hoja.Column(6).Style.NumberFormat.Format = "\"S/\" #,##0.00";
                    hoja.Columns().AdjustToContents();

                    using var streamPeriodo = new MemoryStream();
                    workbook.SaveAs(streamPeriodo);

                    return File(
                        streamPeriodo.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Reporte_Periodo_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                    );
                }

                if (nombreReporte == "Actividad")
                {
                    var hoja = workbook.Worksheets.Add("Actividad");

                    var actividades = _context.ACTIVIDAD_ADMINISTRADOR
                        .Where(x => x.Fecha.Date >= desde.Date && x.Fecha.Date <= hasta.Date)
                        .OrderByDescending(x => x.Fecha)
                        .ToList();

                    hoja.Cell(1, 1).Value = "CREDIPLUS - REPORTE DE ACTIVIDAD";
                    hoja.Range("A1:D1").Merge();
                    hoja.Cell(1, 1).Style.Font.Bold = true;
                    hoja.Cell(1, 1).Style.Font.FontSize = 16;

                    hoja.Cell(3, 1).Value = "Tipo";
                    hoja.Cell(3, 2).Value = "Descripción";
                    hoja.Cell(3, 3).Value = "Fecha";
                    hoja.Cell(3, 4).Value = "Hora";

                    int filaActividad = 4;

                    foreach (var item in actividades)
                    {
                        hoja.Cell(filaActividad, 1).Value = item.Tipo;
                        hoja.Cell(filaActividad, 2).Value = item.Descripcion;
                        hoja.Cell(filaActividad, 3).Value = item.Fecha.ToString("dd/MM/yyyy");
                        hoja.Cell(filaActividad, 4).Value = item.Fecha.ToString("hh:mm tt");

                        filaActividad++;
                    }

                    hoja.Range("A3:D3").Style.Font.Bold = true;
                    hoja.Range("A3:D3").Style.Fill.BackgroundColor = XLColor.FromHtml("#ede9fe");

                    hoja.Columns().AdjustToContents();

                    using var streamActividad = new MemoryStream();
                    workbook.SaveAs(streamActividad);

                    return File(
                        streamActividad.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Reporte_Actividad_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                    );
                }

                if (nombreReporte == "Exportaciones")
                {
                    var hoja = workbook.Worksheets.Add("Exportaciones");

                    var exportaciones = _context.REPORTE_GENERADO
                        .Where(x => x.FechaSolicitud.Date >= desde.Date && x.FechaSolicitud.Date <= hasta.Date)
                        .OrderByDescending(x => x.FechaSolicitud)
                        .ToList();

                    hoja.Cell(1, 1).Value = "CREDIPLUS - REPORTE DE EXPORTACIONES";
                    hoja.Range("A1:G1").Merge();
                    hoja.Cell(1, 1).Style.Font.Bold = true;
                    hoja.Cell(1, 1).Style.Font.FontSize = 16;

                    hoja.Cell(3, 1).Value = "ID";
                    hoja.Cell(3, 2).Value = "Tipo Reporte";
                    hoja.Cell(3, 3).Value = "Descripción";
                    hoja.Cell(3, 4).Value = "Formato";
                    hoja.Cell(3, 5).Value = "Solicitado Por";
                    hoja.Cell(3, 6).Value = "Fecha Solicitud";
                    hoja.Cell(3, 7).Value = "Estado";

                    int filaExportaciones = 4;

                    foreach (var item in exportaciones)
                    {
                        hoja.Cell(filaExportaciones, 1).Value = $"EXP-{item.Id:00000}";
                        hoja.Cell(filaExportaciones, 2).Value = item.TipoReporte;
                        hoja.Cell(filaExportaciones, 3).Value = item.Descripcion;
                        hoja.Cell(filaExportaciones, 4).Value = item.Formato;
                        hoja.Cell(filaExportaciones, 5).Value = item.SolicitadoPor;
                        hoja.Cell(filaExportaciones, 6).Value = item.FechaSolicitud.ToString("dd/MM/yyyy hh:mm tt");
                        hoja.Cell(filaExportaciones, 7).Value = item.Estado;

                        filaExportaciones++;
                    }

                    hoja.Range("A3:G3").Style.Font.Bold = true;
                    hoja.Range("A3:G3").Style.Fill.BackgroundColor = XLColor.FromHtml("#ede9fe");

                    hoja.Columns().AdjustToContents();

                    using var streamExportaciones = new MemoryStream();
                    workbook.SaveAs(streamExportaciones);

                    return File(
                        streamExportaciones.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Reporte_Exportaciones_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                    );
                }

                if (nombreReporte == "Personalizado")
                {
                    var hoja = workbook.Worksheets.Add("Personalizado");

                    hoja.Cell(1, 1).Value = "CREDIPLUS - REPORTE PERSONALIZADO";
                    hoja.Range("A1:H1").Merge();
                    hoja.Cell(1, 1).Style.Font.Bold = true;
                    hoja.Cell(1, 1).Style.Font.FontSize = 16;

                    hoja.Cell(3, 1).Value = "Solicitud";
                    hoja.Cell(3, 2).Value = "Cliente";
                    hoja.Cell(3, 3).Value = "DNI";
                    hoja.Cell(3, 4).Value = "Motivo";
                    hoja.Cell(3, 5).Value = "Monto";
                    hoja.Cell(3, 6).Value = "Plazo";
                    hoja.Cell(3, 7).Value = "Estado";
                    hoja.Cell(3, 8).Value = "Fecha";

                    int filaPersonalizado = 4;

                    foreach (var item in datos)
                    {
                        hoja.Cell(filaPersonalizado, 1).Value = item.Solicitud.NumeroSolicitud;
                        hoja.Cell(filaPersonalizado, 2).Value = $"{item.Solicitud.USUARIO?.Nombre} {item.Solicitud.USUARIO?.Apellido}";
                        hoja.Cell(filaPersonalizado, 3).Value = item.Solicitud.USUARIO?.Dni;
                        hoja.Cell(filaPersonalizado, 4).Value = item.Perfil?.MotivoPrestamo ?? "Sin motivo";
                        hoja.Cell(filaPersonalizado, 5).Value = item.Solicitud.MontoSolicitado;
                        hoja.Cell(filaPersonalizado, 6).Value = item.Solicitud.PlazoMeses;
                        hoja.Cell(filaPersonalizado, 7).Value = item.Solicitud.Estado;
                        hoja.Cell(filaPersonalizado, 8).Value = item.Solicitud.FechaSolicitud.ToString("dd/MM/yyyy");

                        filaPersonalizado++;
                    }

                    hoja.Range("A3:H3").Style.Font.Bold = true;
                    hoja.Range("A3:H3").Style.Fill.BackgroundColor = XLColor.FromHtml("#ede9fe");
                    hoja.Column(5).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                    hoja.Columns().AdjustToContents();

                    using var streamPersonalizado = new MemoryStream();
                    workbook.SaveAs(streamPersonalizado);

                    return File(
                        streamPersonalizado.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Reporte_Personalizado_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                    );
                }

                var resumen = workbook.Worksheets.Add("Resumen General");

                resumen.Cell("A1").Value = "CREDIPLUS - REPORTE ADMINISTRADOR";
                resumen.Range("A1:E1").Merge().Style.Font.Bold = true;
                resumen.Range("A1:E1").Style.Font.FontSize = 16;
                resumen.Range("A1:E1").Style.Font.FontColor = XLColor.White;
                resumen.Range("A1:E1").Style.Fill.BackgroundColor = XLColor.FromHtml("#4c1d95");

                resumen.Cell("A3").Value = "Fecha inicio";
                resumen.Cell("B3").Value = desde.ToString("dd/MM/yyyy");
                resumen.Cell("A4").Value = "Fecha fin";
                resumen.Cell("B4").Value = hasta.ToString("dd/MM/yyyy");
                resumen.Cell("A5").Value = "Motivo préstamo";
                resumen.Cell("B5").Value = tipoCreditoFiltro;
                resumen.Cell("A6").Value = "Estado";
                resumen.Cell("B6").Value = estadoFiltro;
                resumen.Cell("A7").Value = "Generado";
                resumen.Cell("B7").Value = DateTime.Now.ToString("dd/MM/yyyy hh:mm tt");

                resumen.Cell("A10").Value = "Indicador";
                resumen.Cell("B10").Value = "Valor";

                resumen.Cell("A11").Value = "Solicitudes totales";
                resumen.Cell("B11").Value = total;
                resumen.Cell("A12").Value = "Créditos aprobados";
                resumen.Cell("B12").Value = aprobados;
                resumen.Cell("A13").Value = "Créditos rechazados";
                resumen.Cell("B13").Value = rechazados;
                resumen.Cell("A14").Value = "Pendientes / En evaluación";
                resumen.Cell("B14").Value = pendientes;
                resumen.Cell("A15").Value = "Monto desembolsado";
                resumen.Cell("B15").Value = montoDesembolsado;
                resumen.Cell("A16").Value = "Ticket promedio";
                resumen.Cell("B16").Value = ticketPromedio;
                resumen.Cell("A17").Value = "Índice de aprobación";
                resumen.Cell("B17").Value = total > 0 ? $"{Math.Round((double)aprobados * 100 / total, 1)}%" : "0%";
                resumen.Cell("A18").Value = "Índice de rechazo";
                resumen.Cell("B18").Value = total > 0 ? $"{Math.Round((double)rechazados * 100 / total, 1)}%" : "0%";

                resumen.Range("A10:B10").Style.Font.Bold = true;
                resumen.Range("A10:B10").Style.Fill.BackgroundColor = XLColor.FromHtml("#ede9fe");
                resumen.Range("A10:B18").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                resumen.Range("A10:B18").Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                resumen.Range("B15:B16").Style.NumberFormat.Format = "\"S/\" #,##0.00";

                // ===================== HOJA 2: RESUMEN POR MES =====================
                var porMes = workbook.Worksheets.Add("Resumen por Mes");

                porMes.Cell(1, 1).Value = "Mes";
                porMes.Cell(1, 2).Value = "Solicitudes";
                porMes.Cell(1, 3).Value = "Aprobados";
                porMes.Cell(1, 4).Value = "Rechazados";
                porMes.Cell(1, 5).Value = "Monto Desembolsado";

                int fila = 2;

                var gruposMes = datos
                    .GroupBy(x => new { x.Solicitud.FechaSolicitud.Year, x.Solicitud.FechaSolicitud.Month })
                    .OrderBy(x => x.Key.Year)
                    .ThenBy(x => x.Key.Month);

                foreach (var g in gruposMes)
                {
                    porMes.Cell(fila, 1).Value = $"{g.Key.Month}/{g.Key.Year}";
                    porMes.Cell(fila, 2).Value = g.Count();
                    porMes.Cell(fila, 3).Value = g.Count(x => x.Solicitud.Estado == "Aprobado");
                    porMes.Cell(fila, 4).Value = g.Count(x => x.Solicitud.Estado == "Rechazado");
                    porMes.Cell(fila, 5).Value = g.Where(x => x.Solicitud.Estado == "Aprobado").Sum(x => x.Solicitud.MontoSolicitado);
                    fila++;
                }

                porMes.Cell(fila, 1).Value = "TOTAL";
                porMes.Cell(fila, 2).Value = total;
                porMes.Cell(fila, 3).Value = aprobados;
                porMes.Cell(fila, 4).Value = rechazados;
                porMes.Cell(fila, 5).Value = montoDesembolsado;

                porMes.Range(1, 1, 1, 5).Style.Font.Bold = true;
                porMes.Range(1, 1, 1, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#ede9fe");
                porMes.Range(fila, 1, fila, 5).Style.Font.Bold = true;
                porMes.Column(5).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                // ===================== HOJA 3: CRÉDITOS POR TIPO =====================
                var porTipo = workbook.Worksheets.Add("Créditos por Tipo");

                porTipo.Cell(1, 1).Value = "Motivo préstamo";
                porTipo.Cell(1, 2).Value = "Cantidad";
                porTipo.Cell(1, 3).Value = "Porcentaje";
                porTipo.Cell(1, 4).Value = "Monto total";

                fila = 2;

                foreach (var g in datos.GroupBy(x => string.IsNullOrEmpty(x.Perfil?.MotivoPrestamo) ? "Sin motivo" : x.Perfil.MotivoPrestamo)
                                       .OrderByDescending(x => x.Count()))
                {
                    porTipo.Cell(fila, 1).Value = g.Key;
                    porTipo.Cell(fila, 2).Value = g.Count();
                    porTipo.Cell(fila, 3).Value = total > 0 ? Math.Round((double)g.Count() * 100 / total, 1) / 100 : 0;
                    porTipo.Cell(fila, 4).Value = g.Sum(x => x.Solicitud.MontoSolicitado);
                    fila++;
                }

                porTipo.Range(1, 1, 1, 4).Style.Font.Bold = true;
                porTipo.Range(1, 1, 1, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#ede9fe");
                porTipo.Column(3).Style.NumberFormat.Format = "0.0%";
                porTipo.Column(4).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                // ===================== HOJA 4: DETALLE POR USUARIO =====================
                var detalle = workbook.Worksheets.Add("Detalle Usuarios");

                detalle.Cell(1, 1).Value = "Solicitud";
                detalle.Cell(1, 2).Value = "Cliente";
                detalle.Cell(1, 3).Value = "DNI";
                detalle.Cell(1, 4).Value = "Motivo préstamo";
                detalle.Cell(1, 5).Value = "Monto";
                detalle.Cell(1, 6).Value = "Plazo meses";
                detalle.Cell(1, 7).Value = "Interés";
                detalle.Cell(1, 8).Value = "Estado";
                detalle.Cell(1, 9).Value = "Fecha solicitud";

                fila = 2;

                foreach (var item in datos)
                {
                    detalle.Cell(fila, 1).Value = item.Solicitud.NumeroSolicitud;
                    detalle.Cell(fila, 2).Value = $"{item.Solicitud.USUARIO?.Nombre} {item.Solicitud.USUARIO?.Apellido}";
                    detalle.Cell(fila, 3).Value = item.Solicitud.USUARIO?.Dni;
                    detalle.Cell(fila, 4).Value = item.Perfil?.MotivoPrestamo ?? "Sin motivo";
                    detalle.Cell(fila, 5).Value = item.Solicitud.MontoSolicitado;
                    detalle.Cell(fila, 6).Value = item.Solicitud.PlazoMeses;
                    detalle.Cell(fila, 7).Value = item.Solicitud.InteresEstimado;
                    detalle.Cell(fila, 8).Value = item.Solicitud.Estado;
                    detalle.Cell(fila, 9).Value = item.Solicitud.FechaSolicitud.ToString("dd/MM/yyyy");
                    fila++;
                }

                detalle.Range(1, 1, 1, 9).Style.Font.Bold = true;
                detalle.Range(1, 1, 1, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#ede9fe");
                detalle.Column(5).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                // ===================== ESTILO GENERAL =====================
                foreach (var hoja in workbook.Worksheets)
                {
                    hoja.Columns().AdjustToContents();
                    hoja.Rows().AdjustToContents();
                    hoja.SheetView.FreezeRows(1);

                    var used = hoja.RangeUsed();
                    if (used != null)
                    {
                        used.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        used.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    }
                }

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Reporte_{nombreReporte}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                );
            }
            public IActionResult ReporteAdministradorPdf(
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        string tipoCreditoFiltro = "Todos",
        string estadoFiltro = "Todos",
        string nombreReporte = "Administrador")
            {
                QuestPDF.Settings.License = LicenseType.Community;

                DateTime fechaDesdeBase = fechaInicio ?? DateTime.Today;
                DateTime fechaHastaBase = fechaFin ?? DateTime.Today;

                DateTime desde = new DateTime(fechaDesdeBase.Year, fechaDesdeBase.Month, 1);
                DateTime hasta = new DateTime(
                    fechaHastaBase.Year,
                    fechaHastaBase.Month,
                    DateTime.DaysInMonth(fechaHastaBase.Year, fechaHastaBase.Month)
                );

                var query =
                    from s in _context.SOLICITUD_CREDITO.Include(x => x.USUARIO)
                    join p in _context.PERFIL_FINANCIERO
                        on s.Id_Solicitud equals p.SOLICITUD_CREDITO_Id_Solicitud into perfilJoin
                    from p in perfilJoin.DefaultIfEmpty()
                    where s.FechaSolicitud.Date >= desde.Date &&
                          s.FechaSolicitud.Date <= hasta.Date
                    select new { Solicitud = s, Perfil = p };

                if (estadoFiltro != "Todos")
                    query = query.Where(x => x.Solicitud.Estado == estadoFiltro);

                if (tipoCreditoFiltro != "Todos")
                    query = query.Where(x => x.Perfil != null && x.Perfil.MotivoPrestamo == tipoCreditoFiltro);

                var datos = query.OrderByDescending(x => x.Solicitud.FechaSolicitud).ToList();

                int total = datos.Count;
                int aprobados = datos.Count(x => x.Solicitud.Estado == "Aprobado");
                int rechazados = datos.Count(x => x.Solicitud.Estado == "Rechazado");
                decimal monto = datos.Where(x => x.Solicitud.Estado == "Aprobado").Sum(x => x.Solicitud.MontoSolicitado);

                var porMes = datos
                    .GroupBy(x => new { x.Solicitud.FechaSolicitud.Year, x.Solicitud.FechaSolicitud.Month })
                    .OrderBy(x => x.Key.Year)
                    .ThenBy(x => x.Key.Month)
                    .ToList();

                var porTipo = datos
                    .GroupBy(x => string.IsNullOrEmpty(x.Perfil?.MotivoPrestamo) ? "Sin motivo" : x.Perfil.MotivoPrestamo)
                    .OrderByDescending(x => x.Count())
                    .ToList();
                if (nombreReporte == "Mora")
                {
                    var mora = (
                        from c in _context.CUOTA
                        join s in _context.SOLICITUD_CREDITO on c.SOLICITUD_CREDITO_Id_Solicitud equals s.Id_Solicitud
                        join u in _context.Usuario on s.Usuario_Id_Usuario equals u.Id
                        where c.Estado == "Pendiente"
                              && c.FechaLimitePago.Date >= desde.Date
                              && c.FechaLimitePago.Date <= hasta.Date
                              && c.FechaLimitePago.Date < DateTime.Today
                        select new
                        {
                            Cliente = u.Nombre + " " + u.Apellido,
                            Dni = u.Dni,
                            Solicitud = s.NumeroSolicitud,
                            Monto = c.MontoCuota ?? 0,
                            FechaVencimiento = c.FechaLimitePago,
                            DiasMora = EF.Functions.DateDiffDay(c.FechaLimitePago, DateTime.Today)
                        }
                    ).ToList();

                    var pdfMora = Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4.Landscape());
                            page.Margin(25);

                            page.Header().Column(col =>
                            {
                                col.Item().Text("CrediPlus - Reporte de Mora")
                                    .FontSize(20).Bold().FontColor(Colors.Red.Darken2);

                                col.Item().Text($"Periodo: {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                            });

                            page.Content().PaddingTop(15).Column(col =>
                            {
                                if (!mora.Any())
                                {
                                    col.Item().Text("No hay datos de mora para este periodo.")
                                        .FontSize(13).Bold();
                                }
                                else
                                {
                                    col.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(c =>
                                        {
                                            c.RelativeColumn(2);
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                        });

                                        table.Header(h =>
                                        {
                                            h.Cell().Background(Colors.Red.Darken2).Padding(5).Text("Cliente").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Red.Darken2).Padding(5).Text("DNI").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Red.Darken2).Padding(5).Text("Solicitud").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Red.Darken2).Padding(5).Text("Monto").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Red.Darken2).Padding(5).Text("Vencimiento").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Red.Darken2).Padding(5).Text("Días Mora").FontColor(Colors.White).Bold();
                                        });

                                        foreach (var item in mora)
                                        {
                                            table.Cell().Padding(5).Text(item.Cliente);
                                            table.Cell().Padding(5).Text(item.Dni);
                                            table.Cell().Padding(5).Text(item.Solicitud ?? "");
                                            table.Cell().Padding(5).Text($"S/ {item.Monto:N2}");
                                            table.Cell().Padding(5).Text(item.FechaVencimiento.ToString("dd/MM/yyyy"));
                                            table.Cell().Padding(5).Text(item.DiasMora.ToString());
                                        }
                                    });
                                }
                            });

                            page.Footer().AlignCenter().Text("CrediPlus - Reporte generado automáticamente").FontSize(9);
                        });
                    }).GeneratePdf();

                    return File(pdfMora, "application/pdf", $"Reporte_Mora_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
                }
                if (nombreReporte == "Financiero")
                {
                    var pdfFinanciero = Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Margin(30);

                            page.Header().Text("CrediPlus - Reporte Financiero")
                                .FontSize(20).Bold().FontColor(Colors.Purple.Darken3);

                            page.Content().PaddingTop(15).Column(col =>
                            {
                                col.Item().Text($"Periodo: {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}");

                                col.Item().PaddingTop(12).Table(table =>
                                {
                                    table.ColumnsDefinition(c =>
                                    {
                                        c.RelativeColumn(2);
                                        c.RelativeColumn();
                                        c.RelativeColumn();
                                    });

                                    table.Header(h =>
                                    {
                                        h.Cell().Background(Colors.Purple.Darken3).Padding(6).Text("Indicador").FontColor(Colors.White).Bold();
                                        h.Cell().Background(Colors.Purple.Darken3).Padding(6).Text("Cantidad").FontColor(Colors.White).Bold();
                                        h.Cell().Background(Colors.Purple.Darken3).Padding(6).Text("Monto").FontColor(Colors.White).Bold();
                                    });

                                    table.Cell().Padding(6).Text("Solicitudes totales");
                                    table.Cell().Padding(6).Text(total.ToString());
                                    table.Cell().Padding(6).Text($"S/ {datos.Sum(x => x.Solicitud.MontoSolicitado):N2}");

                                    table.Cell().Padding(6).Text("Créditos aprobados");
                                    table.Cell().Padding(6).Text(aprobados.ToString());
                                    table.Cell().Padding(6).Text($"S/ {datos.Where(x => x.Solicitud.Estado == "Aprobado").Sum(x => x.Solicitud.MontoSolicitado):N2}");

                                    table.Cell().Padding(6).Text("Pendientes / En evaluación");
                                    table.Cell().Padding(6).Text(
        datos.Count(x => x.Solicitud.Estado == "Pendiente" || x.Solicitud.Estado == "En Evaluación").ToString()
    );
                                    table.Cell().Padding(6).Text($"S/ {datos.Where(x => x.Solicitud.Estado == "Pendiente" || x.Solicitud.Estado == "En Evaluación").Sum(x => x.Solicitud.MontoSolicitado):N2}");

                                    table.Cell().Padding(6).Text("Créditos rechazados");
                                    table.Cell().Padding(6).Text(rechazados.ToString());
                                    table.Cell().Padding(6).Text($"S/ {datos.Where(x => x.Solicitud.Estado == "Rechazado").Sum(x => x.Solicitud.MontoSolicitado):N2}");
                                });

                                if (!datos.Any())
                                    col.Item().PaddingTop(12).Text("No hay datos para este periodo.").Bold();
                            });
                        });
                    }).GeneratePdf();

                    return File(pdfFinanciero, "application/pdf", $"Reporte_Financiero_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
                }

                if (nombreReporte == "Sucursal")
                {
                    var datosSucursal = datos
                        .GroupBy(x => "Sucursal Principal")
                        .Select(g => new
                        {
                            Sucursal = g.Key,
                            Solicitudes = g.Count(),
                            Aprobados = g.Count(x => x.Solicitud.Estado == "Aprobado"),
                            Pendientes = g.Count(x => x.Solicitud.Estado == "Pendiente" || x.Solicitud.Estado == "En Evaluación"),
                            Rechazados = g.Count(x => x.Solicitud.Estado == "Rechazado"),
                            MontoAprobado = g.Where(x => x.Solicitud.Estado == "Aprobado").Sum(x => x.Solicitud.MontoSolicitado)
                        })
                        .ToList();

                    var pdfSucursal = Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4.Landscape());
                            page.Margin(25);

                            page.Header().Column(col =>
                            {
                                col.Item().Text("CrediPlus - Reporte por Sucursal")
                                    .FontSize(20).Bold().FontColor(Colors.Orange.Darken3);

                                col.Item().Text($"Periodo: {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                            });

                            page.Content().PaddingTop(15).Column(col =>
                            {
                                if (!datosSucursal.Any())
                                {
                                    col.Item().Text("No hay datos para este periodo.").FontSize(13).Bold();
                                }
                                else
                                {
                                    col.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(c =>
                                        {
                                            c.RelativeColumn(2);
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                        });

                                        table.Header(h =>
                                        {
                                            h.Cell().Background(Colors.Orange.Darken3).Padding(5).Text("Sucursal").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Orange.Darken3).Padding(5).Text("Solicitudes").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Orange.Darken3).Padding(5).Text("Aprobados").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Orange.Darken3).Padding(5).Text("Pendientes").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Orange.Darken3).Padding(5).Text("Rechazados").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Orange.Darken3).Padding(5).Text("Monto Aprobado").FontColor(Colors.White).Bold();
                                        });

                                        foreach (var item in datosSucursal)
                                        {
                                            table.Cell().Padding(5).Text(item.Sucursal);
                                            table.Cell().Padding(5).Text(item.Solicitudes.ToString());
                                            table.Cell().Padding(5).Text(item.Aprobados.ToString());
                                            table.Cell().Padding(5).Text(item.Pendientes.ToString());
                                            table.Cell().Padding(5).Text(item.Rechazados.ToString());
                                            table.Cell().Padding(5).Text($"S/ {item.MontoAprobado:N2}");
                                        }
                                    });
                                }
                            });

                            page.Footer().AlignCenter().Text("CrediPlus - Reporte generado automáticamente").FontSize(9);
                        });
                    }).GeneratePdf();

                    return File(pdfSucursal, "application/pdf", $"Reporte_Sucursal_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
                }
                if (nombreReporte == "TipoCredito")
                {
                    var datosTipo = datos
                        .GroupBy(x => string.IsNullOrEmpty(x.Perfil?.MotivoPrestamo) ? "Sin motivo" : x.Perfil.MotivoPrestamo)
                        .Select(g => new
                        {
                            Tipo = g.Key,
                            Solicitudes = g.Count(),
                            Aprobados = g.Count(x => x.Solicitud.Estado == "Aprobado"),
                            Pendientes = g.Count(x => x.Solicitud.Estado == "Pendiente" || x.Solicitud.Estado == "En Evaluación"),
                            Rechazados = g.Count(x => x.Solicitud.Estado == "Rechazado"),
                            MontoAprobado = g.Where(x => x.Solicitud.Estado == "Aprobado").Sum(x => x.Solicitud.MontoSolicitado)
                        })
                        .OrderByDescending(x => x.Solicitudes)
                        .ToList();

                    var pdfTipo = Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4.Landscape());
                            page.Margin(25);

                            page.Header().Column(col =>
                            {
                                col.Item().Text("CrediPlus - Reporte por Tipo de Crédito")
                                    .FontSize(20).Bold().FontColor(Colors.Pink.Darken2);

                                col.Item().Text($"Periodo: {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                            });

                            page.Content().PaddingTop(15).Column(col =>
                            {
                                if (!datosTipo.Any())
                                {
                                    col.Item().Text("No hay datos para este periodo.").FontSize(13).Bold();
                                }
                                else
                                {
                                    col.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(c =>
                                        {
                                            c.RelativeColumn(2);
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                        });

                                        table.Header(h =>
                                        {
                                            h.Cell().Background(Colors.Pink.Darken2).Padding(5).Text("Tipo").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Pink.Darken2).Padding(5).Text("Solicitudes").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Pink.Darken2).Padding(5).Text("Aprobados").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Pink.Darken2).Padding(5).Text("Pendientes").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Pink.Darken2).Padding(5).Text("Rechazados").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Pink.Darken2).Padding(5).Text("Monto Aprobado").FontColor(Colors.White).Bold();
                                        });

                                        foreach (var item in datosTipo)
                                        {
                                            table.Cell().Padding(5).Text(item.Tipo);
                                            table.Cell().Padding(5).Text(item.Solicitudes.ToString());
                                            table.Cell().Padding(5).Text(item.Aprobados.ToString());
                                            table.Cell().Padding(5).Text(item.Pendientes.ToString());
                                            table.Cell().Padding(5).Text(item.Rechazados.ToString());
                                            table.Cell().Padding(5).Text($"S/ {item.MontoAprobado:N2}");
                                        }
                                    });
                                }
                            });

                            page.Footer().AlignCenter().Text("CrediPlus - Reporte generado automáticamente").FontSize(9);
                        });
                    }).GeneratePdf();

                    return File(pdfTipo, "application/pdf", $"Reporte_TipoCredito_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
                }
                if (nombreReporte == "Periodo")
                {
                    var datosPeriodo = datos
                        .GroupBy(x => new { x.Solicitud.FechaSolicitud.Year, x.Solicitud.FechaSolicitud.Month })
                        .Select(g => new
                        {
                            Anio = g.Key.Year,
                            Mes = g.Key.Month,
                            Solicitudes = g.Count(),
                            Aprobadas = g.Count(x => x.Solicitud.Estado == "Aprobado"),
                            Pendientes = g.Count(x => x.Solicitud.Estado == "Pendiente" || x.Solicitud.Estado == "En Evaluación"),
                            Rechazadas = g.Count(x => x.Solicitud.Estado == "Rechazado"),
                            MontoAprobado = g.Where(x => x.Solicitud.Estado == "Aprobado").Sum(x => x.Solicitud.MontoSolicitado)
                        })
                        .OrderBy(x => x.Anio)
                        .ThenBy(x => x.Mes)
                        .ToList();

                    var pdfPeriodo = Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4.Landscape());
                            page.Margin(25);

                            page.Header().Column(col =>
                            {
                                col.Item().Text("CrediPlus - Reporte por Periodo")
                                    .FontSize(20).Bold().FontColor(Colors.Blue.Darken2);

                                col.Item().Text($"Periodo: {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                            });

                            page.Content().PaddingTop(15).Column(col =>
                            {
                                if (!datosPeriodo.Any())
                                {
                                    col.Item().Text("No hay datos para este periodo.").FontSize(13).Bold();
                                }
                                else
                                {
                                    col.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(c =>
                                        {
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                        });

                                        table.Header(h =>
                                        {
                                            h.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Periodo").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Solicitudes").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Aprobadas").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Pendientes").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Rechazadas").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Monto Aprobado").FontColor(Colors.White).Bold();
                                        });

                                        foreach (var item in datosPeriodo)
                                        {
                                            table.Cell().Padding(5).Text($"{item.Mes:00}/{item.Anio}");
                                            table.Cell().Padding(5).Text(item.Solicitudes.ToString());
                                            table.Cell().Padding(5).Text(item.Aprobadas.ToString());
                                            table.Cell().Padding(5).Text(item.Pendientes.ToString());
                                            table.Cell().Padding(5).Text(item.Rechazadas.ToString());
                                            table.Cell().Padding(5).Text($"S/ {item.MontoAprobado:N2}");
                                        }
                                    });
                                }
                            });

                            page.Footer().AlignCenter().Text("CrediPlus - Reporte generado automáticamente").FontSize(9);
                        });
                    }).GeneratePdf();

                    return File(pdfPeriodo, "application/pdf", $"Reporte_Periodo_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
                }
                if (nombreReporte == "Actividad")
                {
                    var actividades = _context.ACTIVIDAD_ADMINISTRADOR
                        .Where(x => x.Fecha.Date >= desde.Date && x.Fecha.Date <= hasta.Date)
                        .OrderByDescending(x => x.Fecha)
                        .ToList();

                    var pdfActividad = Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4.Landscape());
                            page.Margin(25);

                            page.Header().Column(col =>
                            {
                                col.Item().Text("CrediPlus - Reporte de Actividad")
                                    .FontSize(20).Bold().FontColor(Colors.Indigo.Darken2);

                                col.Item().Text($"Periodo: {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                            });

                            page.Content().PaddingTop(15).Column(col =>
                            {
                                if (!actividades.Any())
                                {
                                    col.Item().Text("No hay actividades para este periodo.").FontSize(13).Bold();
                                }
                                else
                                {
                                    col.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(c =>
                                        {
                                            c.RelativeColumn();
                                            c.RelativeColumn(3);
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                        });

                                        table.Header(h =>
                                        {
                                            h.Cell().Background(Colors.Indigo.Darken2).Padding(5).Text("Tipo").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Indigo.Darken2).Padding(5).Text("Descripción").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Indigo.Darken2).Padding(5).Text("Fecha").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Indigo.Darken2).Padding(5).Text("Hora").FontColor(Colors.White).Bold();
                                        });

                                        foreach (var item in actividades)
                                        {
                                            table.Cell().Padding(5).Text(item.Tipo ?? "");
                                            table.Cell().Padding(5).Text(item.Descripcion ?? "");
                                            table.Cell().Padding(5).Text(item.Fecha.ToString("dd/MM/yyyy"));
                                            table.Cell().Padding(5).Text(item.Fecha.ToString("hh:mm tt"));
                                        }
                                    });
                                }
                            });

                            page.Footer().AlignCenter().Text("CrediPlus - Reporte generado automáticamente").FontSize(9);
                        });
                    }).GeneratePdf();

                    return File(pdfActividad, "application/pdf", $"Reporte_Actividad_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
                }
                if (nombreReporte == "Exportaciones")
                {
                    var exportaciones = _context.REPORTE_GENERADO
                        .Where(x => x.FechaSolicitud.Date >= desde.Date && x.FechaSolicitud.Date <= hasta.Date)
                        .OrderByDescending(x => x.FechaSolicitud)
                        .ToList();

                    var pdfExportaciones = Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4.Landscape());
                            page.Margin(25);

                            page.Header().Column(col =>
                            {
                                col.Item().Text("CrediPlus - Reporte de Exportaciones")
                                    .FontSize(20).Bold().FontColor(Colors.Green.Darken3);

                                col.Item().Text($"Periodo: {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                            });

                            page.Content().PaddingTop(15).Column(col =>
                            {
                                if (!exportaciones.Any())
                                {
                                    col.Item().Text("No hay exportaciones para este periodo.").FontSize(13).Bold();
                                }
                                else
                                {
                                    col.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(c =>
                                        {
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                            c.RelativeColumn(2);
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                            c.RelativeColumn();
                                        });

                                        table.Header(h =>
                                        {
                                            h.Cell().Background(Colors.Green.Darken3).Padding(5).Text("ID").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Green.Darken3).Padding(5).Text("Tipo").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Green.Darken3).Padding(5).Text("Descripción").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Green.Darken3).Padding(5).Text("Formato").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Green.Darken3).Padding(5).Text("Fecha").FontColor(Colors.White).Bold();
                                            h.Cell().Background(Colors.Green.Darken3).Padding(5).Text("Estado").FontColor(Colors.White).Bold();
                                        });

                                        foreach (var item in exportaciones)
                                        {
                                            table.Cell().Padding(5).Text($"EXP-{item.Id:00000}");
                                            table.Cell().Padding(5).Text(item.TipoReporte ?? "");
                                            table.Cell().Padding(5).Text(item.Descripcion ?? "");
                                            table.Cell().Padding(5).Text(item.Formato ?? "");
                                            table.Cell().Padding(5).Text(item.FechaSolicitud.ToString("dd/MM/yyyy"));
                                            table.Cell().Padding(5).Text(item.Estado ?? "");
                                        }
                                    });
                                }
                            });

                            page.Footer().AlignCenter()
                                .Text("CrediPlus - Reporte generado automáticamente")
                                .FontSize(9);
                        });
                    }).GeneratePdf();

                    return File(pdfExportaciones, "application/pdf", $"Reporte_Exportaciones_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
                }
                if (nombreReporte == "Personalizado")
                {
                    var pdfPersonalizado = Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4.Landscape());
                            page.Margin(25);

                            page.Header().Column(col =>
                            {
                                col.Item().Text("CrediPlus - Reporte Personalizado")
                                    .FontSize(20)
                                    .Bold()
                                    .FontColor(Colors.Grey.Darken3);

                                col.Item().Text($"Periodo: {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Darken1);
                            });

                            page.Content().PaddingTop(15).Column(col =>
                            {
                                if (!datos.Any())
                                {
                                    col.Item().Text("No existen registros para el periodo seleccionado.")
                                        .FontSize(13)
                                        .Bold();
                                }
                                else
                                {
                                    col.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Background(Colors.Grey.Darken3).Padding(5).Text("Cliente").FontColor(Colors.White).Bold();
                                            header.Cell().Background(Colors.Grey.Darken3).Padding(5).Text("DNI").FontColor(Colors.White).Bold();
                                            header.Cell().Background(Colors.Grey.Darken3).Padding(5).Text("Estado").FontColor(Colors.White).Bold();
                                            header.Cell().Background(Colors.Grey.Darken3).Padding(5).Text("Tipo Crédito").FontColor(Colors.White).Bold();
                                            header.Cell().Background(Colors.Grey.Darken3).Padding(5).Text("Monto").FontColor(Colors.White).Bold();
                                            header.Cell().Background(Colors.Grey.Darken3).Padding(5).Text("Fecha").FontColor(Colors.White).Bold();
                                        });

                                        foreach (var item in datos)
                                        {
                                            table.Cell().Padding(5).Text(item.Solicitud.USUARIO?.Nombre + " " + item.Solicitud.USUARIO?.Apellido);
                                            table.Cell().Padding(5).Text(item.Solicitud.USUARIO?.Dni);
                                            table.Cell().Padding(5).Text(item.Solicitud.Estado);
                                            table.Cell().Padding(5).Text(item.Perfil?.MotivoPrestamo ?? "-");
                                            table.Cell().Padding(5).Text($"S/ {item.Solicitud.MontoSolicitado:N2}");
                                            table.Cell().Padding(5).Text(item.Solicitud.FechaSolicitud.ToString("dd/MM/yyyy"));
                                        }
                                    });
                                }
                            });

                            page.Footer()
                                .AlignCenter()
                                .Text("CrediPlus - Reporte generado automáticamente")
                                .FontSize(9);
                        });
                    }).GeneratePdf();

                    return File(
                        pdfPersonalizado,
                        "application/pdf",
                        $"Reporte_Personalizado_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
                }

                var pdf = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(25);

                        page.Header().Column(col =>
                        {
                            col.Item().Text("CrediPlus - Reporte de Administración")
                                .FontSize(20)
                                .Bold()
                                .FontColor(Colors.Purple.Darken3);

                            col.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy hh:mm tt}")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken1);

                            col.Item().Text($"Filtros: {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy} | Motivo: {tipoCreditoFiltro} | Estado: {estadoFiltro}")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken2);
                        });

                        page.Content().PaddingVertical(15).Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                    .Text($"Solicitudes\n{total}").FontSize(13).Bold();

                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                    .Text($"Aprobados\n{aprobados}").FontSize(13).Bold();

                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                    .Text($"Rechazados\n{rechazados}").FontSize(13).Bold();

                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                    .Text($"Monto\nS/ {monto:N0}").FontSize(13).Bold();
                            });

                            col.Item().PaddingTop(14).Text("Resumen por Mes")
                                .FontSize(14).Bold().FontColor(Colors.Purple.Darken3);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("Mes").Bold();
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("Solicitudes").Bold();
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("Aprobados").Bold();
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("Rechazados").Bold();
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("Monto").Bold();
                                });

                                foreach (var g in porMes)
                                {
                                    table.Cell().Padding(5).Text($"{g.Key.Month}/{g.Key.Year}");
                                    table.Cell().Padding(5).Text(g.Count().ToString());
                                    table.Cell().Padding(5).Text(g.Count(x => x.Solicitud.Estado == "Aprobado").ToString());
                                    table.Cell().Padding(5).Text(g.Count(x => x.Solicitud.Estado == "Rechazado").ToString());
                                    table.Cell().Padding(5).Text($"S/ {g.Where(x => x.Solicitud.Estado == "Aprobado").Sum(x => x.Solicitud.MontoSolicitado):N0}");
                                }
                            });

                            col.Item().PaddingTop(14).Text("Créditos por Tipo / Motivo")
                                .FontSize(14).Bold().FontColor(Colors.Purple.Darken3);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("Motivo").Bold();
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("Cantidad").Bold();
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("Porcentaje").Bold();
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("Monto").Bold();
                                });

                                foreach (var g in porTipo)
                                {
                                    table.Cell().Padding(5).Text(g.Key);
                                    table.Cell().Padding(5).Text(g.Count().ToString());
                                    table.Cell().Padding(5).Text(total > 0 ? $"{Math.Round((double)g.Count() * 100 / total, 1)}%" : "0%");
                                    table.Cell().Padding(5).Text($"S/ {g.Sum(x => x.Solicitud.MontoSolicitado):N0}");
                                }
                            });

                            col.Item().PaddingTop(14).Text("Detalle por Usuario")
                                .FontSize(14).Bold().FontColor(Colors.Purple.Darken3);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn();
                                    c.RelativeColumn(2);
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("Solicitud").Bold();
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("Cliente").Bold();
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("DNI").Bold();
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("Motivo").Bold();
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("Monto").Bold();
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("Estado").Bold();
                                    h.Cell().Background(Colors.Purple.Lighten4).Padding(5).Text("Fecha").Bold();
                                });

                                foreach (var item in datos)
                                {
                                    table.Cell().Padding(5).Text(item.Solicitud.NumeroSolicitud ?? "");
                                    table.Cell().Padding(5).Text($"{item.Solicitud.USUARIO?.Nombre} {item.Solicitud.USUARIO?.Apellido}");
                                    table.Cell().Padding(5).Text(item.Solicitud.USUARIO?.Dni ?? "");
                                    table.Cell().Padding(5).Text(item.Perfil?.MotivoPrestamo ?? "Sin motivo");
                                    table.Cell().Padding(5).Text($"S/ {item.Solicitud.MontoSolicitado:N0}");
                                    table.Cell().Padding(5).Text(item.Solicitud.Estado);
                                    table.Cell().Padding(5).Text(item.Solicitud.FechaSolicitud.ToString("dd/MM/yyyy"));
                                }
                            });
                        

                        });

                        page.Footer().AlignCenter()
                            .Text("CrediPlus - Reporte generado automáticamente")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                    });
                }).GeneratePdf();

                return File(pdf, "application/pdf", $"Reporte_{nombreReporte}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            }
            public IActionResult Pagos(
        string busqueda = "",
        string estadoFiltro = "Todos",
        string metodoFiltro = "Todos",
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null)
            {
                ViewData["ActivePage"] = "Pagos";
                CargarDatosAdministrador();

                DateTime desde = fechaInicio ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime hasta = fechaFin ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month));

                var inicioMes = desde;
                var finMes = hasta.AddDays(1);

                var query =
                    from p in _context.PAGO_CUOTA
                    join c in _context.CUOTA on p.Id_Cuota equals c.Id_Cuota
                    join s in _context.SOLICITUD_CREDITO on c.SOLICITUD_CREDITO_Id_Solicitud equals s.Id_Solicitud
                    join u in _context.Usuario on s.Usuario_Id_Usuario equals u.Id
                    select new
                    {
                        Pago = p,
                        Cuota = c,
                        Solicitud = s,
                        Usuario = u
                    };

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    query = query.Where(x =>
                        x.Pago.CodigoOperacion.Contains(busqueda) ||
                        x.Usuario.Nombre.Contains(busqueda) ||
                        x.Usuario.Apellido.Contains(busqueda) ||
                        x.Usuario.Dni.Contains(busqueda));
                }

                if (estadoFiltro != "Todos")
                    query = query.Where(x => x.Pago.Estado == estadoFiltro);

                if (metodoFiltro != "Todos")
                    query = query.Where(x => x.Pago.MetodoPago == metodoFiltro);

                var pagos = query
                    .OrderByDescending(x => x.Pago.FechaPago)
                    .Select(x => new PagoAdminItemViewModel
                    {
                        IdPagoCuota = x.Pago.Id_PagoCuota,
                        IdCuota = x.Pago.Id_Cuota,
                        IdSolicitud = x.Solicitud.Id_Solicitud,
                        Cliente = x.Usuario.Nombre + " " + x.Usuario.Apellido,
                        Dni = x.Usuario.Dni,
                        Credito = "#C" + x.Solicitud.Id_Solicitud.ToString("000"),
                        Monto = x.Pago.MontoPagado,
                        FechaPago = x.Pago.FechaPago,
                        MetodoPago = x.Pago.MetodoPago,
                        Estado = x.Pago.Estado,
                        Referencia = x.Pago.CodigoOperacion,
                        EntidadPago = x.Pago.EntidadPago
                    })
                    .ToList();

                var pagosMes = pagos
                    .Where(x => x.FechaPago >= inicioMes && x.FechaPago < finMes)
                    .ToList();
                var pagosCompletadosMes = pagosMes
        .Where(x => x.Estado == "Aprobado" || x.Estado == "Completado")
        .ToList();

                var cuotasPendientesMes = _context.CUOTA
        .Where(c =>
            c.Estado == "Pendiente" &&
            c.FechaLimitePago >= inicioMes &&
            c.FechaLimitePago < finMes)
        .ToList();

                int totalPendientesMes = cuotasPendientesMes
                    .Select(x => x.SOLICITUD_CREDITO_Id_Solicitud)
                    .Distinct()
                    .Count();

                decimal montoRecaudadoMes = pagosCompletadosMes.Sum(x => x.Monto);
                var cuotasPendientes = (
        from c in _context.CUOTA
        join s in _context.SOLICITUD_CREDITO
            on c.SOLICITUD_CREDITO_Id_Solicitud equals s.Id_Solicitud
        join u in _context.Usuario
            on s.Usuario_Id_Usuario equals u.Id
        where c.Estado == "Pendiente" &&
              c.FechaLimitePago.Date >= desde.Date &&
              c.FechaLimitePago.Date <= hasta.Date

        group c by new
        {
            s.Id_Solicitud,
            u.Nombre,
            u.Apellido,
            u.Dni
        } into g

        select new PagoAdminItemViewModel
        {
            IdPagoCuota = 0,
            IdSolicitud = g.Key.Id_Solicitud,
            Cliente = g.Key.Nombre + " " + g.Key.Apellido,
            Dni = g.Key.Dni,
            Credito = "#C" + g.Key.Id_Solicitud.ToString("000"),

            Monto = g.Sum(x => x.MontoCuota ?? 0),

            FechaPago = g.Min(x => x.FechaLimitePago),

            MetodoPago = "Sin pago",

            Estado = g.Min(x => x.FechaLimitePago.Date) < DateTime.Today
        ? "Mora"
        : "Pendiente",

            Referencia = "",

            EntidadPago = ""
        }
    ).ToList();

                var modelo = new AdminPagosViewModel
                {
                    Busqueda = busqueda,
                    EstadoFiltro = estadoFiltro,
                    MetodoFiltro = metodoFiltro,
                    Pagos = pagos,
                    CuotasPendientes = cuotasPendientes,
                    TotalPagosMes = pagosCompletadosMes.Count + totalPendientesMes,
                    PagosCompletadosMes = pagosCompletadosMes.Count,
                    PagosPendientesMes = totalPendientesMes,
                    MontoTotalRecaudadoMes = montoRecaudadoMes
                };

                return View(modelo);
            }
            public IActionResult PagosExcel(string busqueda = "", string estadoFiltro = "Todos", string metodoFiltro = "Todos", DateTime? fechaInicio = null, DateTime? fechaFin = null)
            {
                DateTime desde = fechaInicio ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime hasta = fechaFin ?? new DateTime(
                    DateTime.Now.Year,
                    DateTime.Now.Month,
                    DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month)
                );

                var inicioMes = desde;
                var finMes = hasta.AddDays(1);
                var hoy = DateTime.Today;

                var pagosCompletados = (
                    from p in _context.PAGO_CUOTA
                    join c in _context.CUOTA on p.Id_Cuota equals c.Id_Cuota
                    join s in _context.SOLICITUD_CREDITO on c.SOLICITUD_CREDITO_Id_Solicitud equals s.Id_Solicitud
                    join u in _context.Usuario on s.Usuario_Id_Usuario equals u.Id
                    where p.FechaPago.Date >= desde.Date &&
          p.FechaPago.Date <= hasta.Date
                    select new PagoAdminItemViewModel
                    {
                        IdPagoCuota = p.Id_PagoCuota,
                        IdSolicitud = s.Id_Solicitud,
                        Cliente = u.Nombre + " " + u.Apellido,
                        Dni = u.Dni,
                        Credito = "#C" + s.Id_Solicitud.ToString("000"),
                        Monto = p.MontoPagado,
                        FechaPago = p.FechaPago,
                        MetodoPago = p.MetodoPago,
                        Estado = p.Estado == "Aprobado" ? "Completado" : p.Estado,
                        Referencia = p.CodigoOperacion ?? "---",
                        EntidadPago = p.EntidadPago ?? ""
                    }
                ).ToList();

                var cuotasPendientes = (
                    from c in _context.CUOTA
                    join s in _context.SOLICITUD_CREDITO on c.SOLICITUD_CREDITO_Id_Solicitud equals s.Id_Solicitud
                    join u in _context.Usuario on s.Usuario_Id_Usuario equals u.Id
                    where c.Estado == "Pendiente"
                    group c by new
                    {
                        s.Id_Solicitud,
                        u.Nombre,
                        u.Apellido,
                        u.Dni
                    } into g
                    select new PagoAdminItemViewModel
                    {
                        IdPagoCuota = 0,
                        IdSolicitud = g.Key.Id_Solicitud,
                        Cliente = g.Key.Nombre + " " + g.Key.Apellido,
                        Dni = g.Key.Dni,
                        Credito = "#C" + g.Key.Id_Solicitud.ToString("000"),
                        Monto = g.Sum(x => x.MontoCuota ?? 0),
                        FechaPago = g.Min(x => x.FechaLimitePago),
                        MetodoPago = "Sin pago",
                        Estado = g.Min(x => x.FechaLimitePago.Date) < hoy ? "Mora" : "Pendiente",
                        Referencia = "---",
                        EntidadPago = ""
                    }
                ).ToList();

                var reporte = new List<PagoAdminItemViewModel>();
                reporte.AddRange(pagosCompletados);
                reporte.AddRange(cuotasPendientes);

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    reporte = reporte.Where(x =>
                        x.Cliente.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                        x.Dni.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                        x.Referencia.Contains(busqueda, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }

                if (estadoFiltro != "Todos")
                {
                    reporte = reporte.Where(x => x.Estado == estadoFiltro).ToList();
                }

                if (metodoFiltro != "Todos")
                {
                    reporte = reporte.Where(x => x.MetodoPago == metodoFiltro).ToList();
                }

                var ordenado = reporte.OrderByDescending(x => x.FechaPago).ToList();
                var resumenMes = ordenado
        .Where(x => x.FechaPago >= inicioMes && x.FechaPago < finMes)
        .ToList();


                int completados = resumenMes.Count(x => x.Estado == "Completado");

                int pendientes = resumenMes.Count(x =>
        x.Estado.Contains("Pendiente"));

                int mora = resumenMes.Count(x => x.Estado == "Mora");

                int total = resumenMes.Count;

                decimal recaudado = resumenMes
                    .Where(x => x.Estado == "Completado")
                    .Sum(x => x.Monto);
                int yape = resumenMes.Count(x =>
                    x.MetodoPago.Contains("Yape"));

                int plin = resumenMes.Count(x =>
                    x.MetodoPago.Contains("Plin"));

                int transferencia = resumenMes.Count(x =>
                    x.MetodoPago.Contains("Transferencia"));

                int sinPago = resumenMes.Count(x =>
                    x.MetodoPago.Contains("Sin pago"));

                using var workbook = new XLWorkbook();
                var hoja = workbook.Worksheets.Add("Reporte Pagos");

                hoja.Cell("A1").Value = "CREDIPLUS - REPORTE DE PAGOS";
                hoja.Range("A1:I1").Merge();
                hoja.Range("A1:I1").Style.Font.Bold = true;
                hoja.Range("A1:I1").Style.Font.FontSize = 18;
                hoja.Range("A1:I1").Style.Font.FontColor = XLColor.White;
                hoja.Range("A1:I1").Style.Fill.BackgroundColor = XLColor.FromHtml("#4c1d95");
                hoja.Range("A1:I1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                hoja.Cell("A3").Value = "Generado:";
                hoja.Cell("B3").Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                hoja.Cell("A4").Value = "Reporte:";
                hoja.Cell("B4").Value = $"Detalle general | Resumen del mes: {DateTime.Now:MMMM yyyy}";
                hoja.Cell("A5").Value = "Filtros:";
                hoja.Cell("B5").Value = $"Estado: {estadoFiltro} | Método: {metodoFiltro}";

                hoja.Cell("A7").Value = "Total pagos";
                hoja.Cell("C7").Value = "Completados";
                hoja.Cell("E7").Value = "Pendientes";
                hoja.Cell("G7").Value = "Mora";
                hoja.Cell("I7").Value = "Monto recaudado";

                hoja.Cell("A8").Value = total;
                hoja.Cell("C8").Value = completados;
                hoja.Cell("E8").Value = pendientes;
                hoja.Cell("G8").Value = mora;
                hoja.Cell("I8").Value = recaudado;
                hoja.Cell("I8").Style.NumberFormat.Format = "\"S/\" #,##0.00";

                hoja.Range("A7:I8").Style.Font.Bold = true;
                hoja.Range("A7:I8").Style.Fill.BackgroundColor = XLColor.FromHtml("#ede9fe");
                hoja.Range("A7:I8").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                hoja.Range("A7:I8").Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                hoja.Cell("A11").Value = "ID";
                hoja.Cell("B11").Value = "Cliente";
                hoja.Cell("C11").Value = "DNI";
                hoja.Cell("D11").Value = "Crédito";
                hoja.Cell("E11").Value = "Monto";
                hoja.Cell("F11").Value = "Fecha";
                hoja.Cell("G11").Value = "Método";
                hoja.Cell("H11").Value = "Estado";
                hoja.Cell("I11").Value = "Referencia";

                hoja.Range("A11:I11").Style.Font.Bold = true;
                hoja.Range("A11:I11").Style.Font.FontColor = XLColor.White;
                hoja.Range("A11:I11").Style.Fill.BackgroundColor = XLColor.FromHtml("#6d28d9");

                int fila = 12;

                foreach (var item in ordenado)
                {
                    hoja.Cell(fila, 1).Value = item.IdPagoCuota == 0 ? "#P---" : "#P" + item.IdPagoCuota.ToString("000");
                    hoja.Cell(fila, 2).Value = item.Cliente;
                    hoja.Cell(fila, 3).Value = item.Dni;
                    hoja.Cell(fila, 4).Value = item.Credito;
                    hoja.Cell(fila, 5).Value = item.Monto;
                    hoja.Cell(fila, 6).Value = item.FechaPago.ToString("dd/MM/yyyy");
                    hoja.Cell(fila, 7).Value = item.MetodoPago;
                    hoja.Cell(fila, 8).Value = item.Estado;
                    hoja.Cell(fila, 9).Value = item.Referencia;

                    if (item.Estado == "Completado")
                        hoja.Cell(fila, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#dcfce7");

                    if (item.Estado == "Pendiente")
                        hoja.Cell(fila, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#fef3c7");

                    if (item.Estado == "Mora")
                        hoja.Cell(fila, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#fee2e2");

                    fila++;
                }

                hoja.Column(5).Style.NumberFormat.Format = "\"S/\" #,##0.00";


                int resumenFila = fila + 2;
                // Las cantidades NO son dinero
                hoja.Cell("E8").Style.NumberFormat.Format = "0";

                hoja.Cell(resumenFila + 1, 5).Style.NumberFormat.Format = "0"; // Yape
                hoja.Cell(resumenFila + 2, 5).Style.NumberFormat.Format = "0"; // Plin
                hoja.Cell(resumenFila + 3, 5).Style.NumberFormat.Format = "0"; // Transferencia
                hoja.Cell(resumenFila + 4, 5).Style.NumberFormat.Format = "0"; // Sin pago

                hoja.Cell(resumenFila, 1).Value = "RESUMEN POR ESTADO";
                hoja.Cell(resumenFila, 4).Value = "MÉTODOS DE PAGO";
                hoja.Range(resumenFila, 1, resumenFila, 2).Merge();
                hoja.Range(resumenFila, 4, resumenFila, 5).Merge();

                hoja.Range(resumenFila, 1, resumenFila, 2).Style.Font.Bold = true;
                hoja.Range(resumenFila, 4, resumenFila, 5).Style.Font.Bold = true;
                hoja.Range(resumenFila, 1, resumenFila, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#ede9fe");

                hoja.Cell(resumenFila + 1, 1).Value = "Completados";
                hoja.Cell(resumenFila + 1, 2).Value = completados;

                hoja.Cell(resumenFila + 2, 1).Value = "Pendientes";
                hoja.Cell(resumenFila + 2, 2).Value = pendientes;

                hoja.Cell(resumenFila + 3, 1).Value = "Mora";
                hoja.Cell(resumenFila + 3, 2).Value = mora;

                hoja.Cell(resumenFila + 4, 1).Value = "Total";
                hoja.Cell(resumenFila + 4, 2).Value = total;

                hoja.Cell(resumenFila + 1, 4).Value = "Yape";
                hoja.Cell(resumenFila + 1, 5).Value = yape;
                hoja.Cell(resumenFila + 1, 4).Value = "Yape";
                hoja.Cell(resumenFila + 1, 5).Value = yape;

                hoja.Cell(resumenFila + 2, 4).Value = "Plin";
                hoja.Cell(resumenFila + 2, 5).Value = plin;

                hoja.Cell(resumenFila + 3, 4).Value = "Transferencia";
                hoja.Cell(resumenFila + 3, 5).Value = transferencia;

                hoja.Cell(resumenFila + 4, 4).Value = "Sin pago";
                hoja.Cell(resumenFila + 4, 5).Value = sinPago;

                hoja.Cell(resumenFila + 2, 4).Value = "Plin";
                hoja.Cell(resumenFila + 2, 5).Value = plin;

                hoja.Cell(resumenFila + 3, 4).Value = "Transferencia";
                hoja.Cell(resumenFila + 3, 5).Value = transferencia;

                hoja.Cell(resumenFila + 4, 4).Value = "Sin pago";
                hoja.Cell(resumenFila + 4, 5).Value = sinPago;

                var rango = hoja.RangeUsed();

                if (rango != null)
                {
                    rango.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    rango.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    rango.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }

                hoja.Columns().AdjustToContents();
                hoja.Rows().AdjustToContents();
                hoja.SheetView.FreezeRows(11);

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Reporte_Pagos_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                );
            }

            public IActionResult PagosPdf(string busqueda = "", string estadoFiltro = "Todos", string metodoFiltro = "Todos", DateTime? fechaInicio = null, DateTime? fechaFin = null)
            {
                QuestPDF.Settings.License = LicenseType.Community;

                DateTime desde = fechaInicio ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime hasta = fechaFin ?? new DateTime(
                    DateTime.Now.Year,
                    DateTime.Now.Month,
                    DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month)
                );

                var datos = (
                    from p in _context.PAGO_CUOTA
                    join c in _context.CUOTA on p.Id_Cuota equals c.Id_Cuota
                    join s in _context.SOLICITUD_CREDITO on c.SOLICITUD_CREDITO_Id_Solicitud equals s.Id_Solicitud
                    join u in _context.Usuario on s.Usuario_Id_Usuario equals u.Id
                    where p.FechaPago.Date >= desde.Date && p.FechaPago.Date <= hasta.Date
                    select new PagoAdminItemViewModel
                    {
                        IdPagoCuota = p.Id_PagoCuota,
                        IdCuota = p.Id_Cuota,
                        IdSolicitud = s.Id_Solicitud,
                        Cliente = u.Nombre + " " + u.Apellido,
                        Dni = u.Dni,
                        Credito = "#C" + s.Id_Solicitud.ToString("000"),
                        Monto = p.MontoPagado,
                        FechaPago = p.FechaPago,
                        MetodoPago = p.MetodoPago,
                        Estado = p.Estado,
                        Referencia = p.CodigoOperacion ?? "---",
                        EntidadPago = p.EntidadPago ?? ""
                    }).ToList();

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    string texto = busqueda.Trim().ToLower();

                    datos = datos.Where(x =>
                        x.Cliente.ToLower().Contains(texto) ||
                        x.Dni.Contains(texto) ||
                        x.Credito.ToLower().Contains(texto) ||
                        x.Referencia.ToLower().Contains(texto)
                    ).ToList();
                }

                if (estadoFiltro != "Todos")
                    datos = datos.Where(x => x.Estado == estadoFiltro).ToList();

                if (metodoFiltro != "Todos")
                    datos = datos.Where(x => x.MetodoPago == metodoFiltro).ToList();

                var completados = datos
                    .Where(x => x.Estado == "Aprobado" || x.Estado == "Completado")
                    .ToList();

                var pendientes = (
                    from c in _context.CUOTA
                    join s in _context.SOLICITUD_CREDITO on c.SOLICITUD_CREDITO_Id_Solicitud equals s.Id_Solicitud
                    join u in _context.Usuario on s.Usuario_Id_Usuario equals u.Id
                    where c.Estado == "Pendiente"
                          && c.FechaLimitePago.Date >= desde.Date
                          && c.FechaLimitePago.Date <= hasta.Date
                    group c by new { s.Id_Solicitud, u.Nombre, u.Apellido, u.Dni } into g
                    select new PagoAdminItemViewModel
                    {
                        IdPagoCuota = 0,
                        IdSolicitud = g.Key.Id_Solicitud,
                        Cliente = g.Key.Nombre + " " + g.Key.Apellido,
                        Dni = g.Key.Dni,
                        Credito = "#C" + g.Key.Id_Solicitud.ToString("000"),
                        Monto = g.Sum(x => x.MontoCuota ?? 0),
                        FechaPago = g.Min(x => x.FechaLimitePago),
                        MetodoPago = "Sin pago",
                        Estado = g.Min(x => x.FechaLimitePago) < DateTime.Today ? "Mora Pendiente" : "Pendiente",
                        Referencia = "---"
                    }).ToList();

                var reporte = new List<PagoAdminItemViewModel>();
                reporte.AddRange(completados);
                reporte.AddRange(pendientes);

                decimal montoRecaudado = completados.Sum(x => x.Monto);

                var pdf = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(25);

                        page.Header().Text("CrediPlus - Reporte de Pagos")
                            .FontSize(22).Bold().FontColor(Colors.Purple.Darken3);

                        page.Content().Column(col =>
                        {
                            col.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}");
                            col.Item().Text($"Periodo: {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}");

                            col.Item().PaddingVertical(12).Row(row =>
                            {
                                row.RelativeItem().Border(1).Padding(8).Text($"Total pagos\n{reporte.Count}").Bold();
                                row.RelativeItem().Border(1).Padding(8).Text($"Completados\n{completados.Count}").Bold();
                                row.RelativeItem().Border(1).Padding(8).Text($"Pendientes / Mora\n{pendientes.Count}").Bold();
                                row.RelativeItem().Border(1).Padding(8).Text($"Monto recaudado\nS/ {montoRecaudado:N2}").Bold();
                            });

                            if (!reporte.Any())
                            {
                                col.Item().Text("No hay pagos registrados para el período seleccionado.")
                                    .FontSize(13)
                                    .Bold();
                            }
                            else
                            {
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(c =>
                                    {
                                        c.RelativeColumn(1);
                                        c.RelativeColumn(2);
                                        c.RelativeColumn(1);
                                        c.RelativeColumn(1);
                                        c.RelativeColumn(2);
                                        c.RelativeColumn(2);
                                        c.RelativeColumn(1);
                                        c.RelativeColumn(2);
                                    });

                                    string[] headers = { "ID", "Cliente", "Crédito", "Monto", "Fecha", "Método", "Estado", "Referencia" };

                                    table.Header(h =>
                                    {
                                        foreach (var x in headers)
                                        {
                                            h.Cell().Background(Colors.Purple.Darken3).Padding(5)
                                                .Text(x).FontColor(Colors.White).Bold().FontSize(8);
                                        }
                                    });

                                    foreach (var p in reporte.OrderByDescending(x => x.FechaPago))
                                    {
                                        table.Cell().Padding(5).Text(p.IdPagoCuota == 0 ? "#P---" : $"#P{p.IdPagoCuota:000}").FontSize(8);
                                        table.Cell().Padding(5).Text($"{p.Cliente}\nDNI: {p.Dni}").FontSize(8);
                                        table.Cell().Padding(5).Text(p.Credito).FontSize(8);
                                        table.Cell().Padding(5).Text($"S/ {p.Monto:N2}").FontSize(8);
                                        table.Cell().Padding(5).Text(p.FechaPago.ToString("dd/MM/yyyy")).FontSize(8);
                                        table.Cell().Padding(5).Text(p.MetodoPago).FontSize(8);
                                        table.Cell().Padding(5).Text(p.Estado == "Aprobado" ? "Completado" : p.Estado).FontSize(8);
                                        table.Cell().Padding(5).Text(p.Referencia).FontSize(8);
                                    }
                                });
                            }

                            col.Item().PaddingTop(18).Row(row =>
                            {
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                                {
                                    c.Item().Text("Resumen por Estado").Bold().FontSize(13).FontColor(Colors.Purple.Darken3);
                                    c.Item().Text($"Completados: {completados.Count}");
                                    c.Item().Text($"Pendientes / Mora: {pendientes.Count}");
                                    c.Item().Text($"Total: {reporte.Count}");
                                });

                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                                {
                                    c.Item().Text("Métodos de Pago").Bold().FontSize(13).FontColor(Colors.Purple.Darken3);
                                    c.Item().Text($"Yape: {reporte.Count(x => x.MetodoPago == "Yape")}");
                                    c.Item().Text($"Plin: {reporte.Count(x => x.MetodoPago == "Plin")}");
                                    c.Item().Text($"Transferencia: {reporte.Count(x => x.MetodoPago == "Transferencia bancaria")}");
                                    c.Item().Text($"Sin pago: {reporte.Count(x => x.MetodoPago == "Sin pago")}");
                                });
                            });
                        });

                        page.Footer().AlignCenter()
                            .Text("CrediPlus - Reporte generado automáticamente")
                            .FontSize(9);
                    });
                }).GeneratePdf();

                return File(pdf, "application/pdf", "Reporte_Pagos.pdf");
            }
            public IActionResult CronogramaAdminPdf(int idSolicitud)
            {
                QuestPDF.Settings.License = LicenseType.Community;

                var solicitud = _context.SOLICITUD_CREDITO
                    .Include(x => x.USUARIO)
                    .FirstOrDefault(x => x.Id_Solicitud == idSolicitud);

                if (solicitud == null)
                    return RedirectToAction("Pagos");

                var cuotas = _context.CUOTA
                    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud)
                    .OrderBy(x => x.NumeroCuota)
                    .ToList();

                var pagos = _context.PAGO_CUOTA.ToList();

                // Aquí construiremos el PDF bonito en el siguiente paso.

                var pdf = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(30);

                        page.Header().Column(col =>
                        {
                            col.Item().Text("CrediPlus - Cronograma de Pago")
                                .FontSize(22)
                                .Bold()
                                .FontColor(Colors.Purple.Darken3);

                            col.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken1);
                        });

                        page.Content().PaddingVertical(15).Column(col =>
                        {
                            col.Item().Border(1).BorderColor(Colors.Purple.Lighten3).Padding(12).Column(info =>
                            {
                                info.Item().Text("Información del Cliente").FontSize(14).Bold();

                                info.Item().Text($"Cliente: {solicitud.USUARIO?.Nombre} {solicitud.USUARIO?.Apellido}");
                                info.Item().Text($"DNI: {solicitud.USUARIO?.Dni}");
                                info.Item().Text($"Crédito: #C{solicitud.Id_Solicitud:000}");
                                info.Item().Text($"Monto solicitado: S/ {solicitud.MontoSolicitado:N2}");
                                info.Item().Text($"Plazo: {solicitud.PlazoMeses} meses");
                            });

                            col.Item().PaddingTop(15).Text("Detalle de Cuotas")
                                .FontSize(15)
                                .Bold()
                                .FontColor(Colors.Purple.Darken3);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Purple.Darken3).Padding(6).Text("N°").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Purple.Darken3).Padding(6).Text("Monto").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Purple.Darken3).Padding(6).Text("Vencimiento").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Purple.Darken3).Padding(6).Text("Estado").FontColor(Colors.White).Bold();
                                    header.Cell().Background(Colors.Purple.Darken3).Padding(6).Text("Pago").FontColor(Colors.White).Bold();
                                });

                                foreach (var cuota in cuotas)
                                {
                                    var pago = pagos.FirstOrDefault(x => x.Id_Cuota == cuota.Id_Cuota && x.Estado == "Aprobado");

                                    bool completado = pago != null;
                                    bool mora = !completado && cuota.FechaLimitePago.Date < DateTime.Today;

                                    string estado = completado ? "Completado" : mora ? "Mora" : "Pendiente";
                                    string fechaPago = completado ? pago.FechaPago.ToString("dd/MM/yyyy") : "---";

                                    string color = completado
                                        ? Colors.Green.Lighten4
                                        : mora
                                            ? Colors.Red.Lighten4
                                            : Colors.Yellow.Lighten4;

                                    table.Cell().Background(color).Padding(6).Text(cuota.NumeroCuota.ToString());
                                    table.Cell().Background(color).Padding(6).Text($"S/ {(cuota.MontoCuota ?? 0):N2}");
                                    table.Cell().Background(color).Padding(6).Text(cuota.FechaLimitePago.ToString("dd/MM/yyyy"));
                                    table.Cell().Background(color).Padding(6).Text(estado).Bold();
                                    table.Cell().Background(color).Padding(6).Text(fechaPago);
                                }
                            });
                        });

                        page.Footer().AlignCenter()
                            .Text("CrediPlus - Cronograma generado automáticamente")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                    });
                }).GeneratePdf();

                Response.Headers["Content-Disposition"] = "inline; filename=Cronograma_CrediPlus.pdf";
                return File(pdf, "application/pdf");
            }

            public IActionResult CronogramaAdmin(int idSolicitud)
            {
                var solicitud = _context.SOLICITUD_CREDITO
                    .FirstOrDefault(x => x.Id_Solicitud == idSolicitud);

                if (solicitud == null)
                {
                    return RedirectToAction("Pagos");
                }

                return View(solicitud);
            }
            public async Task<IActionResult> Clientes(string? buscar, string estado = "Todos", int pagina = 1)
            {
                const int cantidadPorPagina = 8;

                DateTime primerDiaDelMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime primerDiaSiguienteMes = primerDiaDelMes.AddMonths(1);

                var clientesBase = _context.Usuario
                    .AsNoTracking()
                    .Where(u => u.Rol == "Cliente");

                int totalClientes = await clientesBase.CountAsync();

                int nuevosClientesEsteMes = await clientesBase.CountAsync(u =>
                    u.FechaRegistro >= primerDiaDelMes &&
                    u.FechaRegistro < primerDiaSiguienteMes);

                DateTime limiteActivo = DateTime.Now.AddMinutes(-2);

                int clientesActivos = await clientesBase.CountAsync(u =>
                    u.EstadoActivo == true &&
                    u.UltimaConexion != null &&
                    u.UltimaConexion >= limiteActivo);

                decimal montoTotalOtorgado = await _context.SOLICITUD_CREDITO
                    .AsNoTracking()
                    .Where(s => s.Estado == "Aprobado" &&
                                s.USUARIO != null &&
                                s.USUARIO.Rol == "Cliente")
                    .SumAsync(s => (decimal?)s.MontoSolicitado) ?? 0;

                var consulta = ObtenerClientesFiltrados(buscar, estado);

                int totalFiltrado = await consulta.CountAsync();
                int totalPaginas = (int)Math.Ceiling(totalFiltrado / (double)cantidadPorPagina);

                if (pagina < 1) pagina = 1;
                if (totalPaginas > 0 && pagina > totalPaginas) pagina = totalPaginas;

                var clientesData = await consulta
                    .OrderByDescending(u => u.Id)
                    .Skip((pagina - 1) * cantidadPorPagina)
                    .Take(cantidadPorPagina)
                    .ToListAsync();

                var clientes = clientesData.Select((u, index) => new ClienteAdminItemViewModel
                {
                    Id = u.Id,
                    NumeroFila = ((pagina - 1) * cantidadPorPagina) + index + 1,
                    Nombre = u.Nombre,
                    Apellido = u.Apellido,
                    NombreCompleto = $"{u.Nombre} {u.Apellido}",
                    Iniciales = ObtenerIniciales(u.Nombre, u.Apellido),
                    Dni = u.Dni,
                    Celular = u.Celular,
                    Correo = u.Correo,
                    Genero = u.Genero ?? "",
                    EstadoActivo = u.EstadoActivo == true &&
                   u.UltimaConexion != null &&
                   u.UltimaConexion >= limiteActivo,
                    FechaRegistro = u.FechaRegistro
                }).ToList();

                var modelo = new AdminClientesViewModel
                {
                    TotalClientes = totalClientes,
                    ClientesActivos = clientesActivos,
                    NuevosClientesEsteMes = nuevosClientesEsteMes,
                    MontoTotalOtorgado = montoTotalOtorgado,
                    PorcentajeActivos = totalClientes == 0 ? 0 : clientesActivos * 100.0 / totalClientes,
                    Buscar = buscar,
                    Estado = estado,
                    PaginaActual = pagina,
                    TotalPaginas = totalPaginas,
                    TotalFiltrado = totalFiltrado,
                    InicioRegistro = totalFiltrado == 0 ? 0 : ((pagina - 1) * cantidadPorPagina) + 1,
                    FinRegistro = Math.Min(pagina * cantidadPorPagina, totalFiltrado),
                    Clientes = clientes
                };

                return View(modelo);
            }
            private IQueryable<Usuario> ObtenerClientesFiltrados(string? buscar, string estado)
            {
                DateTime limiteActivo = DateTime.Now.AddMinutes(-2);

                var consulta = _context.Usuario
                    .AsNoTracking()
                    .Where(u => u.Rol == "Cliente");

                if (!string.IsNullOrWhiteSpace(buscar))
                {
                    string texto = buscar.Trim().ToLower();

                    consulta = consulta.Where(u =>
                        (u.Nombre + " " + u.Apellido).ToLower().Contains(texto) ||
                        u.Dni.Contains(texto) ||
                        u.Celular.Contains(texto) ||
                        u.Correo.ToLower().Contains(texto)
                    );
                }

                if (estado == "Activo")
                {
                    consulta = consulta.Where(u =>
                        u.EstadoActivo == true &&
                        u.UltimaConexion != null &&
                        u.UltimaConexion >= limiteActivo);
                }
                else if (estado == "Inactivo")
                {
                    consulta = consulta.Where(u =>
                        u.UltimaConexion == null ||
                        u.UltimaConexion < limiteActivo ||
                        u.EstadoActivo == false);
                }

                return consulta;
            }

            private static string ObtenerIniciales(string? nombre, string? apellido)
            {
                string inicialNombre = string.IsNullOrWhiteSpace(nombre)
                    ? ""
                    : nombre.Trim()[0].ToString().ToUpper();

                string inicialApellido = string.IsNullOrWhiteSpace(apellido)
                    ? ""
                    : apellido.Trim()[0].ToString().ToUpper();

                return inicialNombre + inicialApellido;
            }
            private static IContainer HeaderCellStyle(IContainer container)
            {
                return container
                    .Background("#6D28D9")
                    .PaddingVertical(6)
                    .PaddingHorizontal(5);
            }

            private static IContainer BodyCellStyle(IContainer container)
            {
                return container
                    .BorderBottom(1)
                    .BorderColor(Colors.Grey.Lighten2)
                    .PaddingVertical(6)
                    .PaddingHorizontal(5);
            }
            public async Task<IActionResult> ExportarClientesExcel(string? buscar, string estado = "Todos", DateTime? fechaInicio = null, DateTime? fechaFin = null)
            {
                DateTime desde = fechaInicio ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime hasta = fechaFin ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month));

                var clientes = await ObtenerClientesFiltrados(buscar, estado)
                    .Where(x => x.FechaRegistro.Date >= desde.Date &&
                                x.FechaRegistro.Date <= hasta.Date)
                    .OrderBy(x => x.Id)
                    .ToListAsync();

                using var workbook = new XLWorkbook();
                var hoja = workbook.Worksheets.Add("Clientes");

                hoja.Cell(1, 1).Value = "ID";
                hoja.Cell(1, 2).Value = "Cliente";
                hoja.Cell(1, 3).Value = "DNI";
                hoja.Cell(1, 4).Value = "Celular";
                hoja.Cell(1, 5).Value = "Correo";
                hoja.Cell(1, 6).Value = "Estado";
                hoja.Cell(1, 7).Value = "Última Conexión";

                var header = hoja.Range(1, 1, 1, 7);
                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = XLColor.FromHtml("#6D28D9");
                header.Style.Font.FontColor = XLColor.White;

                int fila = 2;

                foreach (var c in clientes)
                {
                    hoja.Cell(fila, 1).Value = $"#{c.Id:000}";
                    hoja.Cell(fila, 2).Value = $"{c.Nombre} {c.Apellido}";
                    hoja.Cell(fila, 3).Value = c.Dni;
                    hoja.Cell(fila, 4).Value = c.Celular;
                    hoja.Cell(fila, 5).Value = c.Correo;
                    hoja.Cell(fila, 6).Value = c.UltimaConexion != null ? "Activo" : "Inactivo";
                    hoja.Cell(fila, 7).Value = c.UltimaConexion?.ToString("dd/MM/yyyy HH:mm") ?? "Sin conexión";
                    fila++;
                }

                hoja.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Clientes_CrediPlus.xlsx"
                );
            }
            public async Task<IActionResult> ExportarClientesPdf(string? buscar, string estado = "Todos", DateTime? fechaInicio = null, DateTime? fechaFin = null)
            {
                QuestPDF.Settings.License = LicenseType.Community;

                DateTime desde = fechaInicio ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime hasta = fechaFin ?? new DateTime(
                    DateTime.Now.Year,
                    DateTime.Now.Month,
                    DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month)
                );

                var clientes = await ObtenerClientesFiltrados(buscar, estado)
                    .Where(x => x.FechaRegistro.Date >= desde.Date &&
                                x.FechaRegistro.Date <= hasta.Date)
                    .OrderBy(x => x.Id)
                    .ToListAsync();

                using var stream = new MemoryStream();

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(30);

                        page.Header().Column(col =>
                        {
                            col.Item().Text("Reporte de Clientes - CrediPlus")
                                .FontSize(20)
                                .Bold()
                                .FontColor("#6D28D9");

                            col.Item().Text($"Periodo: {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}")
                                .FontSize(10)
                                .FontColor(Colors.Grey.Darken2);
                        });

                        page.Content().Column(col =>
                        {
                            if (!clientes.Any())
                            {
                                col.Item().Text("No hay clientes registrados para el período seleccionado.")
                                    .FontSize(13)
                                    .Bold();
                            }
                            else
                            {
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(0.8f);
                                        columns.RelativeColumn(2.2f);
                                        columns.RelativeColumn(1.2f);
                                        columns.RelativeColumn(1.4f);
                                        columns.RelativeColumn(2.8f);
                                        columns.RelativeColumn(1.2f);
                                        columns.RelativeColumn(1.8f);
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(HeaderCellStyle).Text("ID").FontColor(Colors.White).Bold();
                                        header.Cell().Element(HeaderCellStyle).Text("Cliente").FontColor(Colors.White).Bold();
                                        header.Cell().Element(HeaderCellStyle).Text("DNI").FontColor(Colors.White).Bold();
                                        header.Cell().Element(HeaderCellStyle).Text("Celular").FontColor(Colors.White).Bold();
                                        header.Cell().Element(HeaderCellStyle).Text("Correo").FontColor(Colors.White).Bold();
                                        header.Cell().Element(HeaderCellStyle).Text("Estado").FontColor(Colors.White).Bold();
                                        header.Cell().Element(HeaderCellStyle).Text("Última Conexión").FontColor(Colors.White).Bold();
                                    });

                                    foreach (var c in clientes)
                                    {
                                        table.Cell().Element(BodyCellStyle).Text($"#{c.Id:000}");
                                        table.Cell().Element(BodyCellStyle).Text($"{c.Nombre} {c.Apellido}");
                                        table.Cell().Element(BodyCellStyle).Text(c.Dni);
                                        table.Cell().Element(BodyCellStyle).Text(c.Celular);
                                        table.Cell().Element(BodyCellStyle).Text(c.Correo);
                                        table.Cell().Element(BodyCellStyle).Text(c.UltimaConexion != null ? "Activo" : "Inactivo");
                                        table.Cell().Element(BodyCellStyle).Text(c.UltimaConexion?.ToString("dd/MM/yyyy HH:mm") ?? "Sin conexión");
                                    }
                                });
                            }
                        });
                    });
                }).GeneratePdf(stream);

                return File(stream.ToArray(), "application/pdf", "Clientes_CrediPlus.pdf");
            }
            public async Task<IActionResult> Creditos(string? buscar, string estado = "Todos", int pagina = 1)
            {
                CargarDatosAdministrador();
                ViewData["ActivePage"] = "Creditos";

                const int cantidadPorPagina = 8;

                var baseCreditos = _context.SOLICITUD_CREDITO
                    .AsNoTracking()
                    .Include(s => s.USUARIO)
                    .AsQueryable();

                int totalCreditos = await baseCreditos.CountAsync();
                int creditosAprobados = await baseCreditos.CountAsync(s => s.Estado == "Aprobado");
                int creditosEvaluacion = await baseCreditos.CountAsync(s => s.Estado == "Pendiente" || s.Estado == "En Evaluación");
                int creditosRechazados = await baseCreditos.CountAsync(s => s.Estado == "Rechazado");

                decimal montoTotalSolicitado = await baseCreditos
                    .SumAsync(s => (decimal?)s.MontoSolicitado) ?? 0;

                var consulta = baseCreditos;

                if (!string.IsNullOrWhiteSpace(buscar))
                {
                    string texto = buscar.Trim().ToLower();

                    consulta = consulta.Where(s =>
                        s.NumeroSolicitud.ToLower().Contains(texto) ||
                        s.USUARIO.Nombre.ToLower().Contains(texto) ||
                        s.USUARIO.Apellido.ToLower().Contains(texto) ||
                        s.USUARIO.Dni.Contains(texto)
                    );
                }

                if (estado != "Todos")
                {
                    consulta = consulta.Where(s => s.Estado == estado);
                }

                int totalFiltrado = await consulta.CountAsync();
                int totalPaginas = (int)Math.Ceiling(totalFiltrado / (double)cantidadPorPagina);

                if (pagina < 1) pagina = 1;
                if (totalPaginas > 0 && pagina > totalPaginas) pagina = totalPaginas;

                var creditosData = await consulta
                    .OrderByDescending(s => s.FechaSolicitud)
                    .Skip((pagina - 1) * cantidadPorPagina)
                    .Take(cantidadPorPagina)
                    .ToListAsync();

                var creditos = creditosData.Select((s, index) => new CreditoAdminItemViewModel
                {
                    IdSolicitud = s.Id_Solicitud,
                    NumeroFila = ((pagina - 1) * cantidadPorPagina) + index + 1,
                    NumeroSolicitud = s.NumeroSolicitud ?? $"SOL-{s.Id_Solicitud:000}",
                    Cliente = $"{s.USUARIO?.Nombre} {s.USUARIO?.Apellido}",
                    Iniciales = ObtenerIniciales(s.USUARIO?.Nombre, s.USUARIO?.Apellido),
                    Dni = s.USUARIO?.Dni ?? "",
                    MontoSolicitado = s.MontoSolicitado,
                    PlazoMeses = s.PlazoMeses,
                    InteresEstimado = s.InteresEstimado,
                    Estado = s.Estado,
                    FechaSolicitud = s.FechaSolicitud,
                    TipoCredito = "",
                    MotivoPrestamo = ""
                }).ToList();
                DateTime primerDiaDelMesCreditos = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime primerDiaSiguienteMesCreditos = primerDiaDelMesCreditos.AddMonths(1);

                int creditosPendientesDistribucion = await baseCreditos.CountAsync(s => s.Estado == "Pendiente");
                int creditosEvaluacionDistribucion = await baseCreditos.CountAsync(s => s.Estado == "En Evaluación");
                int creditosRechazadosDistribucion = await baseCreditos.CountAsync(s => s.Estado == "Rechazado");

                var coloresTipo = new[] { "#22c55e", "#2563eb", "#f97316", "#8b5cf6", "#ef4444", "#14b8a6", "#eab308" };

                var tiposCreditoGrafico = await (
                    from s in _context.SOLICITUD_CREDITO
                    join p in _context.PERFIL_FINANCIERO
                        on s.Id_Solicitud equals p.SOLICITUD_CREDITO_Id_Solicitud into perfilJoin
                    from p in perfilJoin.DefaultIfEmpty()
                    group s by (p == null || string.IsNullOrEmpty(p.MotivoPrestamo)
                        ? "Sin motivo"
                        : p.MotivoPrestamo) into g
                    select new
                    {
                        Tipo = g.Key,
                        Cantidad = g.Count()
                    }
                ).ToListAsync();

                int totalTipos = tiposCreditoGrafico.Sum(x => x.Cantidad);

                var tiposGraficoFinal = tiposCreditoGrafico
                    .Select((x, index) => new TipoCreditoGraficoViewModel
                    {
                        Tipo = x.Tipo,
                        Cantidad = x.Cantidad,
                        Porcentaje = totalTipos > 0 ? Math.Round((decimal)x.Cantidad * 100 / totalTipos, 1) : 0,
                        Color = coloresTipo[index % coloresTipo.Length]
                    })
                    .ToList();

                int nuevosCreditosMes = await baseCreditos.CountAsync(s =>
                    s.FechaSolicitud >= primerDiaDelMesCreditos &&
                    s.FechaSolicitud < primerDiaSiguienteMesCreditos);

                decimal montoOtorgadoMes = await baseCreditos
                    .Where(s => s.Estado == "Aprobado" &&
                                s.FechaSolicitud >= primerDiaDelMesCreditos &&
                                s.FechaSolicitud < primerDiaSiguienteMesCreditos)
                    .SumAsync(s => (decimal?)s.MontoSolicitado) ?? 0;

                int creditosDesembolsadosMes = await baseCreditos.CountAsync(s =>
                    s.Estado == "Aprobado" &&
                    s.FechaSolicitud >= primerDiaDelMesCreditos &&
                    s.FechaSolicitud < primerDiaSiguienteMesCreditos);
                var modelo = new AdminCreditosViewModel
                {
                    TotalCreditos = totalCreditos,
                    CreditosAprobados = creditosAprobados,
                    CreditosEvaluacion = creditosEvaluacion,
                    CreditosRechazados = creditosRechazados,
                    MontoTotalSolicitado = montoTotalSolicitado,
                    Buscar = buscar,
                    Estado = estado,
                    PaginaActual = pagina,
                    TotalPaginas = totalPaginas,
                    TotalFiltrado = totalFiltrado,
                    InicioRegistro = totalFiltrado == 0 ? 0 : ((pagina - 1) * cantidadPorPagina) + 1,
                    FinRegistro = Math.Min(pagina * cantidadPorPagina, totalFiltrado),
                    Creditos = creditos,

                    TotalDistribucionEstado = totalCreditos,
                    CreditosActivosDistribucion = creditosAprobados,
                    CreditosCursoDistribucion = creditosEvaluacionDistribucion,
                    CreditosPendientesDistribucion = creditosPendientesDistribucion,
                    CreditosCanceladosDistribucion = creditosRechazadosDistribucion,

                    TotalDistribucionTipo = totalCreditos,
                    TiposCreditoGrafico = tiposGraficoFinal,

                    NuevosCreditosMes = nuevosCreditosMes,
                    MontoOtorgadoMes = montoOtorgadoMes,
                    CreditosDesembolsadosMes = creditosDesembolsadosMes
                };

                return View(modelo);
            }
            public IActionResult ExportarCreditosExcel(string? buscar, string estado = "Todos", DateTime? fechaInicio = null, DateTime? fechaFin = null)
            {
                var query =
                    from s in _context.SOLICITUD_CREDITO.Include(x => x.USUARIO)
                    join p in _context.PERFIL_FINANCIERO
                        on s.Id_Solicitud equals p.SOLICITUD_CREDITO_Id_Solicitud into perfilJoin
                    from p in perfilJoin.DefaultIfEmpty()
                    select new { Solicitud = s, Perfil = p };

                if (!string.IsNullOrWhiteSpace(buscar))
                {
                    string texto = buscar.Trim().ToLower();

                    query = query.Where(x =>
                        x.Solicitud.NumeroSolicitud.ToLower().Contains(texto) ||
                        x.Solicitud.USUARIO.Nombre.ToLower().Contains(texto) ||
                        x.Solicitud.USUARIO.Apellido.ToLower().Contains(texto) ||
                        x.Solicitud.USUARIO.Dni.Contains(texto));
                }

                if (estado != "Todos")
                    query = query.Where(x => x.Solicitud.Estado == estado);
                DateTime desde = fechaInicio ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime hasta = fechaFin ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month));

                query = query.Where(x =>
                    x.Solicitud.FechaSolicitud.Date >= desde.Date &&
                    x.Solicitud.FechaSolicitud.Date <= hasta.Date);

                var datos = query.OrderByDescending(x => x.Solicitud.FechaSolicitud).ToList();

                using var workbook = new XLWorkbook();
                var hoja = workbook.Worksheets.Add("Créditos");

                hoja.Cell("A1").Value = "CREDIPLUS - REPORTE DE CRÉDITOS";
                hoja.Range("A1:I1").Merge();
                hoja.Range("A1:I1").Style.Font.Bold = true;
                hoja.Range("A1:I1").Style.Font.FontSize = 18;
                hoja.Range("A1:I1").Style.Font.FontColor = XLColor.White;
                hoja.Range("A1:I1").Style.Fill.BackgroundColor = XLColor.FromHtml("#6D28D9");

                hoja.Cell("A3").Value = "Generado:";
                hoja.Cell("B3").Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                hoja.Cell("A4").Value = "Filtro estado:";
                hoja.Cell("B4").Value = estado;
                hoja.Cell("A5").Value = "Búsqueda:";
                hoja.Cell("B5").Value = string.IsNullOrWhiteSpace(buscar) ? "Sin búsqueda" : buscar;

                hoja.Cell("A7").Value = "Solicitud";
                hoja.Cell("B7").Value = "Cliente";
                hoja.Cell("C7").Value = "DNI";
                hoja.Cell("D7").Value = "Monto";
                hoja.Cell("E7").Value = "Plazo";
                hoja.Cell("F7").Value = "Interés";
                hoja.Cell("G7").Value = "Estado";
                hoja.Cell("H7").Value = "Fecha";
                hoja.Cell("I7").Value = "Motivo";

                hoja.Range("A7:I7").Style.Font.Bold = true;
                hoja.Range("A7:I7").Style.Font.FontColor = XLColor.White;
                hoja.Range("A7:I7").Style.Fill.BackgroundColor = XLColor.FromHtml("#4C1D95");

                int fila = 8;

                foreach (var item in datos)
                {
                    hoja.Cell(fila, 1).Value = item.Solicitud.NumeroSolicitud;
                    hoja.Cell(fila, 2).Value = $"{item.Solicitud.USUARIO?.Nombre} {item.Solicitud.USUARIO?.Apellido}";
                    hoja.Cell(fila, 3).Value = item.Solicitud.USUARIO?.Dni;
                    hoja.Cell(fila, 4).Value = item.Solicitud.MontoSolicitado;
                    hoja.Cell(fila, 5).Value = item.Solicitud.PlazoMeses + " meses";
                    hoja.Cell(fila, 6).Value = item.Solicitud.InteresEstimado;
                    hoja.Cell(fila, 7).Value = item.Solicitud.Estado;
                    hoja.Cell(fila, 8).Value = item.Solicitud.FechaSolicitud.ToString("dd/MM/yyyy");
                    hoja.Cell(fila, 9).Value = item.Perfil?.MotivoPrestamo ?? "Sin motivo";

                    fila++;
                }

                hoja.Column(4).Style.NumberFormat.Format = "\"S/\" #,##0.00";
                hoja.Column(6).Style.NumberFormat.Format = "\"S/\" #,##0.00";
                hoja.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Creditos_CrediPlus_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                );
            }
            public IActionResult ExportarCreditosPdf(
        string? buscar,
        string estado = "Todos",
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null)
            {
                QuestPDF.Settings.License = LicenseType.Community;

                DateTime desde = fechaInicio ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime hasta = fechaFin ?? new DateTime(
                    DateTime.Now.Year,
                    DateTime.Now.Month,
                    DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month)
                );

                var query =
                    from s in _context.SOLICITUD_CREDITO.Include(x => x.USUARIO)
                    join p in _context.PERFIL_FINANCIERO
                        on s.Id_Solicitud equals p.SOLICITUD_CREDITO_Id_Solicitud into perfilJoin
                    from p in perfilJoin.DefaultIfEmpty()
                    where s.FechaSolicitud.Date >= desde.Date &&
                          s.FechaSolicitud.Date <= hasta.Date
                    select new { Solicitud = s, Perfil = p };

                if (!string.IsNullOrWhiteSpace(buscar))
                {
                    string texto = buscar.Trim().ToLower();

                    query = query.Where(x =>
                        x.Solicitud.NumeroSolicitud.ToLower().Contains(texto) ||
                        x.Solicitud.USUARIO.Nombre.ToLower().Contains(texto) ||
                        x.Solicitud.USUARIO.Apellido.ToLower().Contains(texto) ||
                        x.Solicitud.USUARIO.Dni.Contains(texto));
                }

                if (estado != "Todos")
                    query = query.Where(x => x.Solicitud.Estado == estado);

                var datos = query
                    .OrderByDescending(x => x.Solicitud.FechaSolicitud)
                    .ToList();

                var pdf = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(25);

                        page.Header().Column(col =>
                        {
                            col.Item().Text("CrediPlus - Reporte de Créditos")
                                .FontSize(20).Bold().FontColor(Colors.Purple.Darken2);

                            col.Item().Text($"Periodo: {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}")
                                .FontSize(9).FontColor(Colors.Grey.Darken2);

                            col.Item().Text($"Estado: {estado}")
                                .FontSize(9).FontColor(Colors.Grey.Darken2);
                        });

                        page.Content().PaddingTop(15).Column(col =>
                        {
                            if (!datos.Any())
                            {
                                col.Item().Text("No hay solicitudes registradas para el período seleccionado.")
                                    .FontSize(13)
                                    .Bold();
                            }
                            else
                            {
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Background(Colors.Purple.Darken2).Padding(5).Text("Solicitud").FontColor(Colors.White).Bold();
                                        header.Cell().Background(Colors.Purple.Darken2).Padding(5).Text("Cliente").FontColor(Colors.White).Bold();
                                        header.Cell().Background(Colors.Purple.Darken2).Padding(5).Text("DNI").FontColor(Colors.White).Bold();
                                        header.Cell().Background(Colors.Purple.Darken2).Padding(5).Text("Monto").FontColor(Colors.White).Bold();
                                        header.Cell().Background(Colors.Purple.Darken2).Padding(5).Text("Plazo").FontColor(Colors.White).Bold();
                                        header.Cell().Background(Colors.Purple.Darken2).Padding(5).Text("Interés").FontColor(Colors.White).Bold();
                                        header.Cell().Background(Colors.Purple.Darken2).Padding(5).Text("Estado").FontColor(Colors.White).Bold();
                                        header.Cell().Background(Colors.Purple.Darken2).Padding(5).Text("Fecha").FontColor(Colors.White).Bold();
                                        header.Cell().Background(Colors.Purple.Darken2).Padding(5).Text("Motivo").FontColor(Colors.White).Bold();
                                    });

                                    foreach (var item in datos)
                                    {
                                        table.Cell().Padding(5).Text(item.Solicitud.NumeroSolicitud ?? "");
                                        table.Cell().Padding(5).Text($"{item.Solicitud.USUARIO?.Nombre} {item.Solicitud.USUARIO?.Apellido}");
                                        table.Cell().Padding(5).Text(item.Solicitud.USUARIO?.Dni ?? "");
                                        table.Cell().Padding(5).Text($"S/ {item.Solicitud.MontoSolicitado:N2}");
                                        table.Cell().Padding(5).Text($"{item.Solicitud.PlazoMeses} meses");
                                        table.Cell().Padding(5).Text($"S/ {item.Solicitud.InteresEstimado:N2}");
                                        table.Cell().Padding(5).Text(item.Solicitud.Estado ?? "");
                                        table.Cell().Padding(5).Text(item.Solicitud.FechaSolicitud.ToString("dd/MM/yyyy"));
                                        table.Cell().Padding(5).Text(item.Perfil?.MotivoPrestamo ?? "Sin motivo");
                                    }
                                });
                            }
                        });

                        page.Footer().AlignCenter()
                            .Text("CrediPlus - Reporte de Créditos")
                            .FontSize(9);
                    });
                }).GeneratePdf();

                return File(pdf, "application/pdf", $"Creditos_CrediPlus_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            }
            private static IContainer CreditosPdfHeader(IContainer container)
            {
                return container
                    .Background("#6D28D9")
                    .PaddingVertical(6)
                    .PaddingHorizontal(4);
            }

            private static IContainer CreditosPdfCell(IContainer container)
            {
                return container
                    .BorderBottom(1)
                    .BorderColor(Colors.Grey.Lighten2)
                    .PaddingVertical(5)
                    .PaddingHorizontal(4);
            }
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> EditarClienteAdmin(
        int id,
        string nombre,
        string apellido,
        string dni,
        string celular,
        string correo,
        string? genero,
        bool estadoActivo)
            {
                var cliente = await _context.Usuario
                    .FirstOrDefaultAsync(x => x.Id == id && x.Rol == "Cliente");

                if (cliente == null)
                {
                    return RedirectToAction("Clientes");
                }

                cliente.Nombre = nombre.Trim();
                cliente.Apellido = apellido.Trim();
                cliente.Dni = dni.Trim();
                cliente.Celular = celular.Trim();
                cliente.Correo = correo.Trim();
                cliente.Genero = genero;
                cliente.EstadoActivo = estadoActivo;

                await _context.SaveChangesAsync();

                return RedirectToAction("Clientes");
            }
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> EliminarClienteAdmin(int id)
            {
                var cliente = await _context.Usuario
                    .FirstOrDefaultAsync(x => x.Id == id && x.Rol == "Cliente");

                if (cliente == null)
                {
                    return Json(new { ok = false, mensaje = "No se encontró el cliente." });
                }

                bool tieneSolicitudActiva = await _context.SOLICITUD_CREDITO.AnyAsync(x =>
                    x.Usuario_Id_Usuario == id &&
                    (
                        x.Estado == "Pendiente" ||
                        x.Estado == "En Evaluación" ||
                        x.Estado == "Aprobado" ||
                        x.Estado == "Rechazado"
                    ));

                if (tieneSolicitudActiva)
                {
                    return Json(new { ok = false, mensaje = "No se puede eliminar este cliente porque tiene una solicitud activa." });
                }

                _context.Usuario.Remove(cliente);
                await _context.SaveChangesAsync();

                return Json(new { ok = true, mensaje = "Cliente eliminado correctamente." });
            }

            public IActionResult PerfilAdministrador()
            {
                CargarDatosAdministrador();
                ViewData["ActivePage"] = "Perfil";

                var correoAdmin =
                    User.FindFirst(ClaimTypes.Email)?.Value ??
                    User.FindFirst("Correo")?.Value ??
                    User.FindFirst("correo")?.Value ??
                    User.Identity?.Name;

                var admin = _context.Usuario
                    .FirstOrDefault(x => x.Correo == correoAdmin && x.Rol == "Administrador");

                if (admin == null)
                    return NotFound();
                var actividadesAdmin = _context.ACTIVIDAD_ADMINISTRADOR
        .Where(x => x.IdUsuario == admin.Id)
        .OrderByDescending(x => x.Fecha)
        .ToList();

                ViewBag.ActividadesAdmin = actividadesAdmin;

                return View(admin);
            }

            [HttpPost]
            public IActionResult PerfilAdministrador(Usuario usuario, string codigoCorreo)
            {
                CargarDatosAdministrador();

                var correoActual =
                    User.FindFirst(ClaimTypes.Email)?.Value ??
                    User.FindFirst("Correo")?.Value ??
                    User.FindFirst("correo")?.Value ??
                    User.Identity?.Name;

                var admin = _context.Usuario
                    .FirstOrDefault(x => x.Correo == correoActual && x.Rol == "Administrador");

                if (admin == null)
                    return RedirectToAction("IniciarSesion", "Login");

                if (string.IsNullOrWhiteSpace(codigoCorreo) || codigoCorreo != codigoPerfilAdmin)
                {
                    TempData["MensajeError"] = "El código de verificación es incorrecto.";
                    return RedirectToAction("PerfilAdministrador");
                }

                admin.Nombre = usuario.Nombre;
                admin.Apellido = usuario.Apellido;
                admin.Genero = usuario.Genero;
                admin.Dni = usuario.Dni;
                admin.Celular = usuario.Celular;
                admin.Correo = usuario.Correo;

                if (!string.IsNullOrWhiteSpace(usuario.clave))
                    admin.clave = utilidades.EncriptarClave(usuario.clave);

                _context.SaveChanges();

                TempData["MensajeOk"] = "Perfil actualizado correctamente.";
                return RedirectToAction("PerfilAdministrador");
            }

            [HttpPost]
            public async Task<JsonResult> EnviarCodigoPerfilAdministrador(string correo)
            {
                if (string.IsNullOrWhiteSpace(correo))
                    return Json(new { ok = false, mensaje = "Ingrese un correo válido." });

                codigoPerfilAdmin = rnd.Next(100000, 999999).ToString();

                await _emailService.EnviarCodigoAsync(correo, codigoPerfilAdmin);

                return Json(new { ok = true, mensaje = "Código enviado correctamente." });
            }
            [HttpPost]
            [ValidateAntiForgeryToken]
            public IActionResult ActualizarPerfil(Usuario usuario, string? ConfirmarClave)
            {
                var correoAdmin =
                    User.FindFirst(ClaimTypes.Email)?.Value ??
                    User.FindFirst("Correo")?.Value ??
                    User.FindFirst("correo")?.Value ??
                    User.Identity?.Name;

                var admin = _context.Usuario
                    .FirstOrDefault(x => x.Correo == correoAdmin && x.Rol == "Administrador");
                if (admin == null)
                    return RedirectToAction("IniciarSesion", "Login");

                var cambios = new List<string>();
                if (admin.Nombre != usuario.Nombre)
                    cambios.Add($"Actualizó el nombre de '{admin.Nombre}' a '{usuario.Nombre}'.");

                if (admin.Apellido != usuario.Apellido)
                    cambios.Add($"Actualizó el apellido de '{admin.Apellido}' a '{usuario.Apellido}'.");

                if (admin.Correo != usuario.Correo)
                    cambios.Add($"Actualizó el correo de '{admin.Correo}' a '{usuario.Correo}'.");

                if (admin.Celular != usuario.Celular)
                    cambios.Add($"Actualizó el teléfono de '{admin.Celular}' a '{usuario.Celular}'.");

                if (admin.Dni != usuario.Dni)
                    cambios.Add($"Actualizó el DNI de '{admin.Dni}' a '{usuario.Dni}'.");

                if (admin.Genero != usuario.Genero)
                    cambios.Add($"Actualizó el género de '{admin.Genero}' a '{usuario.Genero}'.");

                if (string.IsNullOrWhiteSpace(usuario.Dni) || usuario.Dni.Length != 8)
                {
                    TempData["MensajeError"] = "El DNI debe tener exactamente 8 dígitos.";
                    return RedirectToAction("PerfilAdministrador");
                }

                bool dniExiste = _context.Usuario.Any(x =>
                    x.Dni == usuario.Dni &&
                    x.Id != admin.Id);

                if (dniExiste)
                {
                    TempData["MensajeError"] = "Ese DNI ya pertenece a otro usuario.";
                    return RedirectToAction("PerfilAdministrador");
                }

                admin.Nombre = usuario.Nombre;
                admin.Apellido = usuario.Apellido;
                admin.Dni = usuario.Dni;
                admin.Correo = usuario.Correo;
                admin.Celular = usuario.Celular;
                admin.Genero = usuario.Genero;

                if (!string.IsNullOrWhiteSpace(usuario.clave))
                {
                    if (usuario.clave != ConfirmarClave)
                    {
                        TempData["MensajeError"] = "Las contraseñas no coinciden.";
                        return RedirectToAction("PerfilAdministrador");
                    }

                    admin.clave = utilidades.EncriptarClave(usuario.clave);
                }

                _context.SaveChanges();

                if (cambios.Any())
                {
                    RegistrarActividadAdmin(
                        "Perfil actualizado",
                        string.Join(" ", cambios)
                    );
                }

                TempData["MensajeOk"] = "Perfil actualizado correctamente.";
                return RedirectToAction("PerfilAdministrador");
            }
            [HttpPost]
            public JsonResult ActualizarPreferenciasNotificaciones(
        bool notificacionCorreo,
        bool notificacionSistema,
        bool notificacionRecordatorio)
            {
                var correoAdmin =
                    User.FindFirst(ClaimTypes.Email)?.Value ??
                    User.FindFirst("Correo")?.Value ??
                    User.FindFirst("correo")?.Value ??
                    User.Identity?.Name;

                var admin = _context.Usuario
                    .FirstOrDefault(x => x.Correo == correoAdmin && x.Rol == "Administrador");

                if (admin == null)
                {
                    return Json(new { ok = false, mensaje = "No se encontró el administrador." });
                }

                admin.NotificacionCorreo = notificacionCorreo;
                admin.NotificacionSistema = notificacionSistema;
                admin.NotificacionRecordatorio = notificacionRecordatorio;

                _context.SaveChanges();

                return Json(new { ok = true, mensaje = "Preferencias guardadas correctamente." });
            }
            [HttpPost]
            public async Task<IActionResult> EnviarAlertasCorreoAdmin()
            {
                var correoAdmin =
                    User.FindFirst(ClaimTypes.Email)?.Value ??
                    User.FindFirst("Correo")?.Value ??
                    User.FindFirst("correo")?.Value ??
                    User.Identity?.Name;

                var admin = _context.Usuario
                    .FirstOrDefault(x => x.Correo == correoAdmin && x.Rol == "Administrador");

                if (admin == null)
                    return RedirectToAction("ProgramaAdministrador");

                if (!admin.NotificacionCorreo)
                {
                    TempData["MensajeError"] = "Las notificaciones por correo están desactivadas.";
                    return RedirectToAction("ProgramaAdministrador");
                }

                int solicitudesPendientes = _context.SOLICITUD_CREDITO
                    .Count(x => x.Estado == "Pendiente" || x.Estado == "En Evaluación");

                int cuotasMora = _context.CUOTA
                    .Count(x => x.Estado == "Pendiente" && x.FechaLimitePago.Date < DateTime.Today);

                string cuerpo = $@"
            <h2>CrediPlus - Alertas del Administrador</h2>
            <p>Hola {admin.Nombre}, tienes el siguiente resumen:</p>
            <ul>
                <li>Solicitudes pendientes o en evaluación: <strong>{solicitudesPendientes}</strong></li>
                <li>Cuotas en mora por revisar: <strong>{cuotasMora}</strong></li>
            </ul>
            <p>Ingresa al panel administrador para revisar los detalles.</p>
        ";

                await _emailService.EnviarCorreoAsync(
                    admin.Correo,
                    "CrediPlus - Alertas del Administrador",
                    cuerpo
                );

                TempData["MensajeOk"] = "Las alertas fueron enviadas a tu correo.";
                return RedirectToAction("ProgramaAdministrador");
            }
            public void RegistrarActividadAdmin(string tipo, string descripcion)
            {
                var correoAdmin =
                    User.FindFirst(ClaimTypes.Email)?.Value ??
                    User.FindFirst("Correo")?.Value ??
                    User.FindFirst("correo")?.Value ??
                    User.Identity?.Name;

                var admin = _context.Usuario
                    .FirstOrDefault(x => x.Correo == correoAdmin && x.Rol == "Administrador");

                if (admin == null)
                    return;

                var actividad = new ActividadAdministrador
                {
                    IdUsuario = admin.Id,
                    Tipo = tipo,
                    Descripcion = descripcion,
                    Fecha = DateTime.Now
                };

                _context.ACTIVIDAD_ADMINISTRADOR.Add(actividad);
                _context.SaveChanges();
            }
            public IActionResult ActividadAdministrador()
            {
                CargarDatosAdministrador();
                ViewData["Title"] = "Actividad del Administrador";
                ViewData["ActivePage"] = "Perfil";

                var correoAdmin =
                    User.FindFirst(ClaimTypes.Email)?.Value ??
                    User.FindFirst("Correo")?.Value ??
                    User.FindFirst("correo")?.Value ??
                    User.Identity?.Name;

                var admin = _context.Usuario
                    .FirstOrDefault(x => x.Correo == correoAdmin && x.Rol == "Administrador");

                if (admin == null)
                    return RedirectToAction("PerfilAdministrador");

                var actividades = _context.ACTIVIDAD_ADMINISTRADOR
                    .Where(x => x.IdUsuario == admin.Id)
                    .OrderByDescending(x => x.Fecha)
                    .ToList();

                return View(actividades);
            }
            public IActionResult GenerarReportes(int pagina = 1)
            {
                CargarDatosAdministrador();
                ViewData["ActivePage"] = "GenerarReportes";

                int porPagina = 3;

                int totalReportes = _context.REPORTE_GENERADO.Count();

                ViewBag.PaginaActual = pagina;
                ViewBag.TotalPaginas = (int)Math.Ceiling((double)totalReportes / porPagina);

                ViewBag.ReportesRecientes = _context.REPORTE_GENERADO
                    .OrderByDescending(x => x.FechaSolicitud)
                    .Skip((pagina - 1) * porPagina)
                    .Take(porPagina)
                    .ToList();

                return View();
            }

        [HttpPost]
        public IActionResult CrearPlantillaNotificacion(
    string nombre,
    string descripcion,
    string asunto,
    string mensaje,
    string icono,
    string color)
        {
            if (string.IsNullOrWhiteSpace(nombre) ||
                string.IsNullOrWhiteSpace(descripcion) ||
                string.IsNullOrWhiteSpace(asunto) ||
                string.IsNullOrWhiteSpace(mensaje))
            {
                TempData["ErrorPlantilla"] = "Debe completar todos los campos de la plantilla.";
                return RedirectToAction("CentroNotificaciones");
            }

            var plantilla = new PlantillaNotificacion
            {
                Nombre = nombre.Trim(),
                Descripcion = descripcion.Trim(),
                Asunto = asunto.Trim(),
                Mensaje = mensaje.Trim(),
                Icono = string.IsNullOrWhiteSpace(icono) ? "fa-regular fa-bell" : icono,
                Color = string.IsNullOrWhiteSpace(color) ? "purple" : color,
                Canal = "Correo Electrónico",
                FechaCreacion = DateTime.Now,
                Activo = true
            };
            bool asuntoExiste = _context.PLANTILLA_NOTIFICACION
    .Any(x => x.Activo == true && x.Asunto.ToLower() == asunto.Trim().ToLower());

            if (asuntoExiste)
            {
                TempData["ErrorPlantilla"] = "Ya existe una plantilla con ese asunto.";
                return RedirectToAction("CentroNotificaciones");
            }

            _context.PLANTILLA_NOTIFICACION.Add(plantilla);
            _context.SaveChanges();

            TempData["OkPlantilla"] = "Plantilla creada correctamente.";
            return RedirectToAction("CentroNotificaciones");
        }
        [HttpPost]
        public IActionResult EditarPlantillaNotificacion(
    int id,
    string nombre,
    string descripcion,
    string asunto,
    string mensaje,
    string icono,
    string color)
        {
            var plantilla = _context.PLANTILLA_NOTIFICACION
                .FirstOrDefault(x => x.Id_Plantilla == id);

            if (plantilla == null)
                return RedirectToAction("CentroNotificaciones");

            if (string.IsNullOrWhiteSpace(nombre) ||
                string.IsNullOrWhiteSpace(descripcion) ||
                string.IsNullOrWhiteSpace(asunto) ||
                string.IsNullOrWhiteSpace(mensaje))
            {
                TempData["ErrorPlantilla"] = "Debe completar todos los campos de la plantilla.";
                return RedirectToAction("CentroNotificaciones");
            }

            plantilla.Nombre = nombre.Trim();
            plantilla.Descripcion = descripcion.Trim();
            plantilla.Asunto = asunto.Trim();
            plantilla.Mensaje = mensaje.Trim();
            plantilla.Icono = string.IsNullOrWhiteSpace(icono) ? "fa-regular fa-bell" : icono;
            plantilla.Color = string.IsNullOrWhiteSpace(color) ? "purple" : color;
            plantilla.Canal = "Correo Electrónico";

            _context.SaveChanges();

            return RedirectToAction("CentroNotificaciones");
        }

        [HttpPost]
        public IActionResult EliminarPlantillaNotificacion(int id)
        {
            var plantilla = _context.PLANTILLA_NOTIFICACION
                .FirstOrDefault(x => x.Id_Plantilla == id);

            if (plantilla != null)
            {
                plantilla.Activo = false;
                _context.SaveChanges();
            }

            return RedirectToAction("CentroNotificaciones");
        }
        private string GenerarHtmlNotificacion(string asunto, string destino, string mensaje)
        {
            return $@"
<div style='background:#f8f5ff;padding:30px;font-family:Arial,sans-serif;'>
    <div style='max-width:650px;margin:auto;background:white;border-radius:22px;overflow:hidden;box-shadow:0 15px 40px rgba(109,40,217,.18);'>

        <div style='background:#6D28D9;color:white;text-align:center;padding:28px;'>
            <h1 style='margin:0;font-size:28px;'>CrediPlus</h1>
            <p style='margin:8px 0 0;font-size:14px;'>Centro de Notificaciones</p>
        </div>

        <div style='padding:30px;color:#241033;'>
            <h2 style='margin-top:0;color:#241033;font-size:22px;'>{asunto}</h2>

            <p style='font-size:15px;color:#4b3a5c;'>
                Se ha generado una nueva notificación para:
            </p>

            <div style='background:#f3ecff;border-radius:14px;padding:14px;margin:15px 0;color:#6D28D9;font-weight:bold;'>
                {destino}
            </div>

            <div style='background:#faf7ff;border-left:5px solid #6D28D9;padding:18px;border-radius:14px;margin:20px 0;'>
                <p style='font-size:16px;line-height:1.7;margin:0;color:#4b3a5c;'>
                    {mensaje}
                </p>
            </div>

            <p style='font-size:13px;color:#8b7b9c;line-height:1.6;'>
                Este correo fue enviado automáticamente por CrediPlus. Por favor no responder este mensaje.
            </p>

            <div style='margin-top:25px;text-align:center;background:#f3ecff;border-radius:14px;padding:16px;'>
                <strong style='color:#6D28D9;'>CrediPlus</strong><br/>
                <span style='font-size:13px;color:#7b6a8e;'>Gestión inteligente de créditos</span>
            </div>
        </div>
    </div>
</div>";
        }

        public IActionResult CronogramaCreditoExcel(int idSolicitud)
        {
            var solicitud = _context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .FirstOrDefault(x => x.Id_Solicitud == idSolicitud && x.Estado == "Aprobado");

            if (solicitud == null)
                return RedirectToAction("Creditos");

            var cuotas = _context.CUOTA
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud)
                .OrderBy(x => x.FechaLimitePago)
                .ToList();

            var pagos = _context.PAGO_CUOTA.ToList();
            DateTime hoy = DateTime.Today;

            using var workbook = new XLWorkbook();
            var hoja = workbook.Worksheets.Add("Cronograma");

            hoja.Cell(1, 1).Value = "CREDIPLUS - CRONOGRAMA DE PAGOS";
            hoja.Range("A1:F1").Merge();
            hoja.Cell(1, 1).Style.Font.Bold = true;
            hoja.Cell(1, 1).Style.Font.FontSize = 16;

            hoja.Cell(3, 1).Value = "Cliente:";
            hoja.Cell(3, 2).Value = $"{solicitud.USUARIO.Nombre} {solicitud.USUARIO.Apellido}";
            hoja.Cell(4, 1).Value = "Solicitud:";
            hoja.Cell(4, 2).Value = solicitud.NumeroSolicitud;
            hoja.Cell(5, 1).Value = "Monto:";
            hoja.Cell(5, 2).Value = solicitud.MontoSolicitado;

            hoja.Cell(7, 1).Value = "N°";
            hoja.Cell(7, 2).Value = "Fecha límite";
            hoja.Cell(7, 3).Value = "Monto cuota";
            hoja.Cell(7, 4).Value = "Estado";
            hoja.Cell(7, 5).Value = "Leyenda";
            hoja.Range("A7:E7").Style.Font.Bold = true;

            int fila = 8;
            int numero = 1;

            foreach (var cuota in cuotas)
            {
                bool pagado = pagos.Any(p => p.Id_Cuota == cuota.Id_Cuota && p.Estado == "Aprobado");

                string estado;
                string color;

                if (pagado)
                {
                    estado = "Pagado";
                    color = "#DCFCE7"; // verde
                }
                else if (cuota.FechaLimitePago.Date < hoy)
                {
                    estado = "Mora";
                    color = "#FEE2E2"; // rojo
                }
                else if (cuota.FechaLimitePago.Month == hoy.Month &&
                         cuota.FechaLimitePago.Year == hoy.Year)
                {
                    estado = "Cuota actual";
                    color = "#FEF3C7"; // amarillo
                }
                else
                {
                    estado = "Pendiente futuro";
                    color = "#FFEDD5"; // anaranjado claro
                }

                hoja.Cell(fila, 1).Value = numero;
                hoja.Cell(fila, 2).Value = cuota.FechaLimitePago.ToString("dd/MM/yyyy");
                hoja.Cell(fila, 3).Value = cuota.MontoCuota ?? 0;
                hoja.Cell(fila, 4).Value = estado;
                hoja.Cell(fila, 5).Value = estado;

                hoja.Range(fila, 1, fila, 5).Style.Fill.BackgroundColor = XLColor.FromHtml(color);

                fila++;
                numero++;
            }

            hoja.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Cronograma_{solicitud.NumeroSolicitud}.xlsx"
            );
        }
        public IActionResult CronogramaCreditoPdf(int idSolicitud)
        {
            var solicitud = _context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .FirstOrDefault(x => x.Id_Solicitud == idSolicitud && x.Estado == "Aprobado");

            if (solicitud == null)
                return RedirectToAction("Creditos");

            var cuotas = _context.CUOTA
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud)
                .OrderBy(x => x.FechaLimitePago)
                .ToList();

            var pagos = _context.PAGO_CUOTA.ToList();
            DateTime hoy = DateTime.Today;

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);

                    page.Header().Column(col =>
                    {
                        col.Item().Text("CREDIPLUS")
                            .FontSize(24)
                            .Bold()
                            .FontColor("#6D28D9");

                        col.Item().Text("Cronograma de Pagos")
                            .FontSize(16)
                            .Bold();

                        col.Item().PaddingTop(10).Text($"Cliente: {solicitud.USUARIO.Nombre} {solicitud.USUARIO.Apellido}");
                        col.Item().Text($"DNI: {solicitud.USUARIO.Dni}");
                        col.Item().Text($"Solicitud: {solicitud.NumeroSolicitud}");
                        col.Item().Text($"Monto aprobado: S/ {solicitud.MontoSolicitado:N2}");
                    });

                    page.Content().PaddingTop(20).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(40);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("N°").Bold();
                            header.Cell().Text("Fecha límite").Bold();
                            header.Cell().Text("Monto").Bold();
                            header.Cell().Text("Estado").Bold();
                        });

                        int numero = 1;

                        foreach (var cuota in cuotas)
                        {
                            bool pagado = pagos.Any(p => p.Id_Cuota == cuota.Id_Cuota && p.Estado == "Aprobado");

                            string estado;
                            string color;

                            if (pagado)
                            {
                                estado = "Pagado";
                                color = "#DCFCE7"; // verde
                            }
                            else if (cuota.FechaLimitePago.Date < hoy)
                            {
                                estado = "Mora";
                                color = "#FEE2E2"; // rojo
                            }
                            else if (cuota.FechaLimitePago.Month == hoy.Month &&
                                     cuota.FechaLimitePago.Year == hoy.Year)
                            {
                                estado = "Cuota actual";
                                color = "#FEF3C7"; // amarillo
                            }
                            else
                            {
                                estado = "Pendiente futuro";
                                color = "#FFEDD5"; // anaranjado claro
                            }

                            table.Cell().Background(color).Padding(6).Text(numero.ToString());
                            table.Cell().Background(color).Padding(6).Text(cuota.FechaLimitePago.ToString("dd/MM/yyyy"));
                            table.Cell().Background(color).Padding(6).Text($"S/ {(cuota.MontoCuota ?? 0):N2}");
                            table.Cell().Background(color).Padding(6).Text(estado);

                            numero++;
                        }
                    });

                    page.Footer().AlignCenter().Text("Generado por CrediPlus");
                });
            });

            return File(pdf.GeneratePdf(), "application/pdf");
        }

        public IActionResult ChatAnalista()
        {
            CargarDatosAdministrador();

            var analista = _context.Usuario.FirstOrDefault(x => x.Rol == "Analista");
            ViewBag.Analista = analista;

            return View();
        }

        [HttpGet]
        public IActionResult ObtenerMensajesAdminAnalista()
        {
            var mensajes = _context.MENSAJE_ADMIN_ANALISTA
                .OrderBy(x => x.FechaEnvio)
                .ToList();

            return Json(mensajes);
        }

        [HttpPost]
        public IActionResult GuardarMensajeAdminAnalista(string mensaje)
        {
            var admin = _context.Usuario.FirstOrDefault(x => x.Rol == "Administrador");
            var analista = _context.Usuario.FirstOrDefault(x => x.Rol == "Analista");

            if (admin == null || analista == null || string.IsNullOrWhiteSpace(mensaje))
                return Json(new { ok = false });

            var nuevo = new MensajeAdminAnalista
            {
                IdAdministrador = admin.Id,
                IdAnalista = analista.Id,
                RemitenteRol = "Administrador",
                Mensaje = mensaje,
                FechaEnvio = DateTime.Now,
                Leido = false
            };

            _context.MENSAJE_ADMIN_ANALISTA.Add(nuevo);
            _context.SaveChanges();

            return Json(new { ok = true });
        }
        [HttpGet]
        public IActionResult EstadoAnalistaActual()
        {
            var analista = _context.Usuario.FirstOrDefault(x => x.Rol == "Analista");

            bool activo = analista != null &&
                          analista.EstadoActivo &&
                          analista.UltimaConexion >= DateTime.Now.AddMinutes(-2);

            return Json(new
            {
                activo = activo,
                texto = activo ? "Activo" : "Inactivo"
            });
        }
    }
}
    

