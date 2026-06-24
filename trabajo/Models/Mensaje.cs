using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    public class Mensaje
    {
        [Key]
        public int Id_Mensaje { get; set; }

        public int SOLICITUD_CREDITO_Id_Solicitud { get; set; }

        public string Remitente { get; set; }

        [Column("Mensaje")]
        public string MensajeTexto { get; set; }
        public string? Imagen { get; set; }
        public DateTime FechaEnvio { get; set; }
        public bool Leido { get; set; }

        [ForeignKey("SOLICITUD_CREDITO_Id_Solicitud")]

        public SolicitudCredito SOLICITUD_CREDITO { get; set; }
    }
}