using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace trabajo.Models
{
    [Table("usuario")]
    public class Usuario
    {
        [Key]
        public int Id { get; set; }

        public string Nombre { get; set; } = null!;

        public string Apellido { get; set; } = null!;

        public string Dni { get; set; } = null!;

        public string Celular { get; set; } = null!;
        public DateTime? UltimaConexion { get; set; }

        public string Correo { get; set; } = null!;

        public string clave { get; set; } = null!;
        public string Rol { get; set; } = null!;
        public DateTime FechaRegistro { get; set; }
        public string Genero { get; set; }
        public bool EstadoActivo { get; set; }
    }
}
