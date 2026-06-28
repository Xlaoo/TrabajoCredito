using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace trabajo.Models
{
    [Table("reporte_generado")]
    public class ReporteGenerado
    {
        [Key]
        public int Id { get; set; }

        public string TipoReporte { get; set; }
        public int Mes { get; set; }
        public int Anio { get; set; }
        public DateTime FechaGeneracion { get; set; }
        public int CantidadDatos { get; set; }
        public string Descripcion { get; set; }
        public string Formato { get; set; }
        public string SolicitadoPor { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public string Estado { get; set; }
        public bool Descargado { get; set; }
    }
}