namespace trabajo.Models.ViewModels
{
    public class AdminPagosViewModel
    {
        public int TotalPagosMes { get; set; }
        public int PagosCompletadosMes { get; set; }
        public int PagosPendientesMes { get; set; }
        public decimal MontoTotalRecaudadoMes { get; set; }

        public string EstadoFiltro { get; set; } = "Todos";
        public string MetodoFiltro { get; set; } = "Todos";
        public string Busqueda { get; set; } = "";

        public List<PagoAdminItemViewModel> Pagos { get; set; } = new();
        public List<PagoAdminItemViewModel> CuotasPendientes { get; set; } = new();
    }

    public class PagoAdminItemViewModel
    {
        public int IdPagoCuota { get; set; }
        public int IdCuota { get; set; }
        public int IdSolicitud { get; set; }
        public string Cliente { get; set; } = "";
        public string Dni { get; set; } = "";
        public string Credito { get; set; } = "";
        public decimal Monto { get; set; }
        public DateTime FechaPago { get; set; }
        public string MetodoPago { get; set; } = "";
        public string Estado { get; set; } = "";
        public string Referencia { get; set; } = "";
        public string EntidadPago { get; set; } = "";
    }
}