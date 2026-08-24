namespace trabajo.Models.ViewModels
{
    public class HistorialCreditoViewModel
    {
        public string NombreCompleto { get; set; }

        public string Dni { get; set; }

        public string Banco { get; set; }

        public string TipoCredito { get; set; }

        public decimal Monto { get; set; }

        public int PlazoMeses { get; set; }

        public string EstadoCredito { get; set; }

        public DateTime FechaInicio { get; set; }

        public DateTime FechaFin { get; set; }
    }
}