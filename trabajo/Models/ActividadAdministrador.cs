using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    [Table("actividad_administrador")]
    public class ActividadAdministrador
    {
        [Key]
        public int IdActividad { get; set; }

        public int IdUsuario { get; set; }

        public string Tipo { get; set; } = string.Empty;

        public string Descripcion { get; set; } = string.Empty;

        public DateTime Fecha { get; set; } = DateTime.Now;
    }
}