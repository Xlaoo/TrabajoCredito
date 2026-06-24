using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace prestamoscreditos.Models
{
    [Table("comentario_cliente")]
    public class ComentarioCliente
    {
        [Key]
        public int Id_Comentario { get; set; }

        public int Calificacion { get; set; }

        public string Comentario { get; set; }

        public DateTime FechaComentario { get; set; }
        public int Usuario_Id { get; set; }
        public bool VistaAnalista { get; set; }
    }
}