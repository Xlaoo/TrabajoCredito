using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using trabajo.Models;
using trabajo.Models.ViewModels;
using System.Security.Claims;
using ClosedXML.Excel;
using System.IO;

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

                TotalReportesGenerados = _context.REPORTE_GENERADO.Count(),

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
    x.EstadoActivo == true),
                EstadoBaseDatos = "Óptimo",
                VersionSistema = "v2.1.0",
                PorcentajeAlmacenamiento = 65,
                UltimoRespaldo = DateTime.Now
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
    }
}
