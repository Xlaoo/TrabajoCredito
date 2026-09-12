namespace trabajo.Models
{
    public class AutenticadorSolicitud
    {
        public int Id { get; set; }

        public int UsuarioId { get; set; }

        public string NumeroVerificacion { get; set; }

        public string Estado { get; set; }

        public DateTime FechaCreacion { get; set; }

        public DateTime FechaExpiracion { get; set; }
    }
}