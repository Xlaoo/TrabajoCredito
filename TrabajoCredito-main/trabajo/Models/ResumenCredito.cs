using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace trabajo.Models;
[Table("resumen_crediticio")]
public class ResumenCredito
{
    [Key]
    public int Id_Resumen { get; set; }

    public int Usuario_Id { get; set; }

    public int DeudasActivas { get; set; }

    public int DeudasPagadas { get; set; }

    public int DeudasVencidas { get; set; }
    public DateTime UltimaActualizacion { get; set; }
}