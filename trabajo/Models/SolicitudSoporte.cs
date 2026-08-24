using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    [Table("solicitud_soporte")]
    public class SolicitudSoporte
    {
        [Key]
        public int Id_Soporte { get; set; }

        public int Id_Analista { get; set; }

        public string Asunto { get; set; } = "";

        public string Mensaje { get; set; } = "";

        public string Estado { get; set; } = "Pendiente";

        public DateTime FechaEnvio { get; set; }

        public bool Leido { get; set; } = false;
    }
}