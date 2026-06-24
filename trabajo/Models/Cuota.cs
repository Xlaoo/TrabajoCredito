using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    [Table("cuota")]
    public class Cuota
    {
        [Key]
        public int Id_Cuota { get; set; }

        public int NumeroCuota { get; set; }
        public decimal? MontoCuota { get; set; }
        public decimal? SaldoPendiente { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public int? Dias { get; set; }
        public decimal? Capital { get; set; }
        public decimal? Interes { get; set; }
        public decimal? Comisiones { get; set; }
        public decimal? Seguros { get; set; }
        public DateTime FechaLimitePago { get; set; }
        public string Estado { get; set; }

        public int SOLICITUD_CREDITO_Id_Solicitud { get; set; }

        [ForeignKey("SOLICITUD_CREDITO_Id_Solicitud")]
        public SolicitudCredito SOLICITUD_CREDITO { get; set; }
    }
} 