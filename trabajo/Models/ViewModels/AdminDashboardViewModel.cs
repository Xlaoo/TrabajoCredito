namespace trabajo.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        // TARJETAS SUPERIORES
        public int TotalClientes { get; set; }
        public int ClientesEstesMes { get; set; }

        public int TotalCreditosAprobados { get; set; }
        public decimal MontoOtorgado { get; set; }

        public int TotalEnEvaluacion { get; set; }
        public decimal MontoEnEvaluacion { get; set; }

        public int TotalCuotasEnMora { get; set; }
        public decimal MontoEnMora { get; set; }

        public int TotalReportesGenerados { get; set; }

        // RESUMEN DE CRÉDITOS
        public int CreditosAprobados { get; set; }
        public int CreditosEnEvaluacion { get; set; }
        public int CreditosDesembolsados { get; set; }
        public int CreditosRechazados { get; set; }
        public int TotalResumenCreditos { get; set; }

        // DISTRIBUCIÓN DE PAGOS DEL MES
        public int PagosCompletadosMes { get; set; }
        public int PagosPendientesMes { get; set; }
        public int PagosMoraMes { get; set; }

        // ACTIVIDAD RECIENTE
        public List<ActividadRecienteAdminViewModel> ActividadesRecientes { get; set; } = new();

        // INFORMACIÓN DEL SISTEMA
        public int UsuariosSistemaActivos { get; set; }
        public string EstadoBaseDatos { get; set; } = "Óptimo";
        public string VersionSistema { get; set; } = "v2.1.0";
        public int PorcentajeAlmacenamiento { get; set; } = 65;
        public DateTime UltimoRespaldo { get; set; }
        public int PorcentajePagosCompletados { get; set; }
        public int PorcentajePagosPendientes { get; set; }
        public int PorcentajePagosMora { get; set; }
    }

    public class ActividadRecienteAdminViewModel
    {
        public string Tipo { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string Icono { get; set; } = "";
        public string Color { get; set; } = "";
    }
}