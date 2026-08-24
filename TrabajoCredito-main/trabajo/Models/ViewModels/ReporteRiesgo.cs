using System;
using System.ComponentModel.DataAnnotations;
namespace trabajo.Models.ViewModels
{
    public class ReporteRiesgo
    {
        [Key]
        public int IdReporte { get; set; }

        public DateTime FechaReporte { get; set; }

        public int Bajo { get; set; }

        public int Medio { get; set; }

        public int Alto { get; set; }

        public int Critico { get; set; }

        public int TotalAprobados { get; set; }

        public int TotalRechazados { get; set; }

        public int TotalClientesAtendidos { get; set; }
    }
}
