using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    public class Evaluacion_Riesgo
    {
        [Key]
        public int Id_Evaluacion { get; set; }

        public string Resultado { get; set; }

        public string? Observacion { get; set; }

        public string? Responsable { get; set; }

        public DateTime FechaEvaluacion { get; set; }


        public int SOLICITUD_CREDITO_Id_Solicitud { get; set; }

        [ForeignKey("SOLICITUD_CREDITO_Id_Solicitud")]
        public SolicitudCredito SOLICITUD_CREDITO { get; set; }
    }
}
