using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using trabajo.Models;
using trabajo.Models.ViewModels;
using trabajo.Service;
using trabajo.Models.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;

namespace trabajo.Controllers
{
    [Authorize]
    public class AnalistaController : Controller
    {
        private readonly UsuarioContext _context;
        private static readonly Random rnd = new Random();
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService = new EmailService();
        private static string codigoPerfilAnalista = "";

        public AnalistaController(
            UsuarioContext context,
            IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public IActionResult ProgramaAnalista()
        {
            var solicitudes = _context.SOLICITUD_CREDITO
                .Include(x => x.PERFIL_FINANCIERO)
                .Include(x => x.USUARIO)
                .Include(x => x.HISTORIAL_ESTADOS)
                .ToList();

            string dni = User.FindFirst("Dni")?.Value;

            var analista = _context.Usuario
                .FirstOrDefault(x => x.Dni == dni);

            ViewBag.Analista = analista;

            ActualizarReporteDashboard();
            DateTime hoy = DateTime.Today;

            DateTime ultimaVistaAlertas = DateTime.MinValue;

            var ultimaVistaTexto = HttpContext.Session.GetString("UltimaVistaAlertas");

            if (!string.IsNullOrWhiteSpace(ultimaVistaTexto))
            {
                DateTime.TryParse(ultimaVistaTexto, out ultimaVistaAlertas);
            }

            int criticasHoy = _context.SOLICITUD_CREDITO
                .Count(x =>
                    x.Estado == "Pendiente" &&
                    x.FechaSolicitud.Date == hoy &&
                    !x.NotificacionVistaAnalista &&
                    x.FechaSolicitud > ultimaVistaAlertas);

            int mensajesNuevos = _context.MENSAJE
                .Count(x =>
                    x.Remitente != "Analista" &&
                    !x.Leido &&
                    x.FechaEnvio > ultimaVistaAlertas);

            int calificacionesHoy = _context.ComentarioClientes
                .Count(x =>
                    x.FechaComentario.Date == hoy &&
                    !x.VistaAnalista &&
                    x.FechaComentario > ultimaVistaAlertas);

            ViewBag.TotalNoLeidos = criticasHoy + mensajesNuevos + calificacionesHoy;
            int tiposActivos = 0;

            if (criticasHoy > 0) tiposActivos++;
            if (mensajesNuevos > 0) tiposActivos++;
            if (calificacionesHoy > 0) tiposActivos++;

            if (criticasHoy > 0 || tiposActivos >= 2)
            {
                ViewBag.ColorNotificacion = "#ef4444"; 
            }
            else if (mensajesNuevos > 0)
            {
                ViewBag.ColorNotificacion = "#2563eb"; 
            }
            else if (calificacionesHoy > 0)
            {
                ViewBag.ColorNotificacion = "#7c3aed"; 
            }
            else
            {
                ViewBag.ColorNotificacion = "#ef4444";
            }
            return View(solicitudes);
        }
        public IActionResult SolicitudesPendientes()
        {
            var pendientes = _context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .Include(x => x.HISTORIAL_ESTADOS)
                .Where(x => x.Estado == "Pendiente")
                .AsEnumerable()
                .OrderByDescending(x => x.HISTORIAL_ESTADOS?
                    .OrderByDescending(h => h.FechaCambio)
                    .FirstOrDefault()?.FechaCambio ?? x.FechaSolicitud)
                .ToList();

            var evaluacion = _context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .Include(x => x.HISTORIAL_ESTADOS)
                .Where(x => x.Estado == "En Evaluación")
                .AsEnumerable()
                .OrderByDescending(x => x.HISTORIAL_ESTADOS?
                    .OrderByDescending(h => h.FechaCambio)
                    .FirstOrDefault()?.FechaCambio ?? x.FechaSolicitud)
                .ToList();

            ViewBag.EnEvaluacion = evaluacion;

            return View(pendientes);
        }
        public IActionResult VerSolicitud(int id, bool abrirMensaje = false)
        {
            var solicitud = _context.SOLICITUD_CREDITO
    .Include(x => x.USUARIO)
    .Include(x => x.PERFIL_FINANCIERO)
    .Include(x => x.MENSAJES)
    .FirstOrDefault(x => x.Id_Solicitud == id);


            if (solicitud == null)
            {
                return NotFound();
            }

            var historial = _context.HISTORIAL_CREDITO
          .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == id)
          .ToList();

            ViewBag.Historial = historial;
            var resumen = _context.RESUMEN_CREDITICIO
.FirstOrDefault(x => x.Usuario_Id == solicitud.Usuario_Id_Usuario);

            ViewBag.Resumen = resumen;
            ViewBag.YaValidado =
_context.HISTORIAL_CREDITO
.Any(x =>
    x.SOLICITUD_CREDITO_Id_Solicitud == id);
            ViewBag.AbrirMensaje = abrirMensaje;
            return View(solicitud);
        }

        [HttpPost]
        public IActionResult ValidarCliente(int idSolicitud)
        {

            var historialGuardado = _context.HISTORIAL_CREDITO
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud)
                .ToList();
            Console.WriteLine("Historial encontrado: " + historialGuardado.Count);

            var solicitud = _context.SOLICITUD_CREDITO
.Include(x => x.PERFIL_FINANCIERO)
.FirstOrDefault(x => x.Id_Solicitud == idSolicitud);

            if (solicitud == null)
            {
                return BadRequest("La solicitud no existe");
            }
            var resumenExistente = _context.RESUMEN_CREDITICIO
    .FirstOrDefault(x => x.Usuario_Id == solicitud.Usuario_Id_Usuario);
            int activas = 0;
            int pagadas = 0;
            int vencidas = 0;

            bool tieneOtrosCreditos =
            solicitud.PERFIL_FINANCIERO.OtrosCreditos;
            int totalHistorial = 0;
            if (resumenExistente != null)
            {
                System.Diagnostics.Debug.WriteLine("LEYENDO BD");


                return Json(new
                {
                    activas = resumenExistente.DeudasActivas,
                    pagadas = resumenExistente.DeudasPagadas,
                    vencidas = resumenExistente.DeudasVencidas,
                    ultimaFecha = resumenExistente.UltimaActualizacion
            .ToString("dd/MM/yyyy")
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("LEYENDO BD");


                if (tieneOtrosCreditos)
                {
                    bool critico = rnd.Next(2) == 0;

                    if (critico)
                    {
                        activas = rnd.Next(1, 5);
                        pagadas = rnd.Next(1, 5);
                        vencidas = rnd.Next(1, 4);
                    }
                    else
                    {
                        activas = rnd.Next(1, 5);
                        pagadas = rnd.Next(1, 5);
                        vencidas = 0;
                    }
                }
                else
                {
                    bool bajo = rnd.Next(2) == 0;

                    if (bajo)
                    {
                        activas = 0;
                        pagadas = rnd.Next(1, 6);
                        vencidas = 0;
                    }
                    else
                    {
                        activas = 0;
                        pagadas = 0;
                        vencidas = 0;
                    }
                }
                totalHistorial =
        activas + pagadas + vencidas;
            }



            string[] tipos =
{
    "Préstamo Personal",
    "Crédito Hipotecario",
    "Crédito Vehicular",
    "Crédito Empresarial",
    "Crédito de Consumo",
    "Tarjeta de Crédito",
    "Línea de Crédito",
    "Crédito Educativo",
    "Microcrédito",
    "Crédito Comercial",
    "Crédito Agropecuario",
    "Crédito para Negocio",
    "Crédito Capital de Trabajo",
    "Crédito para Remodelación",
    "Crédito para Equipamiento"
};
            string[] bancos =
{
    "BCP",
    "Interbank",
    "BBVA",
    "Scotiabank",
    "Banco Pichincha",
    "Banco Falabella",
    "Mibanco",
    "Banco de Comercio",
    "BanBif",
    "Santander",
    "Compartamos Financiera",
    "Caja Arequipa",
    "Caja Huancayo",
    "Caja Piura",
    "Financiera Crediscotia"
};


            int[] plazos =
{
    6,
    12,
    18,
    24,
    30,
    36,
    42,
    48,
    54,
    60,
    72,
    84,
    96,
    108,
    120
};











            DateTime ultimaActualizacion = DateTime.MinValue;
            List<string> estadosHistorial = new List<string>();

            for (int i = 0; i < pagadas; i++)
            {
                estadosHistorial.Add("PAGADA");
            }

            for (int i = 0; i < activas; i++)
            {
                estadosHistorial.Add("ACTIVA");
            }

            for (int i = 0; i < vencidas; i++)
            {
                estadosHistorial.Add("VENCIDA");
            }

            for (int i = 0; i < totalHistorial; i++)
            {
                string banco =
           bancos[rnd.Next(bancos.Length)];

                string tipoCredito = tipos[rnd.Next(tipos.Length)];
                int monto;

                switch (tipoCredito)
                {
                    case "Crédito Hipotecario":
                        monto = rnd.Next(20, 101) * 1000;
                        break;

                    case "Crédito Empresarial":
                        monto = rnd.Next(15, 81) * 1000;
                        break;

                    case "Crédito Vehicular":
                        monto = rnd.Next(10, 61) * 1000;
                        break;

                    case "Préstamo Personal":
                        monto = rnd.Next(5, 31) * 1000;
                        break;

                    case "Tarjeta de Crédito":
                        monto = rnd.Next(1, 16) * 1000;
                        break;

                    case "Línea de Crédito":
                        monto = rnd.Next(3, 26) * 1000;
                        break;

                    case "Crédito Educativo":
                        monto = rnd.Next(5, 31) * 1000;
                        break;

                    case "Microcrédito":
                        monto = rnd.Next(1, 11) * 1000;
                        break;

                    case "Crédito Comercial":
                        monto = rnd.Next(10, 101) * 1000;
                        break;

                    case "Crédito Agropecuario":
                        monto = rnd.Next(5, 51) * 1000;
                        break;

                    default:
                        monto = rnd.Next(3, 21) * 1000;
                        break;
                }
                int plazoMeses;

                switch (tipoCredito)
                {
                    case "Crédito Hipotecario":
                        plazoMeses = plazos[rnd.Next(8, plazos.Length)];
                        break;

                    case "Crédito Vehicular":
                        plazoMeses = plazos[rnd.Next(3, 11)];
                        break;

                    case "Crédito Empresarial":
                        plazoMeses = plazos[rnd.Next(2, plazos.Length)];
                        break;

                    case "Tarjeta de Crédito":
                        plazoMeses = plazos[rnd.Next(0, 4)];
                        break;

                    default:
                        plazoMeses = plazos[rnd.Next(plazos.Length)];
                        break;
                }

                DateTime fechaInicio;
                DateTime fechaFin;
                string estadoCredito = estadosHistorial[i];


                if (estadoCredito == "ACTIVA")
                {
                    int dia = rnd.Next(1, 28);

                    fechaInicio = new DateTime(
                        rnd.Next(2023, 2026),
                        rnd.Next(1, 13),
                        dia);

                    int plazoMesesCredito = plazoMeses;

                    DateTime fechaVencimiento =
                        fechaInicio.AddMonths(plazoMesesCredito);

                    DateTime hoy = DateTime.Today;

                    int cuotasPagadas =
                        ((hoy.Year - fechaInicio.Year) * 12)
                        + hoy.Month
                        - fechaInicio.Month;

                    if (cuotasPagadas >= plazoMesesCredito)
                        cuotasPagadas = plazoMesesCredito - 1;

                    if (cuotasPagadas < 1)
                        cuotasPagadas = 1;

                    fechaFin =
                        fechaInicio.AddMonths(cuotasPagadas);
                }
                else
                {
                    fechaInicio = new DateTime(
                        rnd.Next(2009, 2022),
                        rnd.Next(1, 13),
                        rnd.Next(1, 28));

                    fechaFin =
                        fechaInicio.AddMonths(plazoMeses);

                    while (fechaFin.Year > 2024)
                    {
                        fechaInicio =
                            fechaInicio.AddYears(-1);

                        fechaFin =
                            fechaInicio.AddMonths(plazoMeses);
                    }
                }



                if (totalHistorial > 0)
                {



                    if (fechaFin > ultimaActualizacion)
                    {
                        ultimaActualizacion = fechaFin;
                    }

                    int cuotasPagadas;

                    if (estadoCredito == "PAGADA")
                    {
                        cuotasPagadas = plazoMeses;
                    }
                    else if (estadoCredito == "ACTIVA")
                    {
                        cuotasPagadas =
                            ((fechaFin.Year - fechaInicio.Year) * 12)
                            + fechaFin.Month
                            - fechaInicio.Month;

                        if (cuotasPagadas < 1)
                            cuotasPagadas = 1;
                    }
                    else
                    {
                        cuotasPagadas =
                            rnd.Next(1, Math.Max(2, plazoMeses / 2));
                    }
                    var historial = new HistorialCredito
                    {
                        Banco = banco,
                        TipoCredito = tipoCredito,

                        Monto = monto,
                        PlazoMeses = plazoMeses,
                        CuotasPagadas = cuotasPagadas,

                        EstadoCredito = estadoCredito,

                        FechaInicio = fechaInicio,
                        FechaFin = fechaFin,

                        SOLICITUD_CREDITO_Id_Solicitud = idSolicitud

                    };
                    _context.HISTORIAL_CREDITO.Add(historial);
                }
            }

            var resumen =
    _context.RESUMEN_CREDITICIO
    .FirstOrDefault(x =>
        x.Usuario_Id ==
        solicitud.Usuario_Id_Usuario);

            if (resumen == null)
            {
                resumen = new ResumenCredito
                {
                    Usuario_Id = solicitud.Usuario_Id_Usuario
                };

                _context.RESUMEN_CREDITICIO.Add(resumen);
            }

            resumen.DeudasActivas = activas;
            resumen.DeudasPagadas = pagadas;
            resumen.DeudasVencidas = vencidas;
            resumen.UltimaActualizacion = ultimaActualizacion;

            _context.SaveChanges();
            return Json(new
            {
                activas,
                pagadas,
                vencidas,
                ultimaFecha = ultimaActualizacion.ToString("dd/MM/yyyy")
            });

        }
        [HttpGet]
        public IActionResult ObtenerHistorialCredito(int idSolicitud)
        {
            var solicitud = _context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .FirstOrDefault(x => x.Id_Solicitud == idSolicitud);
            var ultimaFecha = _context.HISTORIAL_CREDITO
    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud)
    .Max(x => x.FechaFin);
            if (solicitud == null)
            {
                return Json(new { ok = false });
            }

            var historial = _context.HISTORIAL_CREDITO
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud)
                .Select(x => new
                {
                    banco = x.Banco,
                    tipoCredito = x.TipoCredito,
                    monto = x.Monto,
                    plazoMeses = x.PlazoMeses,
                    cuotasPagadas = x.CuotasPagadas,
                    estadoCredito = x.EstadoCredito,

                    fechaInicio = x.FechaInicio.HasValue
        ? x.FechaInicio.Value.ToString("dd/MM/yyyy")
        : "",

                    fechaFin = x.FechaFin.HasValue
        ? x.FechaFin.Value.ToString("dd/MM/yyyy")
        : ""
                })
                .ToList();
            System.Diagnostics.Debug.WriteLine(
     string.Join(", ", historial.Select(x => x.estadoCredito))
 );
            return Json(new
            {
                ok = true,
                nombre = solicitud.USUARIO.Nombre,
                apellido = solicitud.USUARIO.Apellido,
                dni = solicitud.USUARIO.Dni,
                historial
            });
        }
        [HttpPost]
        public IActionResult RechazarSolicitud(
    int idSolicitud,
    string comentario,
    string nivelRiesgo)
        {
            var solicitud = _context.SOLICITUD_CREDITO
                .FirstOrDefault(s => s.Id_Solicitud == idSolicitud);

            if (solicitud == null)
            {
                return Json(new
                {
                    success = false,
                    mensaje = "Solicitud no encontrada."
                });
            }
            solicitud.Estado = "Rechazado";


            var perfil = _context.PERFIL_FINANCIERO
                .FirstOrDefault(p =>
                    p.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

            if (perfil != null)
            {
                perfil.NivelRiesgo = nivelRiesgo;
            }


            var historial = new HistorialEstado
            {
                EstadoActual = "Rechazado",
                MotivoCambio = "La solicitud del cliente fue rechazada por el analista.",
                FechaCambio = DateTime.Now,
                SOLICITUD_CREDITO_Id_Solicitud = idSolicitud
            };

            _context.HISTORIAL_ESTADO.Add(historial);


            var evaluacion = _context.Evaluacion_Riesgo
    .FirstOrDefault(x =>
        x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

            if (evaluacion == null)
            {
                evaluacion = new Evaluacion_Riesgo
                {
                    SOLICITUD_CREDITO_Id_Solicitud = idSolicitud
                };

                _context.Evaluacion_Riesgo.Add(evaluacion);
            }

            evaluacion.Resultado = "Rechazado";
            evaluacion.Observacion = comentario;
            evaluacion.FechaEvaluacion = DateTime.Now;
            evaluacion.Responsable = "Analista de riesgo";


            _context.SaveChanges();
            ActualizarReporteDashboard();

            return Json(new
            {
                success = true
            });
        }
        private void ActualizarReporteDashboard()
        {
            var solicitudes = _context.SOLICITUD_CREDITO
                .Include(x => x.PERFIL_FINANCIERO)
                .ToList();

            var reporte = _context.ReporteRiesgo.FirstOrDefault();

            if (reporte == null)
            {
                reporte = new ReporteRiesgo();

                _context.ReporteRiesgo.Add(reporte);
            }

            reporte.FechaReporte = DateTime.Today;

            reporte.Bajo = solicitudes.Count(x =>
                x.PERFIL_FINANCIERO != null &&
                x.PERFIL_FINANCIERO.NivelRiesgo == "Bajo");

            reporte.Medio = solicitudes.Count(x =>
                x.PERFIL_FINANCIERO != null &&
                x.PERFIL_FINANCIERO.NivelRiesgo == "Medio");

            reporte.Alto = solicitudes.Count(x =>
                x.PERFIL_FINANCIERO != null &&
                x.PERFIL_FINANCIERO.NivelRiesgo == "Alto");

            reporte.Critico = solicitudes.Count(x =>
                x.PERFIL_FINANCIERO != null &&
                x.PERFIL_FINANCIERO.NivelRiesgo == "Critico");

            reporte.TotalAprobados = solicitudes.Count(x =>
                x.Estado == "Aprobado");

            reporte.TotalRechazados = solicitudes.Count(x =>
                x.Estado == "Rechazado");

            reporte.TotalClientesAtendidos =
                reporte.TotalAprobados +
                reporte.TotalRechazados;

            _context.SaveChanges();
        }
        [HttpPost]
        public IActionResult AprobarSolicitud(
    int idSolicitud,
    string comentario,
    string nivelRiesgo)
        {
            try
            {
                var solicitud = _context.SOLICITUD_CREDITO
    .Include(x => x.USUARIO)
    .FirstOrDefault(x => x.Id_Solicitud == idSolicitud);

                if (solicitud == null)
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = "No se encontró la solicitud."
                    });
                }

                solicitud.Estado = "Aprobado";
                var cuotasAnteriores = _context.CUOTA
    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == solicitud.Id_Solicitud)
    .ToList();

                _context.CUOTA.RemoveRange(cuotasAnteriores);

                var cronogramasAnteriores = _context.CRONOGRAMA
                    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == solicitud.Id_Solicitud)
                    .ToList();

                _context.CRONOGRAMA.RemoveRange(cronogramasAnteriores);
                decimal capitalMensual =
    Math.Round(
        solicitud.MontoSolicitado /
        solicitud.PlazoMeses,
        2);

                decimal interesTotal =
                    solicitud.MontoSolicitado *
                    (solicitud.InteresEstimado / 100);

                decimal interesMensual =
                    Math.Round(
                        interesTotal /
                        solicitud.PlazoMeses,
                        2);

                decimal cuotaMensual =
                    capitalMensual + interesMensual;

                decimal saldo =
                    solicitud.MontoSolicitado;
                for (int i = 1; i <= solicitud.PlazoMeses; i++)
                {
                    saldo -= capitalMensual;

                    if (saldo < 0)
                        saldo = 0;

                    DateTime vencimiento =
                        DateTime.Now.AddMonths(i);

                    var cuota = new Cuota
                    {
                        NumeroCuota = i,

                        MontoCuota = cuotaMensual,

                        SaldoPendiente = saldo,

                        FechaVencimiento = vencimiento,

                        Dias = 30,

                        Capital = capitalMensual,

                        Interes = interesMensual,

                        Comisiones = 0,

                        Seguros = 0,

                        FechaLimitePago =
                            CalcularFechaLimite(vencimiento, 15),

                        Estado = "Pendiente",

                        SOLICITUD_CREDITO_Id_Solicitud =
                            solicitud.Id_Solicitud
                    };

                    _context.CUOTA.Add(cuota);
                }

                var evaluacion = _context.Evaluacion_Riesgo
     .FirstOrDefault(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

                if (evaluacion == null)
                {
                    evaluacion = new Evaluacion_Riesgo
                    {
                        SOLICITUD_CREDITO_Id_Solicitud = idSolicitud
                    };

                    _context.Evaluacion_Riesgo.Add(evaluacion);
                }

                evaluacion.Resultado = "Aprobado";
                evaluacion.Observacion = comentario;
                evaluacion.FechaEvaluacion = DateTime.Now;
                evaluacion.Responsable = "Analista de riesgo";
                var perfil = _context.PERFIL_FINANCIERO
    .FirstOrDefault(x =>
        x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

                if (perfil != null)
                {
                    perfil.NivelRiesgo = nivelRiesgo;
                }

                var historial = new HistorialEstado
                {
                    EstadoActual = "Aprobado",
                    MotivoCambio = comentario,
                    FechaCambio = DateTime.Now,
                    SOLICITUD_CREDITO_Id_Solicitud = idSolicitud
                };

                _context.HISTORIAL_ESTADO.Add(historial);

                var cronograma = new Cronograma
                {
                    CorreoDestino = solicitud.USUARIO.Correo,
                    FechaEnvio = DateTime.Now,
                    EstadoEnvio = "Enviado",
                    NumeroOperacion = GenerarNumeroOperacion(),
                    SOLICITUD_CREDITO_Id_Solicitud = solicitud.Id_Solicitud
                };

                _context.CRONOGRAMA.Add(cronograma);
                _context.SaveChanges();

                return Json(new
                {
                    success = true
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    mensaje = ex.ToString()
                });
            }
        }
        private DateTime CalcularFechaLimite(DateTime fechaInicio, int diasPlazo)
        {
            DateTime fecha = fechaInicio;
            int diasContados = 0;

            while (diasContados < diasPlazo)
            {
                fecha = fecha.AddDays(1);

                if (fecha.DayOfWeek != DayOfWeek.Sunday)
                {
                    diasContados++;
                }
            }

            return fecha;
        }
        private string GenerarNumeroOperacion()
        {
            int cantidad = _context.CRONOGRAMA.Count() + 1;

            return $"CRONO-{cantidad:D4}";
        }

        [HttpGet]
        public JsonResult ObtenerMensajes(int idSolicitud)
        {
            var solicitud = _context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .FirstOrDefault(x => x.Id_Solicitud == idSolicitud);

            var nombreAnalista = User.FindFirst(ClaimTypes.Name)?.Value;
            var apellidoAnalista = User.FindFirst("Apellido")?.Value;

            var nombreCompletoAnalista =
                nombreAnalista + " " + apellidoAnalista;

            var mensajes = _context.MENSAJE
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud)
                .OrderBy(x => x.FechaEnvio)
                .Select(x => new
                {
                    mensaje = x.MensajeTexto,
                    fecha = x.FechaEnvio,
                    tipoUsuario = x.Remitente,
                    imagen = x.Imagen,

                    nombre = x.Remitente == "Usuario"
                        ? solicitud.USUARIO.Nombre + " " + solicitud.USUARIO.Apellido
                        : nombreCompletoAnalista
                })
                .ToList();

            return Json(mensajes);
        }
        [HttpPost]
        public async Task<JsonResult> EnviarMensaje(
    int idSolicitud,
    string? mensaje,
    IFormFile? imagen)
        {
            string rutaImagen = null;

            if (imagen != null)
            {
                string nombreArchivo =
                    Guid.NewGuid().ToString() +
                    Path.GetExtension(imagen.FileName);

                string carpeta =
                    Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        "imagenesChat");

                if (!Directory.Exists(carpeta))
                {
                    Directory.CreateDirectory(carpeta);
                }

                string rutaCompleta =
                    Path.Combine(carpeta, nombreArchivo);

                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await imagen.CopyToAsync(stream);
                }

                rutaImagen = "/imagenesChat/" + nombreArchivo;
            }

            Mensaje nuevo = new Mensaje();

            nuevo.SOLICITUD_CREDITO_Id_Solicitud = idSolicitud;
            nuevo.MensajeTexto =
                string.IsNullOrWhiteSpace(mensaje)
                ? "[Imagen]"
                : mensaje;

            nuevo.Imagen = rutaImagen;

            nuevo.Remitente = "Analista";
            nuevo.FechaEnvio = DateTime.Now;
            nuevo.Leido = false;

            _context.MENSAJE.Add(nuevo);
            _context.SaveChanges();

            return Json(new
            {
                ok = true
            });
        }
        [HttpGet]
        public JsonResult ObtenerInfoSolicitud(int idSolicitud)
        {
            var solicitud = _context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .FirstOrDefault(x => x.Id_Solicitud == idSolicitud);

            if (solicitud == null)
            {
                return Json(new
                {
                    numeroSolicitud = "",
                    cliente = "",
                    estado = ""
                });
            }

            return Json(new
            {
                idRecibido = idSolicitud,
                numeroSolicitud = solicitud.Id_Solicitud.ToString("00000"),
                cliente = solicitud.USUARIO.Nombre + " " + solicitud.USUARIO.Apellido,
                estado = solicitud.Estado
            });
        }
        [HttpPost]
        public async Task<IActionResult> EnviarEvaluacion([FromBody] dynamic datos)
        {
            try
            {
                JsonElement json = datos;

                int idSolicitud = json.GetProperty("idSolicitud").GetInt32();
                var solicitud = _context.SOLICITUD_CREDITO
    .FirstOrDefault(x => x.Id_Solicitud == idSolicitud);


                if (solicitud == null)
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = "No se encontró la solicitud."
                    });
                }

                solicitud.Estado = "En Evaluación";
                string comentario = "";

                if (json.TryGetProperty("comentario", out JsonElement comentarioJson))
                {
                    comentario = comentarioJson.GetString();
                }

                var anteriores = _context.PROPUESTA_CREDITO
                    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud)
                    .ToList();

                _context.PROPUESTA_CREDITO.RemoveRange(anteriores);

                foreach (JsonElement propuesta in json.GetProperty("propuestas").EnumerateArray())
                {
                    var nueva = new PropuestaCredito
                    {
                        SOLICITUD_CREDITO_Id_Solicitud = idSolicitud,

                        Monto = propuesta.GetProperty("monto").GetDecimal(),

                        PlazoMeses = propuesta.GetProperty("plazoMeses").GetInt32(),

                        EsRecomendada = propuesta.GetProperty("esRecomendada").GetBoolean(),

                        FechaRegistro = DateTime.Now
                    };

                    _context.PROPUESTA_CREDITO.Add(nueva);
                }
                var historial = new HistorialEstado
                {
                    EstadoActual = "En Evaluación",
                    MotivoCambio = "Se enviaron propuestas de evaluación al cliente.",
                    FechaCambio = DateTime.Now,
                    SOLICITUD_CREDITO_Id_Solicitud = idSolicitud
                };
                _context.HISTORIAL_ESTADO.Add(historial);
                var comentarioFinal = string.IsNullOrWhiteSpace(comentario)
     ? "COMENTARIO VACÍO"
     : comentario.Trim();

                var evaluacion = _context.Evaluacion_Riesgo
                    .FirstOrDefault(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud);

                if (evaluacion == null)
                {
                    evaluacion = new Evaluacion_Riesgo
                    {
                        SOLICITUD_CREDITO_Id_Solicitud = idSolicitud
                    };

                    _context.Evaluacion_Riesgo.Add(evaluacion);
                }

                evaluacion.Resultado = "Evaluado";
                evaluacion.Observacion = comentarioFinal;
                evaluacion.FechaEvaluacion = DateTime.Now;
                evaluacion.Responsable = "Analista de riesgo";
                _context.SaveChanges();
                Console.WriteLine("Observación guardada BD: " + evaluacion.Observacion);
                var listaPropuestas = _context.PROPUESTA_CREDITO
    .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud)
    .ToList();

                var solicitudCorreo = _context.SOLICITUD_CREDITO
    .Include(x => x.USUARIO)
    .FirstOrDefault(x => x.Id_Solicitud == idSolicitud);

                if (solicitudCorreo != null && listaPropuestas.Any())
                {
                    await EnviarCorreoEvaluacion(solicitudCorreo, listaPropuestas);
                }



                return Json(new
                {
                    success = true
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    mensaje = ex.ToString()
                });
            }
        }
        private async Task EnviarCorreoEvaluacion(
    SolicitudCredito solicitud,
    List<PropuestaCredito> propuestas)
        {
            string opcionRecomendada = "";
            string otrasOpciones = "";

            var recomendada = propuestas.FirstOrDefault(x => x.EsRecomendada);

            if (recomendada != null)
            {
                opcionRecomendada = $@"
        <tr>
            <td style='padding:15px;text-align:center;border:1px solid #ddd;'>
                S/ {recomendada.Monto:N0}
            </td>

            <td style='padding:15px;text-align:center;border:1px solid #ddd;'>
                {recomendada.PlazoMeses} meses
            </td>
        </tr>";
            }

            int contador = 1;

            foreach (var opcion in propuestas.Where(x => !x.EsRecomendada))
            {
                otrasOpciones += $@"
        <tr>
            <td>{contador}</td>
            <td>S/ {opcion.Monto:N0}</td>
            <td>{opcion.PlazoMeses} meses</td>
        </tr>";

                contador++;
            }

            string html = $@"
<html>

<body style='font-family:Segoe UI;'>

<div style='max-width:700px;
            margin:auto;
            border:1px solid #ddd;
            border-radius:10px;'>

    <div style='background:#4c1d95;
                color:white;
                padding:15px;'>

        <h2>CrediPlus</h2>

    </div>

    <div style='padding:25px;'>

        <h2 style='color:#4c1d95;'>
            Nuevas opciones de crédito para ti
        </h2>

        <p>
            Hola {solicitud.USUARIO.Nombre} {solicitud.USUARIO.Apellido},
        </p>

        <p>
            El analista ha revisado tu solicitud de crédito
            y te presenta las siguientes alternativas.
        </p>

        <h3 style='color:#4c1d95;'>
            ⭐ OPCIÓN RECOMENDADA
        </h3>

        <table style='width:100%;
                      border-collapse:collapse;'>

            <tr>
                <th>Monto</th>
                <th>Plazo</th>
            </tr>

            {opcionRecomendada}

        </table>

        <br/>

        <h3 style='color:#4c1d95;'>
            📋 OTRAS OPCIONES DISPONIBLES
        </h3>

        <table border='1'
               style='width:100%;
                      border-collapse:collapse;
                      text-align:center;'>

            <tr>
                <th>#</th>
                <th>Monto</th>
                <th>Plazo</th>
            </tr>

            {otrasOpciones}

        </table>

        <br/>
<div style='background:#fff3cd;
            color:#664d03;
            padding:15px;
            border-radius:8px;
            margin-top:20px;
            border:1px solid #ffecb5;'>

    <b>⚠ Advertencia importante:</b>
    <br/>
    Debes registrar información real y correcta en la plataforma.
    Si se detectan datos falsos, incompletos o incorrectos,
    tu solicitud podrá ser observada, rechazada o eliminada del sistema.

</div>

<br/>
        <h3>💡 ¿Qué debes hacer?</h3>

        <ol>
            <li>Ingresa a tu cuenta de CrediPlus.</li>
            <li>Dirígete a Mis Solicitudes.</li>
            <li>Selecciona tu solicitud.</li>
            <li>Revisa las propuestas.</li>
            <li>Elige la que prefieras.</li>
        </ol>

        <p>
            Si no deseas continuar,
            podrás cancelar la solicitud desde la plataforma.
        </p>

        <br/>

        <p>
            Saludos,
            <br/>
            <strong>Equipo CrediPlus</strong>
        </p>

    </div>

</div>

</body>

</html>";
            await _emailService.EnviarCorreoAsync(
    solicitud.USUARIO.Correo,
    "Nuevas propuestas de crédito - CrediPlus",
    html);

        }
        [HttpGet]
        public JsonResult ObtenerReportePropuestas(int idSolicitud)
        {
            var propuestas = _context.PROPUESTA_CREDITO
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud)
                .Select(x => new
                {
                    monto = x.Monto,
                    plazoMeses = x.PlazoMeses,
                    esRecomendada = x.EsRecomendada
                })
                .ToList();

            return Json(new
            {
                ok = true,
                propuestas = propuestas
            });
        }
        public IActionResult CronogramaEvaluacionPdf(int idSolicitud)
        {
            var solicitud = _context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .FirstOrDefault(x => x.Id_Solicitud == idSolicitud && x.Estado == "Aprobado");

            if (solicitud == null)
                return NotFound();

            var cuotas = _context.CUOTA
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud)
                .OrderBy(x => x.FechaLimitePago)
                .ToList();

            if (!cuotas.Any())
                return BadRequest("No existen cuotas para esta solicitud.");

            var pagos = _context.PAGO_CUOTA.ToList();

            DateTime hoy = DateTime.Today;

            int idCuotaActual = cuotas
                .Where(c =>
                    c.FechaLimitePago.Date >= hoy &&
                    !pagos.Any(p => p.Id_Cuota == c.Id_Cuota && p.Estado == "Aprobado"))
                .OrderBy(c => c.FechaLimitePago)
                .Select(c => c.Id_Cuota)
                .FirstOrDefault();

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
                        col.Item().Text($"Solicitud: SOL-{solicitud.Id_Solicitud:D5}");
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
                            else if (cuota.Id_Cuota == idCuotaActual)
                            {
                                estado = "Cuota actual";
                                color = "#FDE047"; // amarillo fuerte
                            }
                            else
                            {
                                estado = "Pendiente futuro";
                                color = "#FB923C"; // naranja más fuerte
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
        public IActionResult CronogramaEvaluacionExcel(int idSolicitud)
        {
            var solicitud = _context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .FirstOrDefault(x => x.Id_Solicitud == idSolicitud && x.Estado == "Aprobado");

            if (solicitud == null)
                return NotFound();

            var cuotas = _context.CUOTA
                .Where(x => x.SOLICITUD_CREDITO_Id_Solicitud == idSolicitud)
                .OrderBy(x => x.FechaLimitePago)
                .ToList();

            if (!cuotas.Any())
                return BadRequest("No existen cuotas para esta solicitud.");

            var pagos = _context.PAGO_CUOTA.ToList();

            DateTime hoy = DateTime.Today;

            int idCuotaActual = cuotas
                .Where(c =>
                    c.FechaLimitePago.Date >= hoy &&
                    !pagos.Any(p => p.Id_Cuota == c.Id_Cuota && p.Estado == "Aprobado"))
                .OrderBy(c => c.FechaLimitePago)
                .Select(c => c.Id_Cuota)
                .FirstOrDefault();

            using var workbook = new XLWorkbook();
            var hoja = workbook.Worksheets.Add("Cronograma");

            hoja.Cell(1, 1).Value = "CREDIPLUS - CRONOGRAMA DE PAGOS";
            hoja.Range("A1:E1").Merge();
            hoja.Cell(1, 1).Style.Font.Bold = true;
            hoja.Cell(1, 1).Style.Font.FontSize = 16;
            hoja.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#6D28D9");

            hoja.Cell(3, 1).Value = "Cliente:";
            hoja.Cell(3, 2).Value = $"{solicitud.USUARIO.Nombre} {solicitud.USUARIO.Apellido}";
            hoja.Cell(4, 1).Value = "DNI:";
            hoja.Cell(4, 2).Value = solicitud.USUARIO.Dni;
            hoja.Cell(5, 1).Value = "Solicitud:";
            hoja.Cell(5, 2).Value = $"SOL-{solicitud.Id_Solicitud:D5}";
            hoja.Cell(6, 1).Value = "Monto aprobado:";
            hoja.Cell(6, 2).Value = $"S/ {solicitud.MontoSolicitado:N2}";

            hoja.Cell(8, 1).Value = "N°";
            hoja.Cell(8, 2).Value = "Fecha límite";
            hoja.Cell(8, 3).Value = "Monto cuota";
            hoja.Cell(8, 4).Value = "Estado";
            hoja.Cell(8, 5).Value = "Leyenda";

            hoja.Range("A8:E8").Style.Font.Bold = true;
            hoja.Range("A8:E8").Style.Fill.BackgroundColor = XLColor.FromHtml("#6D28D9");
            hoja.Range("A8:E8").Style.Font.FontColor = XLColor.White;

            int fila = 9;
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
                else if (cuota.Id_Cuota == idCuotaActual)
                {
                    estado = "Cuota actual";
                    color = "#FDE047"; // amarillo fuerte
                }
                else
                {
                    estado = "Pendiente futuro";
                    color = "#FB923C"; // naranja más fuerte
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
                $"Cronograma_SOL-{solicitud.Id_Solicitud:D5}.xlsx"
            );
        }
        public IActionResult HistorialEvaluaciones()
        {
            var historial = _context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .Include(x => x.PERFIL_FINANCIERO)
                .Include(x => x.HISTORIAL_ESTADOS)
                .Where(x => x.Estado == "Aprobado" || x.Estado == "Rechazado")
                .AsEnumerable()
                .OrderByDescending(x =>
                    x.HISTORIAL_ESTADOS?
                        .OrderByDescending(h => h.FechaCambio)
                        .FirstOrDefault()?.FechaCambio
                    ?? x.FechaSolicitud)
                .ToList();

            ViewBag.Evaluaciones = _context.Evaluacion_Riesgo.ToList();

            return View(historial);
        }
        public IActionResult AlertasNotificaciones()
        {
            string dni = User.FindFirst("Dni")?.Value;
            var analista = _context.Usuario.FirstOrDefault(x => x.Dni == dni);

            if (analista == null)
            {
                return RedirectToAction("IniciarSesion", "Login");
            }

            DateTime hoy = DateTime.Today;

            var viewModel = new AlertasNotificacionesViewModel
            {
                AnalistaNombre = $"{analista.Nombre} {analista.Apellido}",
                AnalistaCorreo = analista.Correo
            };

            // 1. ALERTAS CRÍTICAS: solicitudes pendientes de HOY
            var solicitudesCriticas = _context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .Where(x =>
                    x.Estado == "Pendiente" &&
                    x.FechaSolicitud.Date == hoy &&
                    !x.NotificacionVistaAnalista)
                .ToList();

            foreach (var solicitud in solicitudesCriticas)
            {
                viewModel.Alertas.Add(new AlertaItemViewModel
                {
                    Tipo = "critica",
                    IdOrigen = solicitud.Id_Solicitud,
                    IdSolicitud = solicitud.Id_Solicitud,
                    Prioridad = "critico",
                    PrioridadEtiqueta = "Crítico",
                    AsuntoPrincipal = $"Solicitud nueva - SOL-{solicitud.Id_Solicitud:D5}",
                    AsuntoSubtitulo = "Solicitud recibida hoy.",
                    ClienteNombre = solicitud.USUARIO != null
                        ? $"{solicitud.USUARIO.Nombre} {solicitud.USUARIO.Apellido}"
                        : "Cliente desconocido",
                    MontoSolicitado = solicitud.MontoSolicitado,
                    Fecha = solicitud.FechaSolicitud,
                    UrlOjo = Url.Action("VerSolicitud", "Analista", new { id = solicitud.Id_Solicitud })
                });
            }

            // 2. REVISIÓN PENDIENTE: pendientes de días anteriores
            var revisionesPendientes = _context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .Where(x =>
                    x.Estado == "Pendiente" &&
                    x.FechaSolicitud.Date < hoy)
                .ToList();

            foreach (var solicitud in revisionesPendientes)
            {
                int dias = (hoy - solicitud.FechaSolicitud.Date).Days;

                viewModel.Alertas.Add(new AlertaItemViewModel
                {
                    Tipo = "revision",
                    IdOrigen = solicitud.Id_Solicitud,
                    IdSolicitud = solicitud.Id_Solicitud,
                    Prioridad = "revision",
                    PrioridadEtiqueta = "Revisión Pendiente",
                    AsuntoPrincipal = $"Solicitud pendiente de revisión - SOL-{solicitud.Id_Solicitud:D5}",
                    AsuntoSubtitulo = $"La solicitud lleva {dias} día(s) pendiente.",
                    ClienteNombre = solicitud.USUARIO != null
                        ? $"{solicitud.USUARIO.Nombre} {solicitud.USUARIO.Apellido}"
                        : "Cliente desconocido",
                    MontoSolicitado = solicitud.MontoSolicitado,
                    Fecha = solicitud.FechaSolicitud,
                    UrlOjo = Url.Action("VerSolicitud", "Analista", new { id = solicitud.Id_Solicitud })
                });
            }

            // 3. INFORMATIVAS: mensajes nuevos del cliente
            var mensajesNuevos = _context.MENSAJE
                .Include(m => m.SOLICITUD_CREDITO)
                    .ThenInclude(s => s.USUARIO)
                .Where(x =>
                    x.Remitente != "Analista" &&
                    !x.Leido)
                .ToList();

            foreach (var msg in mensajesNuevos)
            {
                var solicitud = msg.SOLICITUD_CREDITO;

                viewModel.Alertas.Add(new AlertaItemViewModel
                {
                    Tipo = "mensaje",
                    IdOrigen = msg.Id_Mensaje,
                    IdSolicitud = solicitud?.Id_Solicitud ?? 0,
                    Prioridad = "informativa",
                    PrioridadEtiqueta = "Informativa",
                    AsuntoPrincipal = $"Nuevo mensaje - SOL-{(solicitud?.Id_Solicitud ?? 0):D5}",
                    AsuntoSubtitulo = msg.MensajeTexto.Length > 70
                        ? msg.MensajeTexto.Substring(0, 70) + "..."
                        : msg.MensajeTexto,
                    ClienteNombre = solicitud?.USUARIO != null
                        ? $"{solicitud.USUARIO.Nombre} {solicitud.USUARIO.Apellido}"
                        : "Cliente desconocido",
                    MontoSolicitado = solicitud?.MontoSolicitado ?? 0,
                    Fecha = msg.FechaEnvio,
                    UrlOjo = Url.Action("VerSolicitud", "Analista", new { id = solicitud?.Id_Solicitud, abrirMensaje = true })
                });
            }

            // 4. CALIFICACIONES: reseñas de HOY no vistas
            var calificacionesHoy = _context.ComentarioClientes
                .Where(x =>
                    x.FechaComentario.Date == hoy &&
                    !x.VistaAnalista)
                .ToList();

            foreach (var comentario in calificacionesHoy)
            {
                var usuarioComentario = _context.Usuario
                    .FirstOrDefault(u => u.Id == comentario.Usuario_Id);

                var solicitudCliente = _context.SOLICITUD_CREDITO
                    .Where(s => s.Usuario_Id_Usuario == comentario.Usuario_Id)
                    .OrderByDescending(s => s.FechaSolicitud)
                    .FirstOrDefault();

                viewModel.Alertas.Add(new AlertaItemViewModel
                {
                    Tipo = "calificacion",
                    IdOrigen = comentario.Id_Comentario,
                    IdSolicitud = solicitudCliente?.Id_Solicitud ?? 0,
                    Prioridad = "calificacion",
                    PrioridadEtiqueta = "Calificación",
                    AsuntoPrincipal = "Nueva calificación de servicio",
                    AsuntoSubtitulo = comentario.Comentario,
                    ClienteNombre = usuarioComentario != null
                        ? $"{usuarioComentario.Nombre} {usuarioComentario.Apellido}"
                        : "Cliente desconocido",
                    MontoSolicitado = solicitudCliente?.MontoSolicitado ?? 0,
                    Fecha = comentario.FechaComentario,
                    UrlOjo = Url.Action("CalificacionesServicio", "Analista")
                });
            }

            viewModel.Alertas = viewModel.Alertas
    .OrderByDescending(x => x.Fecha)
    .ToList();

            HttpContext.Session.SetString("UltimaVistaAlertas", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            return View(viewModel);
        }
        public IActionResult CalendarioRevisiones()
        {
            var prestamos = _context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .Where(x => x.Estado == "Aprobado")
                .OrderByDescending(x => x.FechaSolicitud)
                .ToList();

            return View(prestamos);
        }
        public JsonResult ObtenerDetallePrestamo(int id)
        {
            var prestamo = _context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .FirstOrDefault(x => x.Id_Solicitud == id);

            if (prestamo == null)
            {
                return Json(new { ok = false });
            }

            return Json(new
            {
                ok = true,
                cliente = prestamo.USUARIO.Nombre + " " + prestamo.USUARIO.Apellido,
                dni = prestamo.USUARIO.Dni,
                correo = prestamo.USUARIO.Correo,
                monto = prestamo.MontoSolicitado.ToString("N2"),
                cuotas = prestamo.PlazoMeses,
                interes = prestamo.InteresEstimado,
                fecha = prestamo.FechaSolicitud.ToString("dd/MM/yyyy"),
                hora = prestamo.FechaSolicitud.ToString("HH:mm"),
                estado = prestamo.Estado
            });
        }
        [HttpGet]
        public IActionResult PerfilAnalista()
        {
            string dni = User.FindFirst("Dni")?.Value;

            var analista = _context.Usuario
                .FirstOrDefault(x => x.Dni == dni);

            if (analista == null)
            {
                return RedirectToAction("IniciarSesion", "Login");
            }

            return View(analista);
        }

        [HttpPost]
        public IActionResult PerfilAnalista(Usuario usuario, string codigoCorreo)
        {
            string dniActual = User.FindFirst("Dni")?.Value;

            var analista = _context.Usuario
                .FirstOrDefault(x => x.Dni == dniActual);

            if (analista == null)
            {
                return RedirectToAction("IniciarSesion", "Login");
            }

            if (usuario.Correo != analista.Correo)
            {
                if (string.IsNullOrWhiteSpace(codigoCorreo) ||
                    codigoCorreo != codigoPerfilAnalista)
                {
                    ViewData["Mensaje"] = "El código de verificación del correo es incorrecto.";
                    ViewData["ModoEditar"] = true;
                    ViewData["CorreoIntentado"] = usuario.Correo;

                    return View(usuario);
                }
            }

            analista.Nombre = usuario.Nombre;
            analista.Apellido = usuario.Apellido;
            analista.Genero = usuario.Genero;
            analista.Dni = usuario.Dni;
            analista.Celular = usuario.Celular;
            analista.Correo = usuario.Correo;

            if (!string.IsNullOrWhiteSpace(usuario.clave))
            {
                analista.clave = utilidades.EncriptarClave(usuario.clave);
            }

            _context.Usuario.Update(analista);
            _context.SaveChanges();

            TempData["MensajeOk"] = "Perfil del analista actualizado correctamente.";

            return RedirectToAction("PerfilAnalista");
        }
        [HttpPost]
        public async Task<JsonResult> EnviarCodigoPerfilAnalista(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo))
            {
                return Json(new
                {
                    ok = false,
                    mensaje = "Debe ingresar un correo válido."
                });
            }

            codigoPerfilAnalista = rnd.Next(100000, 999999).ToString();

            await _emailService.EnviarCodigoAsync(correo, codigoPerfilAnalista);

            return Json(new
            {
                ok = true,
                mensaje = "Código enviado correctamente al correo."
            });
        }
        public IActionResult CalificacionesServicio()
        {
            string nombre = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            string apellido = User.FindFirst("Apellido")?.Value ?? "";
            string correo = User.FindFirst("Correo")?.Value ?? "";

            ViewBag.NombreAnalista = $"{nombre} {apellido}".Trim();
            ViewBag.CorreoAnalista = correo;

            return View();
        }
        [HttpGet("/api/calificaciones")]
        public async Task<IActionResult> ObtenerResumenCalificaciones()
        {
            string nombre = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            string apellido = User.FindFirst("Apellido")?.Value ?? "";

            string analistaActual = $"{nombre} {apellido}".Trim();

            if (string.IsNullOrWhiteSpace(analistaActual))
            {
                analistaActual = "Analista";
            }
            try
            {
                var lista = await (
                    from cc in _context.ComentarioClientes
                    join u in _context.Usuario
                        on cc.Usuario_Id equals u.Id
                    orderby cc.FechaComentario descending
                    select new CalificacionDto
                    {
                        IdComentario = cc.Id_Comentario,
                        Calificacion = cc.Calificacion,
                        Comentario = cc.Comentario,
                        FechaComentario = cc.FechaComentario,
                        NombreCliente = u.Nombre,
                        ApellidoCliente = u.Apellido,
                        Analista = analistaActual
                    }
                ).ToListAsync();

                return Ok(BuildResumenCalificaciones(lista));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    mensaje = "Error al obtener calificaciones.",
                    detalle = ex.Message
                });
            }
        }

        private static ResumenCalificacionesDto BuildResumenCalificaciones(List<CalificacionDto> lista)
        {
            int total = lista.Count;

            decimal promedio = total > 0
                ? Math.Round(lista.Average(c => (decimal)c.Calificacion), 1)
                : 0;

            int promotores = lista.Count(c => c.Calificacion == 5);
            int detractores = lista.Count(c => c.Calificacion <= 3);

            int nps = total > 0
                ? (int)Math.Round(((double)(promotores - detractores) / total) * 100)
                : 0;

            string clasificacion = nps >= 50
                ? "Promotor"
                : nps >= 0
                    ? "Pasivo"
                    : "Detractor";

            return new ResumenCalificacionesDto
            {
                PuntajePromedio = promedio,
                TotalCalificaciones = total,
                Nps = nps,
                ClasificacionNps = clasificacion,
                DistribucionEstrellas = new Dictionary<int, int>
        {
            { 5, lista.Count(c => c.Calificacion == 5) },
            { 4, lista.Count(c => c.Calificacion == 4) },
            { 3, lista.Count(c => c.Calificacion == 3) },
            { 2, lista.Count(c => c.Calificacion == 2) },
            { 1, lista.Count(c => c.Calificacion == 1) },
        },
                Calificaciones = lista
            };
        }
        [HttpPost]
        public IActionResult MarcarAlertaVista(string tipo, int idOrigen)
        {
            if (tipo == "critica")
            {
                var solicitud = _context.SOLICITUD_CREDITO
                    .FirstOrDefault(x => x.Id_Solicitud == idOrigen);

                if (solicitud != null)
                {
                    solicitud.NotificacionVistaAnalista = true;
                }
            }
            else if (tipo == "mensaje")
            {
                var mensaje = _context.MENSAJE
                    .FirstOrDefault(x => x.Id_Mensaje == idOrigen);

                if (mensaje != null)
                {
                    mensaje.Leido = true;
                }
            }
            else if (tipo == "calificacion")
            {
                var comentario = _context.ComentarioClientes
                    .FirstOrDefault(x => x.Id_Comentario == idOrigen);

                if (comentario != null)
                {
                    comentario.VistaAnalista = true;
                }
            }

            _context.SaveChanges();

            return Json(new { ok = true });
        }
        public IActionResult GestionDePago()
        {
            var hoy = DateTime.Today;
            string nombre = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            string apellido = User.FindFirst("Apellido")?.Value ?? "";
            string correo = User.FindFirst("Correo")?.Value ?? "";

            ViewBag.NombreAnalista = $"{nombre} {apellido}".Trim();
            ViewBag.CorreoAnalista = correo;

            // Solo solicitudes aprobadas
            var solicitudesAprobadas = _context.SOLICITUD_CREDITO
                .Include(x => x.USUARIO)
                .Where(x => x.Estado == "Aprobado")
                .ToList();

            var idsAprobadas = solicitudesAprobadas
                .Select(x => x.Id_Solicitud)
                .ToList();

            // Métodos solo de solicitudes aprobadas
            var metodos = _context.METODO_PAGO_SOLICITUD
                .Where(x => idsAprobadas.Contains(x.SOLICITUD_CREDITO_Id_Solicitud))
                .ToList();

            ViewBag.Solicitudes = solicitudesAprobadas
                .ToDictionary(x => x.Id_Solicitud, x => x);

            // Pagos pendientes validación pasan a aprobado cuando entra analista
            var pagosPendientes = _context.PAGO_CUOTA
                .Where(x => x.Estado == "Pendiente validación")
                .ToList();

            foreach (var pago in pagosPendientes)
            {
                pago.Estado = "Aprobado";

                var cuota = _context.CUOTA.FirstOrDefault(c => c.Id_Cuota == pago.Id_Cuota);
                if (cuota != null)
                {
                    cuota.Estado = "Pagado";
                }
            }

            _context.SaveChanges();

            var pagosAprobados = _context.PAGO_CUOTA
     .Where(x => x.Estado == "Aprobado" || x.Estado == "Pendiente validación")
     .OrderByDescending(x => x.FechaPago)
     .ToList();

            ViewBag.TotalAprobados = pagosAprobados.Count;

            int esteMes = pagosAprobados
                .Count(x => x.FechaPago.Month == DateTime.Now.Month &&
                            x.FechaPago.Year == DateTime.Now.Year);

            int mesAnterior = _context.PAGO_CUOTA
                .Count(x =>
                    x.Estado == "Aprobado" &&
                    x.FechaPago.Month == DateTime.Now.AddMonths(-1).Month &&
                    x.FechaPago.Year == DateTime.Now.AddMonths(-1).Year);

            ViewBag.PorcentajeEsteMes = mesAnterior > 0
                ? Math.Round(((decimal)(esteMes - mesAnterior) / mesAnterior) * 100, 0)
                : esteMes > 0 ? 100 : 0;

            int totalPagosMetodo = pagosAprobados.Count;

            int totalTransferencia = pagosAprobados.Count(x =>
                x.MetodoPago != null &&
                x.MetodoPago.ToLower().Contains("transferencia"));

            int totalYape = pagosAprobados.Count(x =>
                x.MetodoPago != null &&
                x.MetodoPago.ToLower().Contains("yape"));

            int totalPlin = pagosAprobados.Count(x =>
                x.MetodoPago != null &&
                x.MetodoPago.ToLower().Contains("plin"));

            int totalOtros = totalPagosMetodo - totalTransferencia - totalYape - totalPlin;

            ViewBag.TotalTransferencias = totalTransferencia;
            ViewBag.TotalYape = totalYape;
            ViewBag.TotalPlin = totalPlin;
            ViewBag.TotalOtros = totalOtros;

            ViewBag.PorcTransferencia = totalPagosMetodo > 0
                ? Math.Round((decimal)totalTransferencia * 100 / totalPagosMetodo, 0)
                : 0;

            ViewBag.PorcYape = totalPagosMetodo > 0
                ? Math.Round((decimal)totalYape * 100 / totalPagosMetodo, 0)
                : 0;

            ViewBag.PorcPlin = totalPagosMetodo > 0
                ? Math.Round((decimal)totalPlin * 100 / totalPagosMetodo, 0)
                : 0;

            ViewBag.PorcOtros = totalPagosMetodo > 0
                ? Math.Round((decimal)totalOtros * 100 / totalPagosMetodo, 0)
                : 0;

            ViewBag.TotalYapePlin = totalYape + totalPlin;
            var metodosBase = pagosAprobados
    .Select(x =>
    {
        string entidad = (x.EntidadPago ?? "").ToLower().Trim();

        if (entidad.Contains("yape"))
            return "Yape";

        if (entidad.Contains("plin"))
            return "Plin";

        if (entidad.Contains("bcp"))
            return "BCP";

        if (entidad.Contains("interbank"))
            return "Interbank";

        if (entidad.Contains("bbva"))
            return "BBVA";

        if (entidad.Contains("scotiabank"))
            return "Scotiabank";

        if (entidad.Contains("caja arequipa"))
            return "Caja Arequipa";

        return "Otros";
    })
    .ToList();

            var agrupados = metodosBase
                .GroupBy(x => x)
                .Select(g => new
                {
                    Nombre = g.Key,
                    Total = g.Count(),
                    Porcentaje = totalPagosMetodo > 0
                        ? Math.Round((decimal)g.Count() * 100 / totalPagosMetodo, 0)
                        : 0
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            var top3 = agrupados
                .Where(x => x.Nombre != "Otros")
                .Take(3)
                .ToList();

            int otrosTotal = agrupados
                .Where(x => x.Nombre == "Otros" || !top3.Any(t => t.Nombre == x.Nombre))
                .Sum(x => x.Total);

            decimal otrosPorcentaje = totalPagosMetodo > 0
                ? Math.Round((decimal)otrosTotal * 100 / totalPagosMetodo, 0)
                : 0;

            var metodosAgrupados = top3
                .Cast<object>()
                .ToList();

            metodosAgrupados.Add(new
            {
                Nombre = "Otros",
                Total = otrosTotal,
                Porcentaje = otrosPorcentaje
            });

            ViewBag.MetodosMasUsados = metodosAgrupados;

            ViewBag.ClientesUnicosPago = pagosAprobados
                .Select(p => p.Id_Cuota)
                .Distinct()
                .Count();

            int totalPendientes = 0;

            foreach (var solicitud in solicitudesAprobadas)
            {
                var cuotasPendientes = _context.CUOTA
                    .Where(c =>
                        c.SOLICITUD_CREDITO_Id_Solicitud == solicitud.Id_Solicitud &&
                        c.Estado != "Pagado")
                    .OrderBy(c => c.FechaVencimiento)
                    .ToList();

                if (!cuotasPendientes.Any())
                    continue;

                int vencidas = cuotasPendientes
                    .Count(c => c.FechaVencimiento.Date < hoy);

                totalPendientes += vencidas > 0 ? vencidas : 1;
            }
            ViewBag.TotalPendientes = totalPendientes;

            ViewBag.TotalMetodosResumen = totalPendientes + ViewBag.TotalAprobados;

            ViewBag.TotalMetodosRegistrados = ViewBag.TotalMetodosResumen;
            var idsCuotasPago = pagosAprobados
    .Select(x => x.Id_Cuota)
    .Distinct()
    .ToList();

            ViewBag.CuotasPago = _context.CUOTA
                .Include(x => x.SOLICITUD_CREDITO)
                .ThenInclude(x => x.USUARIO)
                .Where(x => idsCuotasPago.Contains(x.Id_Cuota))
                .ToDictionary(x => x.Id_Cuota, x => x);
            var idsSolicitudesPago = ViewBag.CuotasPago != null
    ? ((Dictionary<int, Cuota>)ViewBag.CuotasPago)
        .Values
        .Select(x => x.SOLICITUD_CREDITO_Id_Solicitud)
        .Distinct()
        .ToList()
    : new List<int>();

            ViewBag.MetodosSolicitudPago = _context.METODO_PAGO_SOLICITUD
                .Where(x => idsSolicitudesPago.Contains(x.SOLICITUD_CREDITO_Id_Solicitud))
                .ToDictionary(x => x.SOLICITUD_CREDITO_Id_Solicitud, x => x);

            var listaGestion = new List<PagoGestionDto>();

            var solicitudesConCuotas = _context.SOLICITUD_CREDITO
                .Include(s => s.USUARIO)
                .Where(s => s.Estado == "Aprobado")
                .ToList();

            foreach (var solicitud in solicitudesConCuotas)
            {
                var metodoSolicitud = _context.METODO_PAGO_SOLICITUD
                    .FirstOrDefault(x => x.SOLICITUD_CREDITO_Id_Solicitud == solicitud.Id_Solicitud);

                var cuotasSolicitud = _context.CUOTA
                    .Where(c => c.SOLICITUD_CREDITO_Id_Solicitud == solicitud.Id_Solicitud)
                    .OrderBy(c => c.NumeroCuota)
                    .ToList();

                var pagosSolicitud = _context.PAGO_CUOTA
                    .Where(p => cuotasSolicitud.Select(c => c.Id_Cuota).Contains(p.Id_Cuota))
                    .OrderByDescending(p => p.FechaPago)
                    .ToList();

                foreach (var pago in pagosSolicitud)
                {
                    listaGestion.Add(new PagoGestionDto
                    {
                        IdCuota = pago.Id_Cuota,
                        IdSolicitud = solicitud.Id_Solicitud,
                        Cliente = solicitud.USUARIO.Nombre + " " + solicitud.USUARIO.Apellido,
                        Dni = solicitud.USUARIO.Dni,
                        MetodoPago = pago.MetodoPago,
                        EntidadPago = pago.EntidadPago,
                        NumeroCuentaPago = metodoSolicitud?.NumeroCuentaPago,
                        TitularCuenta = metodoSolicitud?.TitularCuenta,
                        FechaRegistro = pago.FechaPago,
                        Estado = pago.Estado == "Aprobado" ? "Aprobado" : "Pendiente"
                    });
                }

                var siguientePendiente = cuotasSolicitud
                    .FirstOrDefault(c => c.Estado != "Pagado");

                if (siguientePendiente != null)
                {
                    var ultimoPago = pagosSolicitud.FirstOrDefault();

                    listaGestion.Add(new PagoGestionDto
                    {
                        IdCuota = siguientePendiente.Id_Cuota,
                        IdSolicitud = solicitud.Id_Solicitud,
                        Cliente = solicitud.USUARIO.Nombre + " " + solicitud.USUARIO.Apellido,
                        Dni = solicitud.USUARIO.Dni,
                        MetodoPago = metodoSolicitud?.MetodoPago,
                        EntidadPago = metodoSolicitud?.EntidadPago,
                        NumeroCuentaPago = metodoSolicitud?.NumeroCuentaPago,
                        TitularCuenta = metodoSolicitud?.TitularCuenta,
                        FechaRegistro = ultimoPago != null ? ultimoPago.FechaPago : solicitud.FechaSolicitud,
                        Estado = "Pendiente"
                    });
                }
            }

            listaGestion = listaGestion
                .OrderByDescending(x => x.FechaRegistro)
                .ToList();

            return View(listaGestion);
        }
        public IActionResult PoliticasRiesgo()
        {
            string nombre = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            string apellido = User.FindFirst("Apellido")?.Value ?? "";
            string correo = User.FindFirst("Correo")?.Value ?? "";

            ViewBag.NombreAnalista = $"{nombre} {apellido}".Trim();
            ViewBag.CorreoAnalista = correo;

            return View();
        }
        public IActionResult ContactarSoporte()
        {
            string nombre = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            string apellido = User.FindFirst("Apellido")?.Value ?? "";
            string correo = User.FindFirst("Correo")?.Value ?? "";

            ViewBag.NombreAnalista = $"{nombre} {apellido}".Trim();
            ViewBag.CorreoAnalista = correo;
            var admin = _context.Usuario.FirstOrDefault(x => x.Rol == "Administrador");
            var dni = User.FindFirst("Dni")?.Value;
            var analista = _context.Usuario.FirstOrDefault(x => x.Dni == dni && x.Rol == "Analista");

            ViewBag.IdAdministrador = admin?.Id ?? 0;
            ViewBag.IdAnalista = analista?.Id ?? 0;

            ViewBag.MensajesAdmin = _context.MENSAJE_ADMIN_ANALISTA
                .OrderBy(x => x.FechaEnvio)
                .ToList();
            ViewBag.AdminActivo = admin != null &&
                                  admin.EstadoActivo &&
                                  admin.UltimaConexion >= DateTime.Now.AddMinutes(-2);

            return View();
        }
        [HttpPost]
        public IActionResult EnviarSolicitudSoporte(string asunto, string mensaje)
        {
            var dni = User.FindFirst("Dni")?.Value;

            var analista = _context.Usuario
                .FirstOrDefault(x => x.Dni == dni && x.Rol == "Analista");

            if (analista == null)
                return RedirectToAction("ContactarSoporte");

            _context.SOLICITUD_SOPORTE.Add(new SolicitudSoporte
            {
                Id_Analista = analista.Id,
                Asunto = asunto,
                Mensaje = mensaje,
                Estado = "Pendiente",
                FechaEnvio = DateTime.Now,
                Leido = false
            });

            _context.SaveChanges();

            TempData["OkSoporte"] = "Solicitud enviada correctamente.";
            return RedirectToAction("ContactarSoporte");
        }


    }
}