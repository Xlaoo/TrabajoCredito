using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    [Table("perfil_financiero")]
    public class PerfilFinanciero
    {
        [Key]
        public int Id_PerfilFinanciero { get; set; }

        public decimal IngresoMensual { get; set; }

        public decimal EgresoMensual { get; set; }

        public bool OtrosCreditos { get; set; }

        public string Ocupacion { get; set; }

        public string MotivoPrestamo { get; set; }

        public DateTime FechaRegistro { get; set; }
        public string? NivelRiesgo { get; set; }
        public int SOLICITUD_CREDITO_Id_Solicitud { get; set; }

        [ForeignKey("SOLICITUD_CREDITO_Id_Solicitud")]
        public SolicitudCredito SOLICITUD_CREDITO { get; set; }
    }
}