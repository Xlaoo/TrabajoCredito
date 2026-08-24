using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using trabajo.Models;

[Table("historial_crediticio")]
public class HistorialCredito
{
    [Key]
    public int Id_Historial { get; set; }

    public string Banco { get; set; }

    public decimal Monto { get; set; }

    public int PlazoMeses { get; set; }

    public string EstadoCredito { get; set; }
    public string TipoCredito { get; set; }
    public int CuotasPagadas { get; set; }


    public DateTime? FechaInicio { get; set; }

    public DateTime? FechaFin { get; set; }

    public int SOLICITUD_CREDITO_Id_Solicitud { get; set; }

    [ForeignKey("SOLICITUD_CREDITO_Id_Solicitud")]
    public SolicitudCredito SOLICITUD_CREDITO { get; set; }
}