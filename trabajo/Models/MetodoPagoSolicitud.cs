using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    [Table("metodo_pago_solicitud")]
    public class MetodoPagoSolicitud
    {
        [Key]
        public int Id_MetodoPago { get; set; }

        public string MetodoPago { get; set; }
        public string EntidadPago { get; set; }
        public string NumeroCuentaPago { get; set; }
        public string TitularCuenta { get; set; }

        public int SOLICITUD_CREDITO_Id_Solicitud { get; set; }
    }
}