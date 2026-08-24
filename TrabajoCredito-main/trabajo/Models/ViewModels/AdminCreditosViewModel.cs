namespace trabajo.Models.ViewModels
{
    public class AdminCreditosViewModel
    {
        public int TotalCreditos { get; set; }
        public int CreditosAprobados { get; set; }
        public int CreditosEvaluacion { get; set; }
        public int CreditosRechazados { get; set; }
        public decimal MontoTotalSolicitado { get; set; }

        public string? Buscar { get; set; }
        public string Estado { get; set; } = "Todos";

        public int PaginaActual { get; set; }
        public int TotalPaginas { get; set; }
        public int TotalFiltrado { get; set; }
        public int InicioRegistro { get; set; }
        public int FinRegistro { get; set; }
        public int TotalDistribucionEstado { get; set; }
        public int CreditosActivosDistribucion { get; set; }
        public int CreditosCursoDistribucion { get; set; }
        public int CreditosPendientesDistribucion { get; set; }
        public int CreditosCanceladosDistribucion { get; set; }

        public int TotalDistribucionTipo { get; set; }
        public int CreditosPersonalTipo { get; set; }
        public int CreditosNegocioTipo { get; set; }
        public int CreditosEstudioTipo { get; set; }

        public int NuevosCreditosMes { get; set; }
        public decimal MontoOtorgadoMes { get; set; }
        public int CreditosDesembolsadosMes { get; set; }
        public List<CreditoAdminItemViewModel> Creditos { get; set; } = new();
        public List<TipoCreditoGraficoViewModel> TiposCreditoGrafico { get; set; } = new();
    }
    public class TipoCreditoGraficoViewModel
    {
        public string Tipo { get; set; } = "";
        public int Cantidad { get; set; }
        public decimal Porcentaje { get; set; }
        public string Color { get; set; } = "";
    }
    public class CreditoAdminItemViewModel
    {
        public int IdSolicitud { get; set; }
        public int NumeroFila { get; set; }

        public string NumeroSolicitud { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public string Iniciales { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;

        public decimal MontoSolicitado { get; set; }
        public int PlazoMeses { get; set; }
        public decimal InteresEstimado { get; set; }

        public string Estado { get; set; } = string.Empty;
        public DateTime FechaSolicitud { get; set; }

        public string TipoCredito { get; set; } = string.Empty;
        public string MotivoPrestamo { get; set; } = string.Empty;
    }
}