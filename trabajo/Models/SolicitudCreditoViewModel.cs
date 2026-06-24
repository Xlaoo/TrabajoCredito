namespace trabajo.Models
{
    public class SolicitudCreditoViewModel
    {
        public int IdSolicitud { get; set; }
        public string NumeroSolicitud { get; set; }
        public decimal MontoSolicitado { get; set; }
        public int PlazoMeses { get; set; }
        public decimal InteresEstimado { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public string Estado { get; set; }
    }
}
