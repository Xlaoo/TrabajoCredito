using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    [Table("cancelacion_evaluacion")]
    public class CancelacionEvaluacion
    {
        [Key]
        public int Id_Cancelacion { get; set; }

        public int? IdSolicitud { get; set; }

        public decimal MontoSolicitado { get; set; }

        public string MotivoCancelacion { get; set; }

        public DateTime FechaCancelacion { get; set; }

        public string Responsable { get; set; }
    }
}