using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    [Table("PROPUESTA_CREDITO")]
    public class PropuestaCredito
    {
        [Key]
        public int Id_Propuesta { get; set; }

        public int SOLICITUD_CREDITO_Id_Solicitud { get; set; }

        public decimal Monto { get; set; }

        public int PlazoMeses { get; set; }

        public bool EsRecomendada { get; set; }

        public DateTime FechaRegistro { get; set; }

        [ForeignKey("SOLICITUD_CREDITO_Id_Solicitud")]
        public virtual SolicitudCredito SOLICITUD_CREDITO { get; set; }
    }
}