using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    [Table("solicitud_credito")]
    public class SolicitudCredito
    {
        [Key]
        public int Id_Solicitud { get; set; }

        public string NumeroSolicitud { get; set; }

        public decimal MontoSolicitado { get; set; }

        public int PlazoMeses { get; set; }

        public decimal InteresEstimado { get; set; }

        public DateTime FechaSolicitud { get; set; }

        public string Estado { get; set; }

        public int Usuario_Id_Usuario { get; set; }
        [ForeignKey("Usuario_Id_Usuario")]
        public Usuario USUARIO { get; set; }
        public PerfilFinanciero PERFIL_FINANCIERO { get; set; }
        public bool NotificacionVistaAnalista { get; set; }
        public bool RevisionPendienteVista { get; set; }
        public bool NotificacionEdicionVista { get; set; }
        public virtual ICollection<HistorialCredito> HISTORIAL_CREDITOS { get; set; }
        public virtual ICollection<HistorialEstado> HISTORIAL_ESTADOS { get; set; }
    = new List<HistorialEstado>();
        public virtual ICollection<Mensaje> MENSAJES { get; set; }
    = new List<Mensaje>();
    }
}