namespace trabajo.Models.DTOs
{
    public class CalificacionDto
    {
        public int IdComentario { get; set; }
        public int Calificacion { get; set; }
        public string Comentario { get; set; }
        public DateTime FechaComentario { get; set; }
        public string NombreCliente { get; set; }
        public string ApellidoCliente { get; set; }

        public string Analista { get; set; } = "";
    }

    public class ResumenCalificacionesDto
    {
        public decimal PuntajePromedio { get; set; }
        public int TotalCalificaciones { get; set; }
        public int Nps { get; set; }
        public string ClasificacionNps { get; set; }
        public Dictionary<int, int> DistribucionEstrellas { get; set; }
        public List<CalificacionDto> Calificaciones { get; set; }
    }
}
