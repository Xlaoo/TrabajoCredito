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
using System.Security.Claims;
using trabajo.Models;
using trabajo.Models.ViewModels;

namespace trabajo.Controllers
{
    [Authorize]
    public class AdministradorController : Controller
    {
        private readonly UsuarioContext _context;
        public AdministradorController(UsuarioContext context)
        {
            _context = context;
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
    .OrderByDescending(x => x.Id)
    .FirstOrDefault();

            var repCreditos = _context.REPORTE_GENERADO
    .Where(x => x.TipoReporte == "Creditos")
    .OrderByDescending(x => x.Id)
    .FirstOrDefault();

            var repPagos = _context.REPORTE_GENERADO
    .Where(x => x.TipoReporte == "Pagos")
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
            var reporte = _context.REPORTE_GENERADO
                .Where(x => x.TipoReporte == tipoReporte)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            if (reporte == null) return;

            DateTime ultimaFecha = DateTime.MinValue;

            if (tipoReporte == "Clientes")
            {
                ultimaFecha = _context.Usuario
                    .Where(x => x.Rol == "Cliente")
                    .Max(x => x.FechaRegistro);
            }
            else if (tipoReporte == "Creditos")
            {
                ultimaFecha = _context.SOLICITUD_CREDITO.Any()
                    ? _context.SOLICITUD_CREDITO.Max(x => x.FechaSolicitud)
                    : DateTime.MinValue;
            }
            else if (tipoReporte == "Pagos")
            {
                ultimaFecha = _context.PAGO_CUOTA.Any()
                    ? _context.PAGO_CUOTA.Max(x => x.FechaPago)
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
        public IActionResult Pagos(string busqueda = "", string estadoFiltro = "Todos", string metodoFiltro = "Todos")
        {
            ViewData["ActivePage"] = "Pagos";
            CargarDatosAdministrador();

            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var finMes = inicioMes.AddMonths(1);

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
        public IActionResult PagosExcel(string busqueda = "", string estadoFiltro = "Todos", string metodoFiltro = "Todos")
        {
            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var finMes = inicioMes.AddMonths(1);
            var hoy = DateTime.Today;

            var pagosCompletados = (
                from p in _context.PAGO_CUOTA
                join c in _context.CUOTA on p.Id_Cuota equals c.Id_Cuota
                join s in _context.SOLICITUD_CREDITO on c.SOLICITUD_CREDITO_Id_Solicitud equals s.Id_Solicitud
                join u in _context.Usuario on s.Usuario_Id_Usuario equals u.Id
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

        public IActionResult PagosPdf(string busqueda = "", string estadoFiltro = "Todos", string metodoFiltro = "Todos")
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var finMes = inicioMes.AddMonths(1);

            var datos = (
                from p in _context.PAGO_CUOTA
                join c in _context.CUOTA on p.Id_Cuota equals c.Id_Cuota
                join s in _context.SOLICITUD_CREDITO on c.SOLICITUD_CREDITO_Id_Solicitud equals s.Id_Solicitud
                join u in _context.Usuario on s.Usuario_Id_Usuario equals u.Id
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

            var pagosMes = datos.Where(x => x.FechaPago >= inicioMes && x.FechaPago < finMes).ToList();
            var completadosMes = pagosMes.Where(x => x.Estado == "Aprobado" || x.Estado == "Completado").ToList();

            var pendientes = (
                from c in _context.CUOTA
                join s in _context.SOLICITUD_CREDITO on c.SOLICITUD_CREDITO_Id_Solicitud equals s.Id_Solicitud
                join u in _context.Usuario on s.Usuario_Id_Usuario equals u.Id
                where c.Estado == "Pendiente"
                      && c.FechaLimitePago >= inicioMes
                      && c.FechaLimitePago < finMes
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
            reporte.AddRange(completadosMes);
            reporte.AddRange(pendientes);

            decimal montoRecaudado = completadosMes.Sum(x => x.Monto);

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
                        col.Item().Text($"Reporte del mes: {DateTime.Now:MMMM yyyy}");

                        col.Item().PaddingVertical(12).Row(row =>
                        {
                            row.RelativeItem().Border(1).Padding(8).Text($"Total pagos este mes\n{reporte.Count}").Bold();
                            row.RelativeItem().Border(1).Padding(8).Text($"Completados\n{completadosMes.Count}").Bold();
                            row.RelativeItem().Border(1).Padding(8).Text($"Pendientes / Mora\n{pendientes.Count}").Bold();
                            row.RelativeItem().Border(1).Padding(8).Text($"Monto recaudado\nS/ {montoRecaudado:N2}").Bold();
                        });

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
                        col.Item().PaddingTop(18).Row(row =>
                        {
                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                            {
                                c.Item().Text("Resumen por Estado").Bold().FontSize(13).FontColor(Colors.Purple.Darken3);
                                c.Item().Text($"Completados: {completadosMes.Count}");
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

                    page.Footer().AlignCenter().Text("CrediPlus - Reporte generado automáticamente").FontSize(9);
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
        public async Task<IActionResult> ExportarClientesExcel(string? buscar, string estado = "Todos")
        {
            var clientes = await ObtenerClientesFiltrados(buscar, estado)
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
        public async Task<IActionResult> ExportarClientesPdf(string? buscar, string estado = "Todos")
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var clientes = await ObtenerClientesFiltrados(buscar, estado)
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

                        col.Item().Text($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Table(table =>
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
        public IActionResult ExportarCreditosExcel(string? buscar, string estado = "Todos")
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
        public IActionResult ExportarCreditosPdf(string? buscar, string estado = "Todos")
        {
            QuestPDF.Settings.License = LicenseType.Community;

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

            var datos = query.OrderByDescending(x => x.Solicitud.FechaSolicitud).ToList();

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(25);

                    page.Header().Column(col =>
                    {
                        col.Item().Text("CrediPlus - Reporte de Créditos")
                            .FontSize(20)
                            .Bold()
                            .FontColor("#6D28D9");

                        col.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);

                        col.Item().Text($"Estado: {estado} | Búsqueda: {(string.IsNullOrWhiteSpace(buscar) ? "Sin búsqueda" : buscar)}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().PaddingTop(15).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.7f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CreditosPdfHeader).Text("Solicitud").FontColor(Colors.White).Bold();
                            header.Cell().Element(CreditosPdfHeader).Text("Cliente").FontColor(Colors.White).Bold();
                            header.Cell().Element(CreditosPdfHeader).Text("DNI").FontColor(Colors.White).Bold();
                            header.Cell().Element(CreditosPdfHeader).Text("Monto").FontColor(Colors.White).Bold();
                            header.Cell().Element(CreditosPdfHeader).Text("Plazo").FontColor(Colors.White).Bold();
                            header.Cell().Element(CreditosPdfHeader).Text("Interés").FontColor(Colors.White).Bold();
                            header.Cell().Element(CreditosPdfHeader).Text("Estado").FontColor(Colors.White).Bold();
                            header.Cell().Element(CreditosPdfHeader).Text("Fecha").FontColor(Colors.White).Bold();
                            header.Cell().Element(CreditosPdfHeader).Text("Motivo").FontColor(Colors.White).Bold();
                        });

                        foreach (var item in datos)
                        {
                            table.Cell().Element(CreditosPdfCell).Text(item.Solicitud.NumeroSolicitud ?? "");
                            table.Cell().Element(CreditosPdfCell).Text($"{item.Solicitud.USUARIO?.Nombre} {item.Solicitud.USUARIO?.Apellido}");
                            table.Cell().Element(CreditosPdfCell).Text(item.Solicitud.USUARIO?.Dni ?? "");
                            table.Cell().Element(CreditosPdfCell).Text($"S/ {item.Solicitud.MontoSolicitado:N2}");
                            table.Cell().Element(CreditosPdfCell).Text($"{item.Solicitud.PlazoMeses} meses");
                            table.Cell().Element(CreditosPdfCell).Text($"S/ {item.Solicitud.InteresEstimado:N2}");
                            table.Cell().Element(CreditosPdfCell).Text(item.Solicitud.Estado);
                            table.Cell().Element(CreditosPdfCell).Text(item.Solicitud.FechaSolicitud.ToString("dd/MM/yyyy"));
                            table.Cell().Element(CreditosPdfCell).Text(item.Perfil?.MotivoPrestamo ?? "Sin motivo");
                        }
                    });

                    page.Footer().AlignCenter()
                        .Text("CrediPlus - Reporte de Créditos")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
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
    }
}
    

