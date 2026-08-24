using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    [Table("pago_cancelacion")]
    public class PagoCancelacion
    {
        [Key]
        public int Id_PagoCancelacion { get; set; }

        public decimal MontoDevuelto { get; set; }

        public string MetodoPago { get; set; }

        public string CodigoOperacion { get; set; }

        public string MotivoCancelacion { get; set; }

        public DateTime FechaPago { get; set; }

        public string Estado { get; set; }

        public int SOLICITUD_CREDITO_Id_Solicitud { get; set; }
    }
}