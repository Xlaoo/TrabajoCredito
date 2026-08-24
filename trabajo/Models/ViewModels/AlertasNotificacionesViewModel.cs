using System;
using System.Collections.Generic;
using System.Linq;

namespace trabajo.Models.ViewModels
{
    public class AlertasNotificacionesViewModel
    {
        public string AnalistaNombre { get; set; }
        public string AnalistaCorreo { get; set; }
        public List<AlertaItemViewModel> Alertas { get; set; } = new List<AlertaItemViewModel>();

        public int TotalCriticas => Alertas.Count(a => a.Prioridad == "critico");

        public int TotalRevisionesPendientes => Alertas.Count(a => a.Prioridad == "revision");

        public int TotalInformativas => Alertas.Count(a => a.Prioridad == "informativa");

        public int TotalCalificaciones => Alertas.Count(a => a.Prioridad == "calificacion");

        public int TotalNoLeidas =>
            TotalCriticas + TotalInformativas + TotalCalificaciones;
    }

    public class AlertaItemViewModel
    {
        public int IdSolicitud { get; set; }

        public string Tipo { get; set; }

        public int IdOrigen { get; set; }

        public string UrlOjo { get; set; }

        public string Prioridad { get; set; }

        public string PrioridadEtiqueta { get; set; }

        public string AsuntoPrincipal { get; set; }

        public string AsuntoSubtitulo { get; set; }

        public string ClienteNombre { get; set; }

        public decimal MontoSolicitado { get; set; }

        public DateTime Fecha { get; set; }

        public string FechaFormateada => Fecha.ToString("dd/MM/yy");

        public string HoraFormateada => Fecha.ToString("hh:mm tt");
    }
}