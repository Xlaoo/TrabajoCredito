namespace trabajo.Models.DTOs
{
    public class PagoGestionDto
    {
        public int IdCuota { get; set; }
        public int IdSolicitud { get; set; }

        public string Cliente { get; set; }
        public string Dni { get; set; }

        public string MetodoPago { get; set; }
        public string EntidadPago { get; set; }
        public string NumeroCuentaPago { get; set; }
        public string TitularCuenta { get; set; }

        public DateTime FechaRegistro { get; set; }
        public string Estado { get; set; }
    }
}