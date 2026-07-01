using Microsoft.EntityFrameworkCore;
using prestamoscreditos.Models;
using trabajo.Models.ViewModels;

namespace trabajo.Models
{
    public class UsuarioContext :DbContext
    {
        public UsuarioContext(DbContextOptions<UsuarioContext> options) : base(options)
        {

        }
        public DbSet<Usuario> Usuario { get; set; }
        public DbSet<SolicitudCredito> SOLICITUD_CREDITO { get; set; }
        public DbSet<PagoCancelacion> PAGO_CANCELACION { get; set; }
        public DbSet<HistorialEstado> HISTORIAL_ESTADO { get; set; }
        public DbSet<Cuota> CUOTA { get; set; }
        public DbSet<PagoCuota> PAGO_CUOTA { get; set; }
        public DbSet<PerfilFinanciero> PERFIL_FINANCIERO { get; set; }
        public DbSet<Evaluacion_Riesgo> Evaluacion_Riesgo { get; set; }
        public DbSet<ComentarioCliente> ComentarioClientes { get; set; }

        public DbSet<HistorialCredito> HISTORIAL_CREDITO { get; set; }
        public DbSet<ResumenCredito> RESUMEN_CREDITICIO { get; set; }
        public DbSet<ReporteRiesgo> ReporteRiesgo { get; set; }
        public DbSet<Cronograma> CRONOGRAMA { get; set; }
        public DbSet<Mensaje> MENSAJE { get; set; }
        public DbSet<PropuestaCredito> PROPUESTA_CREDITO { get; set; }
        public DbSet<CancelacionEvaluacion> CancelacionEvaluacion { get; set; }
        public DbSet<MetodoPagoSolicitud> METODO_PAGO_SOLICITUD { get; set; }
        public DbSet<ReporteGenerado> REPORTE_GENERADO { get; set; }
        public DbSet<ActividadAdministrador> ACTIVIDAD_ADMINISTRADOR { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Usuario>().ToTable("usuario");
            modelBuilder.Entity<ComentarioCliente>().ToTable("comentario_cliente");
            modelBuilder.Entity<Cronograma>().ToTable("cronograma");
            modelBuilder.Entity<Cuota>().ToTable("cuota");
            modelBuilder.Entity<Evaluacion_Riesgo>().ToTable("evaluacion_riesgo");
            modelBuilder.Entity<HistorialCredito>().ToTable("historial_crediticio");
            modelBuilder.Entity<HistorialEstado>().ToTable("historial_estado");
            modelBuilder.Entity<Mensaje>().ToTable("mensaje");
            modelBuilder.Entity<MetodoPagoSolicitud>().ToTable("metodo_pago_solicitud");
            modelBuilder.Entity<PagoCancelacion>().ToTable("pago_cancelacion");
            modelBuilder.Entity<PagoCuota>().ToTable("pago_cuota");
            modelBuilder.Entity<PerfilFinanciero>().ToTable("perfil_financiero");
            modelBuilder.Entity<PropuestaCredito>().ToTable("propuesta_credito");
            modelBuilder.Entity<ReporteRiesgo>().ToTable("reporteriesgo");
            modelBuilder.Entity<ResumenCredito>().ToTable("resumen_crediticio");
            modelBuilder.Entity<SolicitudCredito>().ToTable("solicitud_credito");
            modelBuilder.Entity<CancelacionEvaluacion>().ToTable("cancelacion_evaluacion");
            modelBuilder.Entity<ReporteGenerado>().ToTable("reporte_generado");
            modelBuilder.Entity<ActividadAdministrador>().ToTable("actividad_administrador");
        }
    }



}
