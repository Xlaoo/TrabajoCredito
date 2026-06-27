namespace trabajo.Models.ViewModels
{
    // ViewModel principal de la vista de Reportes
    public class AdminReportesViewModel
    {
        // Filtros activos
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public string TipoCreditoFiltro { get; set; } = "Todos";
        public string EstadoFiltro { get; set; } = "Todos";

        // Stats principales
        public int TotalSolicitudes { get; set; }
        public int TotalAprobados { get; set; }
        public int TotalRechazados { get; set; }
        public decimal MontoOtorgado { get; set; }

        // Resumen de créditos (donut)
        public int TotalResumenCreditos { get; set; }
        public int CreditosAprobados { get; set; }
        public int CreditosEnEvaluacion { get; set; }
        public int CreditosDesembolsados { get; set; }
        public int CreditosRechazados { get; set; }

        // Mora y clientes
        public int TotalClientes { get; set; }
        public int ClientesEstesMes { get; set; }
        public int TotalCuotasEnMora { get; set; }
        public decimal MontoEnMora { get; set; }
        public int TotalReportesGenerados { get; set; }

        // Tabla resumen por mes
        public List<ResumenMesViewModel> ResumenPorMes { get; set; } = new();

        // Porcentajes calculados
        public double PorcentajeAprobados =>
            TotalSolicitudes > 0 ? Math.Round((double)TotalAprobados / TotalSolicitudes * 100, 1) : 0;

        public double PorcentajeRechazados =>
            TotalSolicitudes > 0 ? Math.Round((double)TotalRechazados / TotalSolicitudes * 100, 1) : 0;
    }

    // ViewModel para cada fila de la tabla resumen por mes
    public class ResumenMesViewModel
    {
        public int Anio { get; set; }
        public int Mes { get; set; }
        public int Solicitudes { get; set; }
        public int Aprobados { get; set; }
        public int Rechazados { get; set; }
        public decimal MontoDesembolsado { get; set; }

        public string NombreMes => new DateTime(Anio, Mes, 1).ToString("MMMM",
            new System.Globalization.CultureInfo("es-PE"));
    }
}
