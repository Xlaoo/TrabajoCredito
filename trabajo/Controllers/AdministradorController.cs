using ClosedXML.Excel;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;
using System.Security.Claims;
using trabajo.Models;
using trabajo.Models.ViewModels;
using trabajo.Service;
using static QuestPDF.Helpers.Colors;

namespace trabajo.Controllers
{
    [Authorize]
    public class AdministradorController : Controller
    {
        private readonly UsuarioContext _context;
        private static readonly Random rnd = new Random();
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService = new EmailService();
        private static string codigoPerfilAdmin = "";

        public AdministradorController(UsuarioContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
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
                join cuota in _context.CUOTA
                    on pago.Id_Cuota equals cuota.Id_Cuota
                where pago.Estado == "Aprobado"
                select cuota.SOLICITUD_CREDITO_Id_Solicitud
            )
            .Distinct()
            .Count();

            int pagosMoraMes = cuotasActivas
                .Where(c =>
                    c.Estado == "Pendiente" &&
                    c.FechaLimitePago.Date < hoy)
                .Select(c => c.SOLICITUD_CREDITO_Id_Solicitud)
                .Distinct()
                .Count();

            int pagosPendientesMes = cuotasActivas
                .Where(c =>
                    c.Estado == "Pendiente" &&
                    c.FechaLimitePago.Date >= hoy)
                .Select(c => c.SOLICITUD_CREDITO_Id_Solicitud)
                .Distinct()
                .Count();

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

            int totalCuotasEnMora = _context.CUOTA.Count(x =>
      x.FechaLimitePago.Date < hoy &&
      x.Estado == "Pendiente");

            decimal montoEnMora = _context.CUOTA
                .Where(x => x.FechaLimitePago.Date < hoy &&
                            x.Estado == "Pendiente")
                .Sum(x => x.MontoCuota ?? 0);

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
                .Where(x => x.Rol == "Cliente")
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

                TotalReportesGenerados = _context.REPORTE_GENERADO
    .Count(x => x.Estado == "Completado"),

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
                .Where(x => x.TipoReporte == "Clientes")
                .OrderByDescending(x => x.FechaGeneracion)
                .FirstOrDefault();

            var repCreditos = _context.REPORTE_GENERADO
                .Where(x => x.TipoReporte == "Creditos")
                .OrderByDescending(x => x.FechaGeneracion)
                .FirstOrDefault();

            var repPagos = _context.REPORTE_GENERADO
                .Where(x => x.TipoReporte == "Pagos")
                .OrderByDescending(x => x.FechaGeneracion)
                .FirstOrDefault();

            ViewBag.ClientesPendiente = repClientes == null
    || repClientes.Estado == "En Proceso"
    || totalClientesActual > repClientes.CantidadDatos;

            ViewBag.CreditosPendiente = repCreditos == null
                || repCreditos.Estado == "En Proceso"
                || totalCreditosActual > repCreditos.CantidadDatos;

            ViewBag.PagosPendiente = repPagos == null
                || repPagos.Estado == "En Proceso"
                || totalPagosActual > repPagos.CantidadDatos;
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
        public IActionResult Exportaciones(DateTime? fechaInicio, DateTime? fechaFin, string tipoExportacion, string estado)
        {
            CargarDatosAdministrador();
            ViewData["Title"] = "Exportaciones";
            ViewData["ActivePage"] = "Exportaciones";

            SincronizarReportesPendientes();

            DateTime inicio = fechaInicio ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            DateTime fin = fechaFin ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month));

            var query = _context.REPORTE_GENERADO.AsQueryable();

            query = query.Where(x => x.FechaSolicitud.Date >= inicio.Date && x.FechaSolicitud.Date <= fin.Date);

            if (!string.IsNullOrEmpty(tipoExportacion) && tipoExportacion != "Todos")
                query = query.Where(x => x.TipoReporte == tipoExportacion);

            if (!string.IsNullOrEmpty(estado) && estado != "Todos")
                query = query.Where(x => x.Estado == estado);

            var reportes = query
                .OrderByDescending(r => r.FechaSolicitud)
                .ToList();

            ViewBag.FechaInicio = inicio.ToString("yyyy-MM-dd");
            ViewBag.FechaFin = fin.ToString("yyyy-MM-dd");
            ViewBag.TipoExportacion = tipoExportacion ?? "Todos";
            ViewBag.Estado = estado ?? "Todos";

            return View(reportes);
        }
        public IActionResult DescargarExportacion(int id)
        {
            var reporte = _context.REPORTE_GENERADO.FirstOrDefault(x => x.Id == id);

            if (reporte == null)
                return RedirectToAction("Exportaciones");

            reporte.Estado = "Completado";
            reporte.Descargado = true;
            reporte.FechaGeneracion = DateTime.Now;
            reporte.FechaSolicitud = DateTime.Now;
            reporte.CantidadDatos = ObtenerCantidadDatosReporte(reporte.TipoReporte);
            _context.SaveChanges();

            if (reporte.TipoReporte == "Clientes")
                return GenerarClientesExcel();

            if (reporte.TipoReporte == "Creditos")
                return GenerarCreditosExcel();

            if (reporte.TipoReporte == "Pagos")
                return GenerarPagosExcel();

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

            return RedirectToAction("ProgramaAdministrador");
        }
        private int ObtenerCantidadDatosReporte(string tipoReporte)
        {
            if (tipoReporte == "Clientes")
                return _context.Usuario.Count(x => x.Rol == "Cliente");

            if (tipoReporte == "Creditos")
                return _context.SOLICITUD_CREDITO.Count();

            if (tipoReporte == "Pagos")
                return _context.PAGO_CUOTA.Count();

            return 0;
        }
        private void SincronizarReportesPendientes()
        {
            RevisarReportePendiente("Clientes");
            RevisarReportePendiente("Creditos");
            RevisarReportePendiente("Pagos");
        }

        private void RevisarReportePendiente(string tipoReporte)
        {
            int cantidadActual = ObtenerCantidadDatosReporte(tipoReporte);

            var reporte = _context.REPORTE_GENERADO
                .Where(x => x.TipoReporte == tipoReporte)
                .OrderBy(x => x.Id)
                .FirstOrDefault();

            if (reporte == null) return;

            if (cantidadActual > reporte.CantidadDatos)
            {
                reporte.Estado = "En Proceso";
                reporte.Descargado = false;
                reporte.CantidadDatos = cantidadActual;
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
        //Reportes de Administración
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
            RegistrarReporteMensual("Clientes");
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
            RegistrarReporteMensual("Creditos");
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
            RegistrarReporteMensual("Pagos");
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
        public IActionResult ReporteAdministradorExcel(
    DateTime? fechaInicio = null,
    DateTime? fechaFin = null,
    string tipoCreditoFiltro = "Todos",
    string estadoFiltro = "Todos")
        {
            DateTime desde = fechaInicio ?? new DateTime(DateTime.Now.Year, 1, 1);
            DateTime hasta = fechaFin ?? DateTime.Today;

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

            // ===================== HOJA 1: RESUMEN GENERAL =====================
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
                $"Reporte_Administrador_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            );
        }
        public IActionResult ReporteAdministradorPdf(
    DateTime? fechaInicio = null,
    DateTime? fechaFin = null,
    string tipoCreditoFiltro = "Todos",
    string estadoFiltro = "Todos")
        {
            QuestPDF.Settings.License = LicenseType.Community;

            DateTime desde = fechaInicio ?? new DateTime(DateTime.Now.Year, 1, 1);
            DateTime hasta = fechaFin ?? DateTime.Today;

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

            return File(pdf, "application/pdf", "Reporte_Administrador.pdf");
        }



        public IActionResult PerfilAdministrador()
        {
            CargarDatosAdministrador();
            ViewData["ActivePage"] = "Perfil";

            var correoAdmin =
                User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ??

                User.FindFirst("Correo")?.Value ??
                User.FindFirst("correo")?.Value ??
                User.Identity?.Name;

            var admin = _context.Usuario.FirstOrDefault(x => x.Correo == correoAdmin && x.Rol == "Administrador");
            if (admin == null)
            {
                return NotFound();
            }
            return View(admin);
        }

        [HttpPost]
        public IActionResult PerfilAdministrador(Usuario usuario, string codigoCorreo)
        {
            CargarDatosAdministrador();

            var correoActual =
                User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ??
                User.FindFirst("Correo")?.Value ??
                User.FindFirst("correo")?.Value ??
                User.Identity?.Name;

            var admin = _context.Usuario
                .FirstOrDefault(x => x.Correo == correoActual && x.Rol == "Administrador");

            if (admin == null)
                return RedirectToAction("IniciarSesion", "Login");

            // Verificación de correo si fue modificado
            if (string.IsNullOrWhiteSpace(codigoCorreo) || codigoCorreo != codigoPerfilAdmin)
            {
                TempData["MensajeError"] = "El código de verificación del correo es incorrecto.";
                return RedirectToAction("PerfilAdministrador");
            }

            // Actualizar campos
            admin.Nombre = usuario.Nombre;
            admin.Apellido = usuario.Apellido;
            admin.Genero = usuario.Genero;
            admin.Dni = usuario.Dni;
            admin.Celular = usuario.Celular;
            admin.Correo = usuario.Correo;

            if (!string.IsNullOrWhiteSpace(usuario.clave))
                admin.clave = utilidades.EncriptarClave(usuario.clave);

            _context.Usuario.Update(admin);
            _context.SaveChanges();

            TempData["MensajeOk"] = "Perfil actualizado correctamente.";
            return RedirectToAction("PerfilAdministrador");
        }


        [HttpPost]
        public async Task<JsonResult> EnviarCodigoPerfilAdministrador(string correo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(correo))
                    return Json(new { ok = false, mensaje = "Ingrese un correo Gmail válido." });

                codigoPerfilAdmin = rnd.Next(100000, 999999).ToString();

                HttpContext.Session.SetString("CodigoPerfil", codigoPerfilAdmin);
                HttpContext.Session.SetString("CorreoPerfil", correo);

                await _emailService.EnviarCodigoAsync(correo, codigoPerfilAdmin);

                return Json(new { ok = true, mensaje = "Código enviado correctamente al correo." });
            }

            catch
            {
                return Json(new { ok = false, mensaje = "No se pudo enviar el código de verificación." });
            }
        }

    }
}