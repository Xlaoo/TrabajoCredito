using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    [Table("plantilla_notificacion")]
    public class PlantillaNotificacion
    {
        [Key]
        public int Id_Plantilla { get; set; }

        [Required]
        public string Nombre { get; set; }

        [Required]
        public string Descripcion { get; set; }

        [Required]
        public string Asunto { get; set; }

        [Required]
        public string Mensaje { get; set; }

        public string Icono { get; set; } = "fa-regular fa-bell";

        public string Color { get; set; } = "purple";

        public string Canal { get; set; } = "Correo Electrónico";

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public bool Activo { get; set; } = true;
    }
}