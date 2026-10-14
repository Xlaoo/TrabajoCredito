using System;

namespace trabajo.Models
{
    /// <summary>
    /// Modelo de vista para presentar revisiones de préstamos aprobados
    /// en el Calendario de Revisiones del Analista con fechas y horas garantizadas en horario peruano.
    /// </summary>
    public class RevisionCalendarioViewModel
    {
        public int Id_Solicitud { get; set; }
        public string NumeroSolicitud { get; set; } = string.Empty;
        public string ClienteNombre { get; set; } = string.Empty;
        public string ClienteDni { get; set; } = string.Empty;
        public string ClienteCorreo { get; set; } = string.Empty;
        public decimal MontoSolicitado { get; set; }
        public int PlazoMeses { get; set; }
        public decimal InteresEstimado { get; set; }
        public string Estado { get; set; } = string.Empty;

        /// <summary>
        /// Fecha en que el cliente registró su solicitud, convertida a hora peruana.
        /// </summary>
        public DateTime FechaSolicitudPeru { get; set; }

        /// <summary>
        /// Fecha en que el analista aprobó el préstamo, convertida a hora peruana (si aplica).
        /// </summary>
        public DateTime? FechaAprobacionPeru { get; set; }

        /// <summary>
        /// Fecha principal de referencia para la revisión en el calendario (hora peruana).
        /// </summary>
        public DateTime FechaRevisionPeru { get; set; }

        public string FechaRevisionTexto => FechaRevisionPeru.ToString("dd/MM/yyyy");
        public string HoraRevisionTexto => FechaRevisionPeru.ToString("HH:mm");
        public string FechaIso => FechaRevisionPeru.ToString("yyyy-MM-dd");
    }
}
