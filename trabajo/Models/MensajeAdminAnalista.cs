using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    [Table("mensaje_admin_analista")]
    public class MensajeAdminAnalista
    {
        [Key]
        public int IdMensaje { get; set; }

        public int IdAdministrador { get; set; }

        public int IdAnalista { get; set; }

        public string RemitenteRol { get; set; }

        public string Mensaje { get; set; }

        public DateTime FechaEnvio { get; set; }

        public bool Leido { get; set; }
    }
}