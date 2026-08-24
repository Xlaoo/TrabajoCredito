using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    [Table("cronograma")]
    public class Cronograma
    {
        [Key]
        public int Id_Cronograma { get; set; }

        public string CorreoDestino { get; set; }

        public DateTime FechaEnvio { get; set; }

        public string EstadoEnvio { get; set; }

        public string NumeroOperacion { get; set; }

        public int SOLICITUD_CREDITO_Id_Solicitud { get; set; }

        [ForeignKey("SOLICITUD_CREDITO_Id_Solicitud")]
        public virtual SolicitudCredito SOLICITUD_CREDITO { get; set; }
    }
}