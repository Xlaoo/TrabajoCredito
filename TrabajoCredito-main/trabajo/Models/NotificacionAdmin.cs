using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    [Table("notificacion_admin")]
    public class NotificacionAdmin
    {
        [Key]
        public int Id_Notificacion { get; set; }

        public string TipoNotificacion { get; set; } = "";

        public string Asunto { get; set; } = "";

        public string Mensaje { get; set; } = "";

        public string Canal { get; set; } = "";

        public string Destinatario { get; set; } = "";

        public string Estado { get; set; } = "";

        public DateTime FechaEnvio { get; set; }

        public bool Plantilla { get; set; }
    }
}