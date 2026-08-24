using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    [Table("historial_estado")]
    public class HistorialEstado
    {
        [Key]
        public int Id_Historial { get; set; }

        public string EstadoActual { get; set; }

        public string MotivoCambio { get; set; }

        public DateTime FechaCambio { get; set; }

        public int? SOLICITUD_CREDITO_Id_Solicitud { get; set; }

        [ForeignKey("SOLICITUD_CREDITO_Id_Solicitud")]
        public virtual SolicitudCredito SOLICITUD_CREDITO { get; set; }
    }
}