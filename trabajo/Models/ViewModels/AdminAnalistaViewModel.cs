namespace trabajo.Models.ViewModels
{
    public class AdminAnalistaViewModel
    {
        public int IdAdministrador { get; set; }

        public int IdAnalista { get; set; }

        public string NombreAnalista { get; set; } = string.Empty;

        public string DniAnalista { get; set; } = string.Empty;

        public string CorreoAnalista { get; set; } = string.Empty;

        public string CelularAnalista { get; set; } = string.Empty;

        public bool EstadoActivo { get; set; }

        public DateTime FechaAsignacion { get; set; }

        public int SolicitudesAsignadas { get; set; }

        public int SolicitudesCompletadas { get; set; }

        public int SolicitudesEnProceso { get; set; }

        public int SolicitudesPendientes { get; set; }

        public int TasaEfectividad { get; set; }

        public List<AdminAnalistaActividadViewModel> Actividades { get; set; } = new();

        public List<AdminAnalistaMensajeViewModel> Mensajes { get; set; } = new();
    }

    public class AdminAnalistaActividadViewModel
    {
        public string Titulo { get; set; } = string.Empty;

        public string Descripcion { get; set; } = string.Empty;

        public DateTime Fecha { get; set; }

        public string Icono { get; set; } = "fas fa-users";
    }

    public class AdminAnalistaMensajeViewModel
    {
        public int IdMensaje { get; set; }

        public string RemitenteRol { get; set; } = string.Empty;

        public string Mensaje { get; set; } = string.Empty;

        public DateTime FechaEnvio { get; set; }
    }
}
