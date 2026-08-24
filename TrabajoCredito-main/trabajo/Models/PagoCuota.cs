using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace prestamoscreditos.Models
{
    [Table("pago_cuota")]
    public class PagoCuota
    {
        [Key]
        public int Id_PagoCuota { get; set; }

        public int Id_Cuota { get; set; }

        public decimal MontoPagado { get; set; }

        public string MetodoPago { get; set; }

        public string CodigoOperacion { get; set; }
        public string EntidadPago { get; set; }

        public DateTime FechaPago { get; set; }

        public string Estado { get; set; }
    }
}