namespace trabajo.Models
{
    public class AutenticadorDispositivo
    {
        public int Id { get; set; }

        public int UsuarioId { get; set; }

        public string FcmToken { get; set; }

        public string NombreDispositivo { get; set; }

        public DateTime FechaVinculacion { get; set; }

        public bool Activo { get; set; }
    }
}