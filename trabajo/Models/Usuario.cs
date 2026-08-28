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

        public string Correo { get; set; } = null!;

        public string clave { get; set; } = null!;
        public string Rol { get; set; } = null!;
        public DateTime FechaRegistro { get; set; }
        public string Genero { get; set; }
        public bool EstadoActivo { get; set; }
        public DateTime? UltimaConexion { get; set; }
        public bool NotificacionCorreo { get; set; } = true;

        public bool NotificacionSistema { get; set; } = true;

        public bool NotificacionRecordatorio { get; set; } = false;
        // ==========================================
        // SEGURIDAD POR VOZ
        // ==========================================

        public string? FraseVoz { get; set; }
        public string? AudioRegistro { get; set; }
        public byte[]? EmbeddingVoz { get; set; }
        public bool VozHabilitadaLogin { get; set; }
    }
}
